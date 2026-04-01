Imports System.Text.Json
Imports System.Text.Json.Serialization
Imports System.Threading
Imports System.Threading.Tasks
Imports Microsoft.AspNetCore.Http
Imports VisualLLM.Inference.Models
Imports VisualLLM.Inference.Services

Namespace Api
    Public Class DemoChatCompletionBackend
        Implements IChatCompletionBackend

        Private Shared ReadOnly EventJsonOptions As New JsonSerializerOptions With {
            .DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        }

        Private ReadOnly _engine As IInferenceEngine
        Private ReadOnly _runtimeSettings As InferenceRuntimeSettings

        Public Sub New(engine As IInferenceEngine, runtimeSettings As InferenceRuntimeSettings)
            _engine = engine
            _runtimeSettings = runtimeSettings
        End Sub

        Public ReadOnly Property BackendName As String Implements IChatCompletionBackend.BackendName
            Get
                Return "demo-backend"
            End Get
        End Property

        Public Function GetHealthSnapshotAsync(cancellationToken As CancellationToken) As Task(Of BackendHealthSnapshot) Implements IChatCompletionBackend.GetHealthSnapshotAsync
            Return Task.FromResult(New BackendHealthSnapshot With {
                .IsReady = True,
                .Message = "Demo backend is active. It exists for tests and controlled nonsense only.",
                .UpstreamUrl = String.Empty
            })
        End Function

        Public Function GetModelsAsync(cancellationToken As CancellationToken) As Task(Of ModelsResponse) Implements IChatCompletionBackend.GetModelsAsync
            Return Task.FromResult(
                New ModelsResponse With {
                    .Data = New List(Of ModelDescriptor) From {
                        New ModelDescriptor With {
                            .Id = _runtimeSettings.ModelIdentifier,
                            .Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                            .OwnedBy = "visualllm"
                        }
                    }
                })
        End Function

        Public Async Function ProxyChatCompletionsAsync(
            request As ChatCompletionRequest,
            httpContext As HttpContext,
            cancellationToken As CancellationToken) As Task Implements IChatCompletionBackend.ProxyChatCompletionsAsync

            Dim completion = Await _engine.CompleteAsync(request, _runtimeSettings, cancellationToken)

            If request.Stream Then
                Await WriteStreamingResponseAsync(httpContext, request, completion, cancellationToken)
                Return
            End If

            Dim response = BuildCompletionResponse(request, completion)
            httpContext.Response.StatusCode = StatusCodes.Status200OK
            Await httpContext.Response.WriteAsJsonAsync(response, cancellationToken)
        End Function

        Private Shared Function BuildCompletionResponse(request As ChatCompletionRequest, completion As CompletionResult) As ChatCompletionResponse
            Return New ChatCompletionResponse With {
                .Id = CreateResponseId(),
                .Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                .Model = If(String.IsNullOrWhiteSpace(request.Model), "default", request.Model),
                .Choices = New List(Of ChatCompletionChoice) From {
                    New ChatCompletionChoice With {
                        .Index = 0,
                        .Message = New ChatMessage With {
                            .Role = "assistant",
                            .Content = completion.Content
                        },
                        .FinishReason = completion.FinishReason
                    }
                },
                .Usage = New UsageSummary With {
                    .PromptTokens = completion.PromptTokens,
                    .CompletionTokens = completion.CompletionTokens,
                    .TotalTokens = completion.PromptTokens + completion.CompletionTokens
                }
            }
        End Function

        Private Async Function WriteStreamingResponseAsync(
            httpContext As HttpContext,
            request As ChatCompletionRequest,
            completion As CompletionResult,
            cancellationToken As CancellationToken) As Task

            Dim responseId = CreateResponseId()
            Dim createdAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()

            httpContext.Response.StatusCode = StatusCodes.Status200OK
            httpContext.Response.ContentType = "text/event-stream"
            httpContext.Response.Headers.CacheControl = "no-cache"

            Dim openingChunk As New ChatCompletionChunkResponse With {
                .Id = responseId,
                .Created = createdAt,
                .Model = If(String.IsNullOrWhiteSpace(request.Model), _runtimeSettings.ModelIdentifier, request.Model),
                .Choices = New List(Of ChatCompletionChunkChoice) From {
                    New ChatCompletionChunkChoice With {
                        .Index = 0,
                        .Delta = New ChatCompletionDelta With {.Role = "assistant"}
                    }
                }
            }

            Await WriteEventAsync(httpContext, openingChunk, cancellationToken)

            For Each segment In _engine.SplitForStreaming(completion.Content)
                Dim contentChunk As New ChatCompletionChunkResponse With {
                    .Id = responseId,
                    .Created = createdAt,
                    .Model = If(String.IsNullOrWhiteSpace(request.Model), _runtimeSettings.ModelIdentifier, request.Model),
                    .Choices = New List(Of ChatCompletionChunkChoice) From {
                        New ChatCompletionChunkChoice With {
                            .Index = 0,
                            .Delta = New ChatCompletionDelta With {.Content = segment}
                        }
                    }
                }

                Await WriteEventAsync(httpContext, contentChunk, cancellationToken)
            Next

            Dim terminalChunk As New ChatCompletionChunkResponse With {
                .Id = responseId,
                .Created = createdAt,
                .Model = If(String.IsNullOrWhiteSpace(request.Model), _runtimeSettings.ModelIdentifier, request.Model),
                .Choices = New List(Of ChatCompletionChunkChoice) From {
                    New ChatCompletionChunkChoice With {
                        .Index = 0,
                        .Delta = New ChatCompletionDelta(),
                        .FinishReason = completion.FinishReason
                    }
                }
            }

            Await WriteEventAsync(httpContext, terminalChunk, cancellationToken)
            Await httpContext.Response.WriteAsync("data: [DONE]" & Environment.NewLine & Environment.NewLine, cancellationToken)
            Await httpContext.Response.Body.FlushAsync(cancellationToken)
        End Function

        Private Shared Async Function WriteEventAsync(httpContext As HttpContext, payload As ChatCompletionChunkResponse, cancellationToken As CancellationToken) As Task
            Dim json = JsonSerializer.Serialize(payload, EventJsonOptions)
            Await httpContext.Response.WriteAsync($"data: {json}{Environment.NewLine}{Environment.NewLine}", cancellationToken)
            Await httpContext.Response.Body.FlushAsync(cancellationToken)
        End Function

        Private Shared Function CreateResponseId() As String
            Return $"chatcmpl-{Guid.NewGuid():N}"
        End Function
    End Class
End Namespace
