Imports System.Threading
Imports System.Threading.Tasks
Imports VisualLLM.Inference.Models

Namespace Services
    Public Interface IInferenceEngine
        ReadOnly Property BackendName As String

        Function CompleteAsync(
            request As ChatCompletionRequest,
            settings As InferenceRuntimeSettings,
            cancellationToken As CancellationToken) As Task(Of CompletionResult)

        Function SplitForStreaming(content As String) As IReadOnlyList(Of String)
    End Interface
End Namespace
