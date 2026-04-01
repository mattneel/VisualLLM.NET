Imports System.Text.Json
Imports System.Text.Json.Serialization
Imports System.Threading
Imports System.Threading.Tasks
Imports Microsoft.AspNetCore.Http
Imports VisualLLM.Inference.Models
Imports VisualLLM.Inference.Services
Imports VisualLLM.Native.Runtime

Namespace Api
    Public Class NativeChatCompletionBackend
        Implements IChatCompletionBackend

        Private Shared ReadOnly EventJsonOptions As New JsonSerializerOptions With {
            .DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        }

        Private ReadOnly _runtime As NativeLlamaRuntime
        Private ReadOnly _runtimeSettings As InferenceRuntimeSettings
        Private ReadOnly _nativeStatus As NativeRuntimeStatus

        Public Sub New(runtime As NativeLlamaRuntime, runtimeSettings As InferenceRuntimeSettings, nativeStatus As NativeRuntimeStatus)
            _runtime = runtime
            _runtimeSettings = runtimeSettings
            _nativeStatus = nativeStatus
        End Sub

        Public ReadOnly Property BackendName As String Implements IChatCompletionBackend.BackendName
            Get
                Return "llama.cpp-pinvoke"
            End Get
        End Property

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

        Public Function GetHealthSnapshotAsync(cancellationToken As CancellationToken) As Task(Of BackendHealthSnapshot) Implements IChatCompletionBackend.GetHealthSnapshotAsync
            Return Task.FromResult(
                New BackendHealthSnapshot With {
                    .IsReady = _runtime.IsInitialized,
                    .Message = If(_runtime.IsInitialized, "Native llama runtime loaded in-process.", _nativeStatus.Message),
                    .UpstreamUrl = String.Empty
                })
        End Function

        Public Async Function ProxyChatCompletionsAsync(
            request As ChatCompletionRequest,
            httpContext As HttpContext,
            cancellationToken As CancellationToken) As Task Implements IChatCompletionBackend.ProxyChatCompletionsAsync

            If request.Stream Then
                Await WriteStreamingResponseAsync(request, httpContext, cancellationToken)
                Return
            End If

            Dim completion = Await _runtime.CompleteChatAsync(request, cancellationToken)
            Dim response = BuildCompletionResponse(request, completion)
            httpContext.Response.StatusCode = StatusCodes.Status200OK
            Await httpContext.Response.WriteAsJsonAsync(response, cancellationToken)
        End Function

        Private Async Function WriteStreamingResponseAsync(request As ChatCompletionRequest, httpContext As HttpContext, cancellationToken As CancellationToken) As Task
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

            Dim completion = Await _runtime.CompleteChatAsync(
                request,
                cancellationToken,
                Async Function(piece As String, innerCancellationToken As CancellationToken)
                    Dim contentChunk As New ChatCompletionChunkResponse With {
                        .Id = responseId,
                        .Created = createdAt,
                        .Model = If(String.IsNullOrWhiteSpace(request.Model), _runtimeSettings.ModelIdentifier, request.Model),
                        .Choices = New List(Of ChatCompletionChunkChoice) From {
                            New ChatCompletionChunkChoice With {
                                .Index = 0,
                                .Delta = New ChatCompletionDelta With {.Content = piece}
                            }
                        }
                    }

                    Await WriteEventAsync(httpContext, contentChunk, innerCancellationToken)
                End Function)

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
