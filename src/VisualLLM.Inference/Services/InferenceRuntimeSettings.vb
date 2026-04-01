Namespace Services
    Public Class InferenceRuntimeSettings
        Public Property ModelIdentifier As String = "default"
        Public Property ModelPath As String = String.Empty
        Public Property DefaultMaxTokens As Integer = 256
        Public Property ContextLength As Integer = 4096
        Public Property ThreadCount As Integer = Environment.ProcessorCount
        Public Property DefaultTemperature As Double = 0.7
        Public Property BackendName As String = "deterministic-enterprise-demo"
    End Class
End Namespace
