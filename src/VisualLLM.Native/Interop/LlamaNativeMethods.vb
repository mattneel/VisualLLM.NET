Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports System.Runtime.InteropServices

Namespace Interop
    Public NotInheritable Class LlamaNativeMethods
        Private Shared ReadOnly ResolverLock As New Object()
        Private Shared ReadOnly LoadedLibraries As New Dictionary(Of String, IntPtr)(StringComparer.OrdinalIgnoreCase)
        Private Shared _configuredLibraryPath As String = String.Empty
        Private Shared _configuredLibraryDirectory As String = String.Empty
        Private Shared _resolverInitialized As Boolean

        Private Sub New()
        End Sub

        Public Shared Sub ConfigureLibrary(libraryPath As String)
            If String.IsNullOrWhiteSpace(libraryPath) Then
                Throw New ArgumentException("A llama native library path is required.", NameOf(libraryPath))
            End If

            SyncLock ResolverLock
                _configuredLibraryPath = IO.Path.GetFullPath(libraryPath)
                _configuredLibraryDirectory = IO.Path.GetDirectoryName(_configuredLibraryPath)

                PreloadSidecarLibraries(_configuredLibraryDirectory)
                EnsureLibraryLoaded(_configuredLibraryPath)

                If Not _resolverInitialized Then
                    NativeLibrary.SetDllImportResolver(GetType(LlamaNativeMethods).Assembly, AddressOf ResolveLibraryImport)
                    _resolverInitialized = True
                End If
            End SyncLock
        End Sub

        Private Shared Function ResolveLibraryImport(libraryName As String, assembly As Assembly, searchPath As DllImportSearchPath?) As IntPtr
            If String.Equals(libraryName, "llama", StringComparison.OrdinalIgnoreCase) AndAlso
                Not String.IsNullOrWhiteSpace(_configuredLibraryPath) Then

                Return EnsureLibraryLoaded(_configuredLibraryPath)
            End If

            If Not String.IsNullOrWhiteSpace(_configuredLibraryDirectory) Then
                For Each candidateName In GetCandidateLibraryNames(libraryName)
                    Dim candidatePath = Path.Combine(_configuredLibraryDirectory, candidateName)
                    If File.Exists(candidatePath) Then
                        Return EnsureLibraryLoaded(candidatePath)
                    End If
                Next
            End If

            Return IntPtr.Zero
        End Function

        Private Shared Sub PreloadSidecarLibraries(directoryPath As String)
            If String.IsNullOrWhiteSpace(directoryPath) OrElse Not Directory.Exists(directoryPath) Then
                Return
            End If

            For Each candidatePath In GetSidecarLoadOrder(directoryPath)
                EnsureLibraryLoaded(candidatePath)
            Next
        End Sub

        Private Shared Function EnsureLibraryLoaded(libraryPath As String) As IntPtr
            Dim normalizedPath = IO.Path.GetFullPath(libraryPath)
            Dim handle As IntPtr = IntPtr.Zero

            If LoadedLibraries.TryGetValue(normalizedPath, handle) Then
                Return handle
            End If

            handle = NativeLibrary.Load(normalizedPath)
            LoadedLibraries(normalizedPath) = handle
            Return handle
        End Function

        Private Shared Iterator Function GetSidecarLoadOrder(directoryPath As String) As IEnumerable(Of String)
            For Each libraryName In GetOrderedDependencyNames()
                Dim candidatePath = Path.Combine(directoryPath, libraryName)
                If File.Exists(candidatePath) Then
                    Yield candidatePath
                End If
            Next

            Dim llamaLibraryPath = Path.Combine(directoryPath, GetPrimaryLibraryFileName())
            If File.Exists(llamaLibraryPath) Then
                Yield llamaLibraryPath
            End If
        End Function

        Private Shared Function GetOrderedDependencyNames() As IReadOnlyList(Of String)
            If RuntimeInformation.IsOSPlatform(OSPlatform.Windows) Then
                Return {
                    "ggml-base.dll",
                    "ggml-cpu.dll",
                    "ggml.dll",
                    "mtmd.dll"
                }
            End If

            If RuntimeInformation.IsOSPlatform(OSPlatform.OSX) Then
                Return {
                    "libggml-base.dylib",
                    "libggml-cpu.dylib",
                    "libggml.dylib",
                    "libmtmd.dylib"
                }
            End If

            Return {
                "libggml-base.so",
                "libggml-cpu.so",
                "libggml.so",
                "libmtmd.so"
            }
        End Function

        Private Shared Function GetCandidateLibraryNames(libraryName As String) As IEnumerable(Of String)
            If String.IsNullOrWhiteSpace(libraryName) Then
                Return Enumerable.Empty(Of String)()
            End If

            If Path.HasExtension(libraryName) Then
                Return {libraryName}
            End If

            If RuntimeInformation.IsOSPlatform(OSPlatform.Windows) Then
                Return {libraryName & ".dll"}
            End If

            If RuntimeInformation.IsOSPlatform(OSPlatform.OSX) Then
                Return {
                    "lib" & libraryName & ".dylib",
                    libraryName & ".dylib"
                }
            End If

            Return {
                "lib" & libraryName & ".so",
                libraryName & ".so"
            }
        End Function

        Private Shared Function GetPrimaryLibraryFileName() As String
            If RuntimeInformation.IsOSPlatform(OSPlatform.Windows) Then
                Return "llama.dll"
            End If

            If RuntimeInformation.IsOSPlatform(OSPlatform.OSX) Then
                Return "libllama.dylib"
            End If

            Return "libllama.so"
        End Function

        <StructLayout(LayoutKind.Sequential)>
        Public Structure llama_model_params
            Public devices As IntPtr
            Public tensor_buft_overrides As IntPtr
            Public n_gpu_layers As Integer
            Public split_mode As Integer
            Public main_gpu As Integer
            Public tensor_split As IntPtr
            Public progress_callback As IntPtr
            Public progress_callback_user_data As IntPtr
            Public kv_overrides As IntPtr
            <MarshalAs(UnmanagedType.I1)>
            Public vocab_only As Boolean
            <MarshalAs(UnmanagedType.I1)>
            Public use_mmap As Boolean
            <MarshalAs(UnmanagedType.I1)>
            Public use_direct_io As Boolean
            <MarshalAs(UnmanagedType.I1)>
            Public use_mlock As Boolean
            <MarshalAs(UnmanagedType.I1)>
            Public check_tensors As Boolean
            <MarshalAs(UnmanagedType.I1)>
            Public use_extra_bufts As Boolean
            <MarshalAs(UnmanagedType.I1)>
            Public no_host As Boolean
            <MarshalAs(UnmanagedType.I1)>
            Public no_alloc As Boolean
        End Structure

        <StructLayout(LayoutKind.Sequential)>
        Public Structure llama_context_params
            Public n_ctx As UInteger
            Public n_batch As UInteger
            Public n_ubatch As UInteger
            Public n_seq_max As UInteger
            Public n_threads As Integer
            Public n_threads_batch As Integer
            Public rope_scaling_type As Integer
            Public pooling_type As Integer
            Public attention_type As Integer
            Public flash_attn_type As Integer
            Public rope_freq_base As Single
            Public rope_freq_scale As Single
            Public yarn_ext_factor As Single
            Public yarn_attn_factor As Single
            Public yarn_beta_fast As Single
            Public yarn_beta_slow As Single
            Public yarn_orig_ctx As UInteger
            Public defrag_thold As Single
            Public cb_eval As IntPtr
            Public cb_eval_user_data As IntPtr
            Public type_k As Integer
            Public type_v As Integer
            Public abort_callback As IntPtr
            Public abort_callback_data As IntPtr
            <MarshalAs(UnmanagedType.I1)>
            Public embeddings As Boolean
            <MarshalAs(UnmanagedType.I1)>
            Public offload_kqv As Boolean
            <MarshalAs(UnmanagedType.I1)>
            Public no_perf As Boolean
            <MarshalAs(UnmanagedType.I1)>
            Public op_offload As Boolean
            <MarshalAs(UnmanagedType.I1)>
            Public swa_full As Boolean
            <MarshalAs(UnmanagedType.I1)>
            Public kv_unified As Boolean
            Public samplers As IntPtr
            Public n_samplers As UIntPtr
        End Structure

        <StructLayout(LayoutKind.Sequential)>
        Public Structure llama_sampler_chain_params
            <MarshalAs(UnmanagedType.I1)>
            Public no_perf As Boolean
        End Structure

        <StructLayout(LayoutKind.Sequential)>
        Public Structure llama_chat_message
            Public role As IntPtr
            Public content As IntPtr
        End Structure

        <StructLayout(LayoutKind.Sequential)>
        Public Structure llama_batch
            Public n_tokens As Integer
            Public token As IntPtr
            Public embd As IntPtr
            Public pos As IntPtr
            Public n_seq_id As IntPtr
            Public seq_id As IntPtr
            Public logits As IntPtr
        End Structure

        <DllImport("llama", CallingConvention:=CallingConvention.Cdecl)>
        Public Shared Sub llama_backend_init()
        End Sub

        <DllImport("llama", CallingConvention:=CallingConvention.Cdecl)>
        Public Shared Sub llama_backend_free()
        End Sub

        <DllImport("llama", CallingConvention:=CallingConvention.Cdecl)>
        Public Shared Function llama_model_default_params() As llama_model_params
        End Function

        <DllImport("llama", CallingConvention:=CallingConvention.Cdecl)>
        Public Shared Function llama_context_default_params() As llama_context_params
        End Function

        <DllImport("llama", CallingConvention:=CallingConvention.Cdecl)>
        Public Shared Function llama_sampler_chain_default_params() As llama_sampler_chain_params
        End Function

        <DllImport("llama", CallingConvention:=CallingConvention.Cdecl)>
        Public Shared Function llama_model_load_from_file(path_model As IntPtr, parameters As llama_model_params) As IntPtr
        End Function

        <DllImport("llama", CallingConvention:=CallingConvention.Cdecl)>
        Public Shared Sub llama_model_free(model As IntPtr)
        End Sub

        <DllImport("llama", CallingConvention:=CallingConvention.Cdecl)>
        Public Shared Function llama_init_from_model(model As IntPtr, parameters As llama_context_params) As IntPtr
        End Function

        <DllImport("llama", CallingConvention:=CallingConvention.Cdecl)>
        Public Shared Sub llama_free(context As IntPtr)
        End Sub

        <DllImport("llama", CallingConvention:=CallingConvention.Cdecl)>
        Public Shared Function llama_model_get_vocab(model As IntPtr) As IntPtr
        End Function

        <DllImport("llama", CallingConvention:=CallingConvention.Cdecl)>
        Public Shared Function llama_model_chat_template(model As IntPtr, name As IntPtr) As IntPtr
        End Function

        <DllImport("llama", CallingConvention:=CallingConvention.Cdecl)>
        Public Shared Function llama_model_has_encoder(model As IntPtr) As <MarshalAs(UnmanagedType.I1)> Boolean
        End Function

        <DllImport("llama", CallingConvention:=CallingConvention.Cdecl)>
        Public Shared Function llama_model_decoder_start_token(model As IntPtr) As Integer
        End Function

        <DllImport("llama", CallingConvention:=CallingConvention.Cdecl)>
        Public Shared Function llama_get_memory(context As IntPtr) As IntPtr
        End Function

        <DllImport("llama", CallingConvention:=CallingConvention.Cdecl)>
        Public Shared Function llama_memory_seq_pos_max(memory As IntPtr, seqId As Integer) As Integer
        End Function

        <DllImport("llama", CallingConvention:=CallingConvention.Cdecl)>
        Public Shared Sub llama_memory_clear(memory As IntPtr, <MarshalAs(UnmanagedType.I1)> clearData As Boolean)
        End Sub

        <DllImport("llama", CallingConvention:=CallingConvention.Cdecl)>
        Public Shared Function llama_n_ctx(context As IntPtr) As UInteger
        End Function

        <DllImport("llama", CallingConvention:=CallingConvention.Cdecl)>
        Public Shared Function llama_chat_apply_template(
            template As IntPtr,
            messages As llama_chat_message(),
            messageCount As UIntPtr,
            <MarshalAs(UnmanagedType.I1)> addAssistant As Boolean,
            buffer As Byte(),
            length As Integer) As Integer
        End Function

        <DllImport("llama", CallingConvention:=CallingConvention.Cdecl)>
        Public Shared Function llama_tokenize(
            vocab As IntPtr,
            text As IntPtr,
            textLength As Integer,
            tokens As Integer(),
            maximumTokens As Integer,
            <MarshalAs(UnmanagedType.I1)> addSpecial As Boolean,
            <MarshalAs(UnmanagedType.I1)> parseSpecial As Boolean) As Integer
        End Function

        <DllImport("llama", CallingConvention:=CallingConvention.Cdecl)>
        Public Shared Function llama_vocab_bos(vocab As IntPtr) As Integer
        End Function

        <DllImport("llama", CallingConvention:=CallingConvention.Cdecl)>
        Public Shared Function llama_vocab_is_eog(vocab As IntPtr, token As Integer) As <MarshalAs(UnmanagedType.I1)> Boolean
        End Function

        <DllImport("llama", CallingConvention:=CallingConvention.Cdecl)>
        Public Shared Function llama_token_to_piece(
            vocab As IntPtr,
            token As Integer,
            buffer As Byte(),
            length As Integer,
            lstrip As Integer,
            <MarshalAs(UnmanagedType.I1)> special As Boolean) As Integer
        End Function

        <DllImport("llama", CallingConvention:=CallingConvention.Cdecl)>
        Public Shared Function llama_batch_get_one(tokens As IntPtr, tokenCount As Integer) As llama_batch
        End Function

        <DllImport("llama", CallingConvention:=CallingConvention.Cdecl)>
        Public Shared Function llama_encode(context As IntPtr, batch As llama_batch) As Integer
        End Function

        <DllImport("llama", CallingConvention:=CallingConvention.Cdecl)>
        Public Shared Function llama_decode(context As IntPtr, batch As llama_batch) As Integer
        End Function

        <DllImport("llama", CallingConvention:=CallingConvention.Cdecl)>
        Public Shared Function llama_sampler_chain_init(parameters As llama_sampler_chain_params) As IntPtr
        End Function

        <DllImport("llama", CallingConvention:=CallingConvention.Cdecl)>
        Public Shared Sub llama_sampler_chain_add(chain As IntPtr, sampler As IntPtr)
        End Sub

        <DllImport("llama", CallingConvention:=CallingConvention.Cdecl)>
        Public Shared Function llama_sampler_init_greedy() As IntPtr
        End Function

        <DllImport("llama", CallingConvention:=CallingConvention.Cdecl)>
        Public Shared Function llama_sampler_init_dist(seed As UInteger) As IntPtr
        End Function

        <DllImport("llama", CallingConvention:=CallingConvention.Cdecl)>
        Public Shared Function llama_sampler_init_min_p(probability As Single, minimumKeep As UIntPtr) As IntPtr
        End Function

        <DllImport("llama", CallingConvention:=CallingConvention.Cdecl)>
        Public Shared Function llama_sampler_init_temp(temperature As Single) As IntPtr
        End Function

        <DllImport("llama", CallingConvention:=CallingConvention.Cdecl)>
        Public Shared Function llama_sampler_sample(sampler As IntPtr, context As IntPtr, index As Integer) As Integer
        End Function

        <DllImport("llama", CallingConvention:=CallingConvention.Cdecl)>
        Public Shared Sub llama_sampler_reset(sampler As IntPtr)
        End Sub

        <DllImport("llama", CallingConvention:=CallingConvention.Cdecl)>
        Public Shared Sub llama_sampler_free(sampler As IntPtr)
        End Sub
    End Class
End Namespace
