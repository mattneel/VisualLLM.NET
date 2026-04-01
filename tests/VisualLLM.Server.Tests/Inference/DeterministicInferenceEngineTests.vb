Imports System.Threading
Imports System.Threading.Tasks
Imports VisualLLM.Inference.Models
Imports VisualLLM.Inference.Services
Imports Xunit

Namespace Inference
    Public Class DeterministicInferenceEngineTests
        <Fact>
        Public Async Function CompleteAsync_ReturnsCompletionAndUsage() As Task
            Dim engine As New DeterministicInferenceEngine(New PromptComposer())
            Dim request As New ChatCompletionRequest With {
                .Messages = New List(Of ChatMessage) From {
                    New ChatMessage With {.Role = "user", .Content = "Explain Visual Basic for AI workloads."}
                }
            }
            Dim settings As New InferenceRuntimeSettings()

            Dim result = Await engine.CompleteAsync(request, settings, CancellationToken.None)

            Assert.NotEmpty(result.Content)
            Assert.True(result.PromptTokens > 0)
            Assert.True(result.CompletionTokens > 0)
        End Function

        <Fact>
        Public Sub SplitForStreaming_BreaksLongContentIntoChunks()
            Dim engine As New DeterministicInferenceEngine(New PromptComposer())
            Dim segments = engine.SplitForStreaming("one two three four five six seven eight nine ten")

            Assert.True(segments.Count >= 2)
            Assert.All(segments, Sub(segment) Assert.False(String.IsNullOrWhiteSpace(segment)))
        End Sub
    End Class
End Namespace
