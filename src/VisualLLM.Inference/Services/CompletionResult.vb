Namespace Services
    Public Class CompletionResult
        Public Property Content As String = String.Empty
        Public Property FinishReason As String = "stop"
        Public Property PromptTokens As Integer
        Public Property CompletionTokens As Integer
    End Class
End Namespace
