Imports System.Text.Json
Imports System.Text.Json.Serialization

Namespace Models
    Public Class ChatMessage
        <JsonPropertyName("role")>
        Public Property Role As String = String.Empty

        <JsonPropertyName("content")>
        Public Property Content As String = String.Empty
    End Class

    Public Class ChatCompletionRequest
        <JsonPropertyName("model")>
        Public Property Model As String = "default"

        <JsonPropertyName("messages")>
        Public Property Messages As List(Of ChatMessage) = New List(Of ChatMessage)()

        <JsonPropertyName("stream")>
        Public Property Stream As Boolean

        <JsonPropertyName("max_tokens")>
        Public Property MaxTokens As Integer?

        <JsonPropertyName("temperature")>
        Public Property Temperature As Double?

        <JsonExtensionData>
        Public Property ExtensionData As Dictionary(Of String, JsonElement)
    End Class

    Public Class UsageSummary
        <JsonPropertyName("prompt_tokens")>
        Public Property PromptTokens As Integer

        <JsonPropertyName("completion_tokens")>
        Public Property CompletionTokens As Integer

        <JsonPropertyName("total_tokens")>
        Public Property TotalTokens As Integer
    End Class

    Public Class ChatCompletionChoice
        <JsonPropertyName("index")>
        Public Property Index As Integer

        <JsonPropertyName("message")>
        Public Property Message As ChatMessage = New ChatMessage()

        <JsonPropertyName("finish_reason")>
        Public Property FinishReason As String = "stop"
    End Class

    Public Class ChatCompletionResponse
        <JsonPropertyName("id")>
        Public Property Id As String = String.Empty

        <JsonPropertyName("object")>
        Public Property ObjectType As String = "chat.completion"

        <JsonPropertyName("created")>
        Public Property Created As Long

        <JsonPropertyName("model")>
        Public Property Model As String = String.Empty

        <JsonPropertyName("choices")>
        Public Property Choices As List(Of ChatCompletionChoice) = New List(Of ChatCompletionChoice)()

        <JsonPropertyName("usage")>
        Public Property Usage As UsageSummary = New UsageSummary()
    End Class

    Public Class ChatCompletionDelta
        <JsonPropertyName("role")>
        Public Property Role As String

        <JsonPropertyName("content")>
        Public Property Content As String
    End Class

    Public Class ChatCompletionChunkChoice
        <JsonPropertyName("index")>
        Public Property Index As Integer

        <JsonPropertyName("delta")>
        Public Property Delta As ChatCompletionDelta = New ChatCompletionDelta()

        <JsonPropertyName("finish_reason")>
        Public Property FinishReason As String
    End Class

    Public Class ChatCompletionChunkResponse
        <JsonPropertyName("id")>
        Public Property Id As String = String.Empty

        <JsonPropertyName("object")>
        Public Property ObjectType As String = "chat.completion.chunk"

        <JsonPropertyName("created")>
        Public Property Created As Long

        <JsonPropertyName("model")>
        Public Property Model As String = String.Empty

        <JsonPropertyName("choices")>
        Public Property Choices As List(Of ChatCompletionChunkChoice) = New List(Of ChatCompletionChunkChoice)()
    End Class

    Public Class ModelDescriptor
        <JsonPropertyName("id")>
        Public Property Id As String = String.Empty

        <JsonPropertyName("object")>
        Public Property ObjectType As String = "model"

        <JsonPropertyName("created")>
        Public Property Created As Long

        <JsonPropertyName("owned_by")>
        Public Property OwnedBy As String = "visualllm"
    End Class

    Public Class ModelsResponse
        <JsonPropertyName("object")>
        Public Property ObjectType As String = "list"

        <JsonPropertyName("data")>
        Public Property Data As List(Of ModelDescriptor) = New List(Of ModelDescriptor)()
    End Class
End Namespace
