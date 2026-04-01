Imports System.IO
Imports System.Text
Imports System.Threading
Imports System.Threading.Tasks
Imports Microsoft.AspNetCore.Http
Imports VisualLLM.Inference.Models
Imports VisualLLM.Inference.Services
Imports VisualLLM.Server.Api
Imports Xunit

Namespace Server
    Public Class ChatEndpointsTests
        <Fact>
        Public Async Function PostChatCompletionsAsync_WritesStandardJsonResponse() As Task
            Dim context = CreateHttpContext()
            Dim request As New ChatCompletionRequest With {
                .Model = "default",
                .Messages = New List(Of ChatMessage) From {
                    New ChatMessage With {.Role = "user", .Content = "Hello!"}
                }
            }

            Await ChatEndpoints.PostChatCompletionsAsync(
                request,
                context,
                New InferenceRuntimeSettings(),
                New DemoChatCompletionBackend(
                    New DeterministicInferenceEngine(New PromptComposer()),
                    New InferenceRuntimeSettings()),
                CancellationToken.None)

            context.Response.Body.Position = 0

            Using reader As New StreamReader(context.Response.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks:=False, leaveOpen:=True)
                Dim json = Await reader.ReadToEndAsync()
                Assert.Contains("""object"":""chat.completion""", json)
                Assert.Contains("""role"":""assistant""", json)
            End Using
        End Function

        <Fact>
        Public Async Function PostChatCompletionsAsync_WritesStreamingResponse() As Task
            Dim context = CreateHttpContext()
            Dim request As New ChatCompletionRequest With {
                .Model = "default",
                .Stream = True,
                .Messages = New List(Of ChatMessage) From {
                    New ChatMessage With {.Role = "user", .Content = "Hello!"}
                }
            }

            Await ChatEndpoints.PostChatCompletionsAsync(
                request,
                context,
                New InferenceRuntimeSettings(),
                New DemoChatCompletionBackend(
                    New DeterministicInferenceEngine(New PromptComposer()),
                    New InferenceRuntimeSettings()),
                CancellationToken.None)

            context.Response.Body.Position = 0

            Using reader As New StreamReader(context.Response.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks:=False, leaveOpen:=True)
                Dim payload = Await reader.ReadToEndAsync()
                Assert.Equal("text/event-stream", context.Response.ContentType)
                Assert.Contains("data: [DONE]", payload)
                Assert.Contains("chat.completion.chunk", payload)
            End Using
        End Function

        Private Shared Function CreateHttpContext() As DefaultHttpContext
            Dim context As New DefaultHttpContext()
            context.Response.Body = New MemoryStream()
            Return context
        End Function
    End Class
End Namespace
