Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Threading
Imports System.Threading.Tasks
Imports VisualLLM.Inference.Models
Imports VisualLLM.Inference.Services
Imports VisualLLM.Native.Interop

Namespace Runtime
    Public Class NativeLlamaRuntime
        Implements IDisposable

        Private Shared ReadOnly BackendLock As New Object()
        Private Shared _backendInitialized As Boolean

        Private ReadOnly _options As NativeRuntimeOptions
        Private ReadOnly _settings As InferenceRuntimeSettings
        Private ReadOnly _promptComposer As PromptComposer
        Private ReadOnly _gate As New SemaphoreSlim(1, 1)
        Private _model As IntPtr
        Private _context As IntPtr
        Private _vocab As IntPtr
        Private _isInitialized As Boolean
        Private _disposed As Boolean

        Public Sub New(options As NativeRuntimeOptions, settings As InferenceRuntimeSettings, promptComposer As PromptComposer)
            _options = options
            _settings = settings
            _promptComposer = promptComposer
        End Sub

        Public ReadOnly Property IsInitialized As Boolean
            Get
                Return _isInitialized
            End Get
        End Property

        Public Sub Initialize()
            ThrowIfDisposed()

            If _isInitialized Then
                Return
            End If

            If String.IsNullOrWhiteSpace(_options.LibraryPath) Then
                Throw New InvalidOperationException("A resolved llama native library path is required.")
            End If

            If String.IsNullOrWhiteSpace(_options.ModelPath) OrElse Not File.Exists(_options.ModelPath) Then
                Throw New FileNotFoundException("The configured GGUF model file was not found.", _options.ModelPath)
            End If

            LlamaNativeMethods.ConfigureLibrary(_options.LibraryPath)
            EnsureBackendInitialized()

            Dim modelParams = LlamaNativeMethods.llama_model_default_params()
            modelParams.n_gpu_layers = _options.GpuLayers
            modelParams.use_mmap = True
            modelParams.check_tensors = False

            Dim modelPathPointer = Marshal.StringToCoTaskMemUTF8(_options.ModelPath)

            Try
                _model = LlamaNativeMethods.llama_model_load_from_file(modelPathPointer, modelParams)
            Finally
                Marshal.FreeCoTaskMem(modelPathPointer)
            End Try

            If _model = IntPtr.Zero Then
                Throw New InvalidOperationException("llama_model_load_from_file returned null.")
            End If

            Dim contextParams = LlamaNativeMethods.llama_context_default_params()
            contextParams.n_ctx = CUInt(_settings.ContextLength)
            contextParams.n_batch = CUInt(Math.Max(1, _settings.ContextLength))
            contextParams.n_ubatch = CUInt(Math.Max(1, Math.Min(_settings.ContextLength, 512)))
            contextParams.n_seq_max = 1UI
            contextParams.n_threads = Math.Max(1, _settings.ThreadCount)
            contextParams.n_threads_batch = Math.Max(1, _settings.ThreadCount)
            contextParams.no_perf = True
            contextParams.offload_kqv = (_options.GpuLayers <> 0)

            _context = LlamaNativeMethods.llama_init_from_model(_model, contextParams)
            If _context = IntPtr.Zero Then
                Throw New InvalidOperationException("llama_init_from_model returned null.")
            End If

            _vocab = LlamaNativeMethods.llama_model_get_vocab(_model)
            If _vocab = IntPtr.Zero Then
                Throw New InvalidOperationException("llama_model_get_vocab returned null.")
            End If

            _isInitialized = True
        End Sub

        Public Async Function CompleteChatAsync(
            request As ChatCompletionRequest,
            cancellationToken As CancellationToken,
            Optional onToken As Func(Of String, CancellationToken, Task) = Nothing) As Task(Of CompletionResult)

            ThrowIfDisposed()
            Initialize()

            Await _gate.WaitAsync(cancellationToken)

            Try
                ResetState()

                Dim prompt = FormatPrompt(request.Messages)
                Dim promptTokens = Tokenize(prompt, True, True)
                Dim maxCompletionTokens = ResolveMaxCompletionTokens(promptTokens.Length, request.MaxTokens)
                Dim completionBuilder As New StringBuilder()
                Dim generatedTokenCount = 0

                If LlamaNativeMethods.llama_model_has_encoder(_model) Then
                    EvaluateTokens(promptTokens, True)

                    Dim decoderStartToken = LlamaNativeMethods.llama_model_decoder_start_token(_model)
                    If decoderStartToken = -1 Then
                        decoderStartToken = LlamaNativeMethods.llama_vocab_bos(_vocab)
                    End If

                    EvaluateTokens({decoderStartToken}, False)
                Else
                    EvaluateTokens(promptTokens, False)
                End If

                Using sampler = New SamplerHandle(CreateSampler(request.Temperature))
                    While generatedTokenCount < maxCompletionTokens
                        cancellationToken.ThrowIfCancellationRequested()

                        Dim contextUsed = LlamaNativeMethods.llama_memory_seq_pos_max(LlamaNativeMethods.llama_get_memory(_context), 0) + 1
                        Dim contextLimit = CInt(LlamaNativeMethods.llama_n_ctx(_context))
                        If contextUsed >= contextLimit Then
                            Exit While
                        End If

                        Dim token = LlamaNativeMethods.llama_sampler_sample(sampler.Pointer, _context, -1)
                        If LlamaNativeMethods.llama_vocab_is_eog(_vocab, token) Then
                            Exit While
                        End If

                        Dim piece = TokenToPiece(token)
                        completionBuilder.Append(piece)
                        generatedTokenCount += 1

                        If onToken IsNot Nothing Then
                            Await onToken(piece, cancellationToken)
                        End If

                        EvaluateTokens({token}, False)
                    End While
                End Using

                Return New CompletionResult With {
                    .Content = completionBuilder.ToString(),
                    .FinishReason = "stop",
                    .PromptTokens = promptTokens.Length,
                    .CompletionTokens = generatedTokenCount
                }
            Finally
                _gate.Release()
            End Try
        End Function

        Public Sub Dispose() Implements IDisposable.Dispose
            If _disposed Then
                Return
            End If

            _disposed = True

            If _context <> IntPtr.Zero Then
                LlamaNativeMethods.llama_free(_context)
                _context = IntPtr.Zero
            End If

            If _model <> IntPtr.Zero Then
                LlamaNativeMethods.llama_model_free(_model)
                _model = IntPtr.Zero
            End If

            _gate.Dispose()
        End Sub

        Private Shared Sub EnsureBackendInitialized()
            SyncLock BackendLock
                If Not _backendInitialized Then
                    LlamaNativeMethods.llama_backend_init()
                    _backendInitialized = True
                End If
            End SyncLock
        End Sub

        Private Function FormatPrompt(messages As IReadOnlyList(Of ChatMessage)) As String
            Dim templatePointer = LlamaNativeMethods.llama_model_chat_template(_model, IntPtr.Zero)
            If templatePointer = IntPtr.Zero Then
                Return _promptComposer.Compose(messages)
            End If

            Dim templateText = Marshal.PtrToStringUTF8(templatePointer)
            If String.IsNullOrWhiteSpace(templateText) Then
                Return _promptComposer.Compose(messages)
            End If

            Dim nativeMessages As New List(Of LlamaNativeMethods.llama_chat_message)()
            Dim allocatedPointers As New List(Of IntPtr)()

            Try
                For Each message In messages
                    Dim rolePointer = Marshal.StringToCoTaskMemUTF8(If(message.Role, "user"))
                    Dim contentPointer = Marshal.StringToCoTaskMemUTF8(If(message.Content, String.Empty))
                    allocatedPointers.Add(rolePointer)
                    allocatedPointers.Add(contentPointer)

                    nativeMessages.Add(New LlamaNativeMethods.llama_chat_message With {
                        .role = rolePointer,
                        .content = contentPointer
                    })
                Next

                Dim requiredLength = LlamaNativeMethods.llama_chat_apply_template(
                    templatePointer,
                    nativeMessages.ToArray(),
                    CType(nativeMessages.Count, UIntPtr),
                    True,
                    Nothing,
                    0)

                If requiredLength <= 0 Then
                    Return _promptComposer.Compose(messages)
                End If

                Dim buffer(requiredLength) As Byte
                Dim actualLength = LlamaNativeMethods.llama_chat_apply_template(
                    templatePointer,
                    nativeMessages.ToArray(),
                    CType(nativeMessages.Count, UIntPtr),
                    True,
                    buffer,
                    buffer.Length)

                If actualLength <= 0 Then
                    Return _promptComposer.Compose(messages)
                End If

                Return Encoding.UTF8.GetString(buffer, 0, actualLength)
            Finally
                For Each pointer In allocatedPointers
                    If pointer <> IntPtr.Zero Then
                        Marshal.FreeCoTaskMem(pointer)
                    End If
                Next
            End Try
        End Function

        Private Function Tokenize(text As String, addSpecial As Boolean, parseSpecial As Boolean) As Integer()
            Dim textPointer = Marshal.StringToCoTaskMemUTF8(text)

            Try
                Dim textLength = Encoding.UTF8.GetByteCount(text)
                Dim requiredTokenCount = -LlamaNativeMethods.llama_tokenize(_vocab, textPointer, textLength, Nothing, 0, addSpecial, parseSpecial)
                If requiredTokenCount <= 0 Then
                    Throw New InvalidOperationException("llama_tokenize returned an invalid token count.")
                End If

                Dim tokens(requiredTokenCount - 1) As Integer
                Dim actualCount = LlamaNativeMethods.llama_tokenize(_vocab, textPointer, textLength, tokens, tokens.Length, addSpecial, parseSpecial)
                If actualCount < 0 Then
                    Throw New InvalidOperationException("llama_tokenize failed for the current prompt.")
                End If

                If actualCount = tokens.Length Then
                    Return tokens
                End If

                Dim resized(actualCount - 1) As Integer
                Array.Copy(tokens, resized, actualCount)
                Return resized
            Finally
                Marshal.FreeCoTaskMem(textPointer)
            End Try
        End Function

        Private Function ResolveMaxCompletionTokens(promptTokenCount As Integer, requestedMaxTokens As Integer?) As Integer
            Dim requested = If(requestedMaxTokens, _settings.DefaultMaxTokens)
            Dim available = Math.Max(1, _settings.ContextLength - promptTokenCount - 1)
            Return Math.Max(1, Math.Min(requested, available))
        End Function

        Private Sub EvaluateTokens(tokens As Integer(), useEncoder As Boolean)
            Dim tokenBuffer = Marshal.AllocHGlobal(tokens.Length * Marshal.SizeOf(Of Integer)())

            Try
                Marshal.Copy(tokens, 0, tokenBuffer, tokens.Length)

                Dim batch = LlamaNativeMethods.llama_batch_get_one(tokenBuffer, tokens.Length)
                Dim result = If(useEncoder, LlamaNativeMethods.llama_encode(_context, batch), LlamaNativeMethods.llama_decode(_context, batch))

                If result <> 0 Then
                    Throw New InvalidOperationException($"llama evaluation failed with code {result}.")
                End If
            Finally
                Marshal.FreeHGlobal(tokenBuffer)
            End Try
        End Sub

        Private Function TokenToPiece(token As Integer) As String
            Dim buffer(1023) As Byte
            Dim pieceLength = LlamaNativeMethods.llama_token_to_piece(_vocab, token, buffer, buffer.Length, 0, True)

            If pieceLength <= 0 Then
                Throw New InvalidOperationException("llama_token_to_piece failed.")
            End If

            Return Encoding.UTF8.GetString(buffer, 0, pieceLength)
        End Function

        Private Sub ResetState()
            Dim memory = LlamaNativeMethods.llama_get_memory(_context)
            LlamaNativeMethods.llama_memory_clear(memory, False)
        End Sub

        Private Function CreateSampler(requestTemperature As Double?) As IntPtr
            Dim temperature = CSng(If(requestTemperature, _settings.DefaultTemperature))
            Dim parameters = LlamaNativeMethods.llama_sampler_chain_default_params()
            parameters.no_perf = True

            Dim sampler = LlamaNativeMethods.llama_sampler_chain_init(parameters)
            If sampler = IntPtr.Zero Then
                Throw New InvalidOperationException("llama_sampler_chain_init returned null.")
            End If

            If temperature <= 0.0F Then
                LlamaNativeMethods.llama_sampler_chain_add(sampler, LlamaNativeMethods.llama_sampler_init_greedy())
            Else
                LlamaNativeMethods.llama_sampler_chain_add(sampler, LlamaNativeMethods.llama_sampler_init_min_p(0.05F, CType(1, UIntPtr)))
                LlamaNativeMethods.llama_sampler_chain_add(sampler, LlamaNativeMethods.llama_sampler_init_temp(temperature))
                LlamaNativeMethods.llama_sampler_chain_add(sampler, LlamaNativeMethods.llama_sampler_init_dist(&HFFFFFFFFUI))
            End If

            LlamaNativeMethods.llama_sampler_reset(sampler)
            Return sampler
        End Function

        Private Sub ThrowIfDisposed()
            If _disposed Then
                Throw New ObjectDisposedException(NameOf(NativeLlamaRuntime))
            End If
        End Sub

        Private NotInheritable Class SamplerHandle
            Implements IDisposable

            Public Sub New(pointer As IntPtr)
                Me.Pointer = pointer
            End Sub

            Public ReadOnly Property Pointer As IntPtr

            Public Sub Dispose() Implements IDisposable.Dispose
                If Pointer <> IntPtr.Zero Then
                    LlamaNativeMethods.llama_sampler_free(Pointer)
                End If
            End Sub
        End Class
    End Class
End Namespace
