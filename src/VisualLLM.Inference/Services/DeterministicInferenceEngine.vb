Imports System.Threading
Imports System.Threading.Tasks
Imports System.Text
Imports System.Text.RegularExpressions
Imports VisualLLM.Inference.Models

Namespace Services
    Public Class DeterministicInferenceEngine
        Implements IInferenceEngine

        Private ReadOnly _promptComposer As PromptComposer

        Public Sub New(promptComposer As PromptComposer)
            _promptComposer = promptComposer
        End Sub

        Public ReadOnly Property BackendName As String Implements IInferenceEngine.BackendName
            Get
                Return "deterministic-enterprise-demo"
            End Get
        End Property

        Public Function CompleteAsync(
            request As ChatCompletionRequest,
            settings As InferenceRuntimeSettings,
            cancellationToken As CancellationToken) As Task(Of CompletionResult) Implements IInferenceEngine.CompleteAsync

            Dim prompt = _promptComposer.Compose(request.Messages)
            Dim latestUserMessage = _promptComposer.GetLatestUserMessage(request.Messages)
            Dim responseText = BuildResponse(latestUserMessage, settings, request)

            Dim result As New CompletionResult With {
                .Content = responseText,
                .FinishReason = "stop",
                .PromptTokens = TokenEstimator.Estimate(prompt),
                .CompletionTokens = TokenEstimator.Estimate(responseText)
            }

            Return Task.FromResult(result)
        End Function

        Public Function SplitForStreaming(content As String) As IReadOnlyList(Of String) Implements IInferenceEngine.SplitForStreaming
            Dim segments As New List(Of String)()

            If String.IsNullOrWhiteSpace(content) Then
                Return segments
            End If

            Dim words = content.Split({" "c}, StringSplitOptions.RemoveEmptyEntries)
            Dim builder As New StringBuilder()

            For Each word In words
                Dim projectedLength = builder.Length + word.Length
                If builder.Length > 0 Then
                    projectedLength += 1
                End If

                If projectedLength > 30 AndAlso builder.Length > 0 Then
                    segments.Add(builder.ToString() & " ")
                    builder.Clear()
                End If

                If builder.Length > 0 Then
                    builder.Append(" ")
                End If

                builder.Append(word)
            Next

            If builder.Length > 0 Then
                segments.Add(builder.ToString())
            End If

            Return segments
        End Function

        Private Shared Function BuildResponse(
            latestUserMessage As String,
            settings As InferenceRuntimeSettings,
            request As ChatCompletionRequest) As String

            Dim lineBreak = Environment.NewLine & Environment.NewLine
            Dim normalizedInput = NormalizeInput(latestUserMessage)
            Dim configuredTemperature = If(request.Temperature, settings.DefaultTemperature)
            Dim maxTokens = If(request.MaxTokens, settings.DefaultMaxTokens)

            If String.IsNullOrWhiteSpace(normalizedInput) Then
                Return "VisualLLM.NET is online. Provide a user message and the server will answer through the OpenAI-compatible endpoint."
            End If

            If normalizedInput.IndexOf("visual basic", StringComparison.OrdinalIgnoreCase) >= 0 Then
                Return "Visual Basic remains perfectly capable of orchestrating AI workloads when the heavy math lives behind a stable interface." &
                    lineBreak &
                    "This implementation keeps the HTTP surface, prompt plumbing, and runtime configuration in VB.NET while leaving room for the native llama.cpp execution path in VisualLLM.Native."
            End If

            If normalizedInput.IndexOf("hello", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
                normalizedInput.IndexOf("hi", StringComparison.OrdinalIgnoreCase) >= 0 Then

                Return "Hello from VisualLLM.NET." &
                    lineBreak &
                    $"The server is running with backend '{settings.BackendName}', model alias '{settings.ModelIdentifier}', and a deterministic completion path that can stream tokens over SSE."
            End If

            Return "VisualLLM.NET processed the prompt below and returned a deterministic completion so the repo is runnable without shipping a bundled model runtime." &
                lineBreak &
                $"Prompt: ""{Truncate(normalizedInput, 220)}""" &
                lineBreak &
                $"Runtime: context={settings.ContextLength}, threads={settings.ThreadCount}, temperature={configuredTemperature:0.0}, max_tokens={maxTokens}." &
                lineBreak &
                "Swap in a native llama.cpp-backed engine behind the same interface and the API contract remains unchanged."
        End Function

        Private Shared Function NormalizeInput(content As String) As String
            If String.IsNullOrWhiteSpace(content) Then
                Return String.Empty
            End If

            Return Regex.Replace(content.Trim(), "\s+", " ")
        End Function

        Private Shared Function Truncate(content As String, maxLength As Integer) As String
            If content.Length <= maxLength Then
                Return content
            End If

            Return content.Substring(0, maxLength - 3) & "..."
        End Function
    End Class
End Namespace
