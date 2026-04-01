Namespace Runtime
    Public Class NativeRuntimeOptions
        Public Property LibraryPath As String = String.Empty
        Public Property ModelPath As String = String.Empty
        Public Property ServerExecutablePath As String = String.Empty
        Public Property WorkingDirectory As String = String.Empty
        Public Property ContextLength As Integer = 4096
        Public Property ThreadCount As Integer = Environment.ProcessorCount
        Public Property GpuLayers As Integer = 99
    End Class
End Namespace
