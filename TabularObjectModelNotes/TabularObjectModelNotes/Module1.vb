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