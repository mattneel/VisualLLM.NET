Namespace Runtime
    Public Class NativeRuntimeStatus
        Public Property IsAvailable As Boolean
        Public Property BackendName As String = "llama.cpp-runtime"
        Public Property Message As String = String.Empty
        Public Property RuntimeIdentifier As String = String.Empty
        Public Property ResolvedLibraryPath As String = String.Empty
        Public Property ResolvedServerExecutablePath As String = String.Empty
        Public Property SearchRoots As List(Of String) = New List(Of String)()

        Public ReadOnly Property HasLibrary As Boolean
            Get
                Return Not String.IsNullOrWhiteSpace(ResolvedLibraryPath)
            End Get
        End Property

        Public ReadOnly Property HasServerExecutable As Boolean
            Get
                Return Not String.IsNullOrWhiteSpace(ResolvedServerExecutablePath)
            End Get
        End Property
    End Class
End Namespace
