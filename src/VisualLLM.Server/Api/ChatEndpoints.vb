Imports System.Threading
Imports System.Threading.Tasks
Imports Microsoft.AspNetCore.Http
Imports VisualLLM.Inference.Models
Imports VisualLLM.Inference.Services

Namespace Api
    Public NotInheritable Class ChatEndpoints
        Private Sub New()
        End Sub

        Public Shared Async Function GetModelsAsync(backend As IChatCompletionBackend, cancellationToken As CancellationToken) As Task(Of IResult)
            Dim response = Await backend.GetModelsAsync(cancellationToken)
            Return Results.Ok(response)
        End Function

        Public Shared Async Function PostChatCompletionsAsync(
            request As ChatCompletionRequest,
            httpContext As HttpContext,
            runtimeSettings As InferenceRuntimeSettings,
            backend As IChatCompletionBackend,
            cancellationToken As CancellationToken) As Task

            If request Is Nothing Then
                Await WriteBadRequestAsync(httpContext, "A JSON request body is required.", cancellationToken)
                Return
            End If

            If request.Messages Is Nothing OrElse request.Messages.Count = 0 Then
                Await WriteBadRequestAsync(httpContext, "At least one message is required.", cancellationToken)
                Return
            End If

            If String.IsNullOrWhiteSpace(request.Model) Then
                request.Model = runtimeSettings.ModelIdentifier
            End If

            Await backend.ProxyChatCompletionsAsync(request, httpContext, cancellationToken)
        End Function

        Private Shared Async Function WriteBadRequestAsync(httpContext As HttpContext, message As String, cancellationToken As CancellationToken) As Task
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest
            Await httpContext.Response.WriteAsJsonAsync(
                New With {
                    .error = New With {
                        .message = message,
                        .type = "invalid_request_error"
                    }
                },
                cancellationToken)
        End Function
    End Class
End Namespace
