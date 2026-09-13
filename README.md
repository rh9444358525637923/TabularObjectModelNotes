# セマンティックモデル ＆ レポート 運用管理・CDフロー[cite: 2]

## 1. 全体アーキテクチャと採用技術の前提[cite: 2]

* **レポート管理:** `PBIX`（Power BI Desktop ファイル）※PBIPがGAされ次第、速やかにPBIPへ移行予定[cite: 2]
* **モデル管理:** `TMDL`（Tabular Model Definition Language）※公式TOMライブラリにてシリアライズ（GA済み）[cite: 2]
* **バージョン管理:** 共有ストレージ（ファイルサーバー等）でのリリースバージョン別フォルダ管理[cite: 2]
* **デプロイ（CD）:**[cite: 2]
  * レポート: Power BI Desktop からの発行（Publish）[cite: 2]
  * モデル: TOM ライブラリを組み込んだ独自デプロイツール（VB.NET / PowerShell）によるXMLAエンドポイントへの更新[cite: 2]

### 【なぜこのアーキテクチャなのか？（採用理由）】[cite: 2]
1. **クラウド上のDirect Lakeの制約:** Direct Lakeモデルは従来のPBIXファイルとしてダウンロードできないため、モデル定義をテキスト（TMDL）として抽出・保存する仕組みが不可欠である。[cite: 2]
2. **完全な履歴保持と切り戻しの担保:** Deployment Pipelines（Fabric標準機能）単体では、Git未導入環境において「ソースコードとしてのバックアップ」が残らない。[cite: 2] 万一の障害時に「1行のDAXの修正」レベルでの迅速な差分比較・切り戻しを実現するため、手元へのTMDL/PBIX抽出とファイル保管を必須とする。[cite: 2]
3. **公式サポート（ガバナンス要件）の遵守:** サードパーティ製ツール（Tabular Editor等）をパイプラインに組み込まず、Microsoft純正の .NET ライブラリ（AMO/TOM）を使用することで、エンタープライズのセキュリティおよびサポート要件をクリアする。[cite: 2]
4. **PBIPのGA待ち（段階的移行）:** 現状レポートフォーマットであるPBIPはプレビュー版のためPBIXを採用するが、モデル側（TMDL）を先行して分離・テキスト管理しておくことで、将来PBIPがGAされた際の「完全なソースコード管理」へ移行する下地を作る。[cite: 2]

---

## 2. 運用ディレクトリ構成ルール[cite: 2]

共有フォルダ内にリリースバージョンごとにフォルダを作成し、その時点の「完全なスナップショット」を保管します。[cite: 2]

```text
C:\Projects\PowerBI_Release_History\
├── v1.0.0_20260901\                ← リリース済みの過去バージョン
│   ├── Report\                     ← PBIXファイル
│   │   └── SalesReport.pbix
│   └── SemanticModel\              ← TMDL一式（テキストファイル群）
│       ├── tables\
│       ├── roles\
│       └── model.tmdl
│
└── v1.1.0_20260913\                ← 今回作業する新しいバージョン
    ├── Report\
    └── SemanticModel\
```

---

## 3. 開発から本番デプロイまでのワークフロー[cite: 2]

開発者が改修に着手し、本番環境へデプロイするまでの一連のステップです。[cite: 2]

### Step 1: 開発環境での実装とテスト[cite: 2]
1. **レポートの修正:** 手元のPower BI Desktopで、最新バージョンのフォルダ（例: `v1.1.0_20260913\Report\`）にあるPBIXファイルを開き、修正を行う。[cite: 2]
2. **モデルの修正:** Fabricの「開発用ワークスペース」上（Webエディタ等）で直接、セマンティックモデル（DAXメジャーやリレーションなど）を修正・検証する。[cite: 2]

**【理由】** Direct Lakeのパフォーマンスを正確に検証するためには、手元のPCではなく、Fabricのコンピュートリソースが割り当たっている開発ワークスペース上でモデルを修正・テストすることが最も確実であるためです。[cite: 2]

### Step 2: ソースコードの抽出とバージョン確定（バックアップ）[cite: 2]
開発・検証が完了したら、その状態を「確定バージョン」としてローカルに保存します。[cite: 2]

1. **レポートの保存:** Power BI DesktopでPBIXファイルを上書き保存し、閉じる。[cite: 2]
2. **モデルの抽出（シリアライズ）:**[cite: 2]
   * 開発用ワークスペースのXMLAエンドポイントを指定し、デプロイツール（シリアライズモード）を実行する。[cite: 2]
   * 開発環境の最新モデルが `v1.1.0_20260913\SemanticModel\` 配下にTMDLフォルダとして出力（バックアップ）される。[cite: 2]

**【理由】** ここで抽出されたTMDLファイルとPBIXファイルは、「本番へ反映するためのマスターデータ」となります。[cite: 2] 特にTMDLは、将来障害が発生した際、「v1.0.0とv1.1.0のTMDLフォルダをWinMerge等で比較し、どこを変更したのか」をテキスト差分で特定するための重要な資産となります。[cite: 2]

### Step 3: 本番環境へのデプロイ（CD）[cite: 2]
テスト済みの定義を、本番環境へ反映します。[cite: 2]

1. **モデルのデプロイ:**[cite: 2]
   * デプロイツール（PowerShellバッチ等）を実行し、ターゲットを「本番ワークスペース（PROD）」に指定する。[cite: 2]
   * ツールはローカルの `v1.1.0_20260913\SemanticModel\` のTMDLを読み込み、本番のセマンティックモデルへメタデータを上書き（Update）する。[cite: 2]
2. **レポートのデプロイ:**[cite: 2]
   * ローカルのPBIXファイルをPower BI Desktopで開く。[cite: 2]
   * レポートが参照しているセマンティックモデルの接続先を、「開発ワークスペース」から「本番ワークスペース」へ切り替える（データソース設定）。[cite: 2]
   * Power BI Desktopの「発行（Publish）」ボタンを押し、本番ワークスペースへアップロードする。[cite: 2]

**【理由】**[cite: 2]
* **モデル先行デプロイの原則:** レポートには新しいモデル（新しいDAXメジャーなど）が必要になるため、必ず「モデルのデプロイ」を先に行います。[cite: 2]
* **直接編集の禁止:** 本番ワークスペースのモデルを人間が直接編集するリスク（操作ミスや意図しない変更）を排除し、「手元で検証され、バックアップが取られたTMDL」だけが機械的に本番へ適用される仕組みを担保します。[cite: 2]

---

## 4. デプロイツール（FabricTmdlDeployer）の実行環境と使い方

### 4.1 必須環境
* **OS:** Windows 10/11
* **フレームワーク:** .NET Framework 4.7.2
* **認証:** 実行ユーザーのEntra ID（MFA対応、ログインポップアップによる対話型認証）

### 4.2 実行用フォルダ構成
ツール本体（`.exe`）と依存する `.dll` ファイル群を同一のフォルダに配置して実行します。実行すると、自動的にツールと同じ階層に `SemanticModel` フォルダが作成（または参照）されます。

```text
C:\Tools\FabricDeployTool\
├── FabricTmdlDeployer.exe              ← ツール本体
├── Microsoft.AnalysisServices.dll      ← 依存ライブラリ群（必須）
├── Microsoft.AnalysisServices.Core.dll
├── Microsoft.AnalysisServices.Tabular.dll
├── Microsoft.AnalysisServices.Tabular.Json.dll
└── SemanticModel\                      ← 自動生成・参照されるTMDL出力先フォルダ
    ├── tables\
    ├── roles\
    └── model.tmdl
```

### 4.3 コマンド実行手順

**【シリアライズ（ダウンロード）】**
指定したワークスペースのモデル定義を手元の `SemanticModel` フォルダへ書き出します（Step 2で利用）。
```powershell
FabricTmdlDeployer.exe serialize "powerbi://api.powerbi.com/v1.0/myorg/Workspace_Dev" "DatasetName"
```

**【デプロイ（アップロード）】**
ツールと同階層にある `SemanticModel` フォルダの内容で、ターゲット環境のモデルを上書きします（Step 3で利用）。
```powershell
FabricTmdlDeployer.exe deploy "powerbi://api.powerbi.com/v1.0/myorg/Workspace_Prod" "DatasetName"
```

> 実際の運用を想定した引数のサンプルです。URLやデータセット名にスペースが含まれる可能性があるため、引数はダブルクォーテーション(`"`)で囲むことを推奨します。
> 
> **サンプル1: 開発環境からモデル「Sales_DirectLake」をダウンロードする**
> ```powershell
> FabricTmdlDeployer.exe serialize "powerbi://api.powerbi.com/v1.0/myorg/Fabric_Dev_Workspace" "Sales_DirectLake"
> ```
> 
> **サンプル2: 本番環境へモデル「Sales_DirectLake」をデプロイする**
> ```powershell
> FabricTmdlDeployer.exe deploy "powerbi://api.powerbi.com/v1.0/myorg/Fabric_Prod_Workspace" "Sales_DirectLake"
> ```

---

## 5. ツールソースコード (VB.NET)

ビルドに使用する最終版のソースコード（`Module1.vb`）です。
※ 実行環境を構築する際は、NuGetパッケージ `Microsoft.AnalysisServices` のインストールが必要です。

```vb
Imports System
Imports System.IO
Imports Microsoft.AnalysisServices.Tabular
Imports Microsoft.AnalysisServices.Tabular.Tmdl

Namespace FabricTmdlDeployer
    Module Module1

        ' EXEと同じ階層の "SemanticModel" フォルダを固定で使用する
        Private ReadOnly BaseTmdlFolder As String = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SemanticModel")

        Sub Main(args As String())
            Console.WriteLine("==================================================")
            Console.WriteLine(" Fabric Semantic Model TMDL Tool (.NET 4.7.2) ")
            Console.WriteLine("==================================================")

            If args.Length < 3 Then
                PrintUsage()
                Environment.Exit(1)
            End If

            Dim mode As String = args(0).ToLowerInvariant()
            Dim workspaceXmla As String = args(1)
            Dim datasetName As String = args(2)

            ' TLS 1.2 を明示的に指定（Fabric へのセキュア接続に必須）
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12

            Try
                Select Case mode
                    Case "deploy"
                        ExecuteDeploy(workspaceXmla, datasetName)
                    Case "serialize"
                        ExecuteSerialize(workspaceXmla, datasetName)
                    Case Else
                        Console.WriteLine($"未知のモードです: {mode}")
                        PrintUsage()
                        Environment.Exit(1)
                End Select

                Environment.Exit(0)

            Catch ex As Exception
                HandleException(ex)
            End Try
        End Sub

        ''' <summary>
        ''' 指定したFabricワークスペースのモデルをEXE直下のフォルダへ書き出す（シリアライズ）
        ''' </summary>
        Private Sub ExecuteSerialize(workspaceXmla As String, datasetName As String)
            Dim connectionString As String = BuildInteractiveConnectionString(workspaceXmla, datasetName)

            Console.WriteLine($"[1/3] Fabric ワークスペースへ接続中: {workspaceXmla}")
            Using server As New Server()
                server.Connect(connectionString)

                Console.WriteLine($"[2/3] リモートモデルを検索中: '{datasetName}'")
                Dim remoteDatabase As Database = server.Databases.GetByName(datasetName)

                ' Initial Catalog を指定しているため、正常なら必ず Model は存在する
                If remoteDatabase.Model Is Nothing Then
                    Throw New Exception($"サーバー上のモデル '{datasetName}' の中身を読み込めませんでした。")
                End If

                If Not Directory.Exists(BaseTmdlFolder) Then
                    Directory.CreateDirectory(BaseTmdlFolder)
                End If

                Console.WriteLine($"[3/3] TMDL 定義をローカルへ出力中: {BaseTmdlFolder}")
                ' データベースの外枠ではなく、モデル本体（テーブル群）をシリアライズする
                TmdlSerializer.SerializeModelToFolder(remoteDatabase.Model, BaseTmdlFolder)

                Console.ForegroundColor = ConsoleColor.Green
                Console.WriteLine("==================================================")
                Console.WriteLine($"[シリアライズ成功] テーブル数 {remoteDatabase.Model.Tables.Count} 件をダウンロードしました。")
                Console.WriteLine("==================================================")
                Console.ResetColor()
            End Using
        End Sub

        ''' <summary>
        ''' EXE直下のTMDLフォルダをFabricワークスペースのモデルへ反映する（デプロイ）
        ''' </summary>
        Private Sub ExecuteDeploy(workspaceXmla As String, datasetName As String)
            If Not Directory.Exists(BaseTmdlFolder) Then
                Throw New DirectoryNotFoundException($"TMDL フォルダが見つかりません: {BaseTmdlFolder}")
            End If

            Console.WriteLine($"[1/4] TMDL 定義をロード中: {BaseTmdlFolder}")
            Dim localModel As Model = TmdlSerializer.DeserializeModelFromFolder(BaseTmdlFolder)
            Console.WriteLine($"      ローカルモデル読み込み成功: テーブル数 = {localModel.Tables.Count}")

            Dim connectionString As String = BuildInteractiveConnectionString(workspaceXmla, datasetName)

            Console.WriteLine($"[2/4] Fabric ワークスペースへ接続中: {workspaceXmla}")
            Using server As New Server()
                server.Connect(connectionString)

                Console.WriteLine($"[3/4] リモートモデルを検索中: '{datasetName}'")
                Dim remoteDatabase As Database = server.Databases.GetByName(datasetName)

                If remoteDatabase.Model Is Nothing Then
                    Throw New Exception($"サーバー上のモデル '{datasetName}' の中身を読み込めませんでした。")
                End If

                Console.WriteLine("[4/4] 変更差分をコピーして保存中...")
                localModel.CopyTo(remoteDatabase.Model)
                remoteDatabase.Model.SaveChanges()

                Console.ForegroundColor = ConsoleColor.Green
                Console.WriteLine("==================================================")
                Console.WriteLine($"[デプロイ成功] モデル '{datasetName}' のデプロイが完了しました。")
                Console.WriteLine("==================================================")
                Console.ResetColor()
            End Using
        End Sub

        ''' <summary>
        ''' 対話型認証用の接続文字列を生成
        ''' Initial Catalog を指定して、サーバー側でモデルを強制的にメモリにロードさせる
        ''' </summary>
        Private Function BuildInteractiveConnectionString(workspaceXmla As String, datasetName As String) As String
            Return $"Data Source={workspaceXmla};Initial Catalog={datasetName};"
        End Function

        Private Sub HandleException(ex As Exception)
            If TypeOf ex Is TmdlFormatException Then
                Dim fex As TmdlFormatException = DirectCast(ex, TmdlFormatException)
                Console.ForegroundColor = ConsoleColor.Red
                Console.WriteLine("==================================================")
                Console.WriteLine("[構文エラー (TmdlFormatException)]")
                Console.WriteLine($"ファイル : {fex.Document}")
                Console.WriteLine($"エラー行 : {fex.Line}")
                Console.WriteLine($"該当箇所 : {fex.LineText}")
                Console.WriteLine($"エラー内容: {fex.Message}")
                Console.WriteLine("==================================================")
                Console.ResetColor()
            ElseIf TypeOf ex Is TmdlSerializationException Then
                Dim sex As TmdlSerializationException = DirectCast(ex, TmdlSerializationException)
                Console.ForegroundColor = ConsoleColor.Red
                Console.WriteLine("==================================================")
                Console.WriteLine("[メタデータ論理エラー (TmdlSerializationException)]")
                Console.WriteLine($"エラー内容: {sex.Message}")
                Console.WriteLine("==================================================")
                Console.ResetColor()
            Else
                Console.ForegroundColor = ConsoleColor.Red
                Console.WriteLine("==================================================")
                Console.WriteLine($"[処理失敗] {ex.Message}")
                If ex.InnerException IsNot Nothing Then
                    Console.WriteLine($"詳細: {ex.InnerException.Message}")
                End If
                Console.WriteLine("==================================================")
                Console.ResetColor()
            End If
            Environment.Exit(1)
        End Sub

        Private Sub PrintUsage()
            Console.ForegroundColor = ConsoleColor.Yellow
            Console.WriteLine("使用法:")
            Console.WriteLine("  FabricTmdlDeployer.exe deploy <WorkspaceUrl> <DatasetName>")
            Console.WriteLine("  FabricTmdlDeployer.exe serialize <WorkspaceUrl> <DatasetName>")
            Console.ResetColor()
        End Sub

    End Module
End Namespace
```

---

## 6. 将来の展望（PBIP GA時 ＆ Git導入時）[cite: 2]

この運用フローは、Microsoft の最新アップデートへスムーズに追従できるよう設計されています。[cite: 2]

1. **PBIPのGAに伴うレポート管理の移行**[cite: 2]
   * 現在プレビュー版である「Power BI Project（PBIP）」フォーマットがGA（一般提供）され次第、レポートの保存形式を `PBIX` から `PBIP` へ移行します。[cite: 2]
   * これにより、モデル（TMDL）だけでなく、レポート（PBIR形式のJSON群）もテキストベースで管理可能となり、完全な差分レビューが実現します。[cite: 2]
2. **Git導入時のシームレスな統合**[cite: 2]
   * 手元の「バージョン別フォルダ管理」は、Azure DevOps Repos や GitHub の「リポジトリ（コミット履歴）」にそのまま置き換わります。[cite: 2]
   * Step 3 のデプロイ作業は、Gitへ `push`（または Pull Request のマージ）を行うだけで、Fabric の「Git統合機能」がTMDLとPBIPを自動検知し、本番ワークスペースへ同期する形に進化します。[cite: 2]

現行の運用は、将来の完全自動化・テキストベース管理に向けた、組織のナレッジ蓄積と移行準備として最適なステップとなります。[cite: 2]