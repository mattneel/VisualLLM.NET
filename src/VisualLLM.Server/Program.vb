Imports VisualLLM.Server.Hosting

Module Program
    Sub Main(args As String())
        Dim application = ServerBootstrapper.BuildApplication(args)
        application.Run()
    End Sub
End Module
