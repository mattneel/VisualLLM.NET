Imports System.Threading
Imports System.Threading.Tasks
Imports Microsoft.AspNetCore.Http
Imports VisualLLM.Inference.Models

Namespace Api
    Public Interface IChatCompletionBackend
        ReadOnly Property BackendName As String

        Function GetModelsAsync(cancellationToken As CancellationToken) As Task(Of ModelsResponse)

        Function ProxyChatCompletionsAsync(
            request As ChatCompletionRequest,
            httpContext As HttpContext,
            cancellationToken As CancellationToken) As Task

        Function GetHealthSnapshotAsync(cancellationToken As CancellationToken) As Task(Of BackendHealthSnapshot)
    End Interface
End Namespace
