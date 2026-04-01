Imports System.Threading
Imports System.Reflection
Imports System.Text.Json.Serialization
Imports Microsoft.AspNetCore.Builder
Imports Microsoft.AspNetCore.Hosting
Imports Microsoft.AspNetCore.Http
Imports Microsoft.Extensions.DependencyInjection
Imports VisualLLM.Inference.Models
Imports VisualLLM.Inference.Services
Imports VisualLLM.Native.Runtime
Imports VisualLLM.Server.Api

Namespace Hosting
    Public NotInheritable Class ServerBootstrapper
        Private Sub New()
        End Sub

        Public Shared Function BuildApplication(arguments As String(), Optional suppliedOptions As ServerOptions = Nothing) As WebApplication
            Dim options = If(suppliedOptions, ServerOptions.Parse(arguments))
            Dim builder = WebApplication.CreateBuilder(arguments)

            builder.WebHost.UseUrls($"http://0.0.0.0:{options.ListenPort}")
            builder.Services.ConfigureHttpJsonOptions(
                Sub(configuration)
                    configuration.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                End Sub)

            RegisterServices(builder.Services, options)

            Dim application = builder.Build()
            MapEndpoints(application)

            If options.ShowBanner Then
                WriteBanner(
                    options,
                    application.Services.GetRequiredService(Of NativeRuntimeStatus)(),
                    application.Services.GetRequiredService(Of IChatCompletionBackend)())
            End If

            Return application
        End Function

        Private Shared Sub RegisterServices(services As IServiceCollection, options As ServerOptions)
            Dim nativeOptions = options.ToNativeRuntimeOptions()
            Dim nativeStatus = NativeRuntimeInspector.Inspect(nativeOptions)
            Dim runtimeSettings = options.ToInferenceRuntimeSettings("llama.cpp-pinvoke")
            Dim backend = CreateBackend(options, nativeStatus, runtimeSettings)

            services.AddSingleton(options)
            services.AddSingleton(nativeOptions)
            services.AddSingleton(nativeStatus)
            services.AddSingleton(runtimeSettings)
            services.AddSingleton(Of IChatCompletionBackend)(backend)
        End Sub

        Private Shared Function CreateBackend(
            options As ServerOptions,
            nativeStatus As NativeRuntimeStatus,
            runtimeSettings As InferenceRuntimeSettings) As IChatCompletionBackend

            If String.Equals(options.BackendPreference, "demo", StringComparison.OrdinalIgnoreCase) Then
                Return New DemoChatCompletionBackend(New DeterministicInferenceEngine(New PromptComposer()), runtimeSettings)
            End If

            If String.IsNullOrWhiteSpace(options.ModelPath) Then
                Throw New InvalidOperationException("A GGUF model path is required for the real backend. Use --model or VISUALLLM_MODEL_PATH.")
            End If

            If Not nativeStatus.HasLibrary Then
                Throw New InvalidOperationException(
                    "No portable llama native library was found. Build it into runtimes/<rid>/native, add it to PATH, or set VISUALLLM_NATIVE_LIBRARY.")
            End If

            Dim runtimeOptions = options.ToNativeRuntimeOptions()
            runtimeOptions.LibraryPath = nativeStatus.ResolvedLibraryPath
            runtimeOptions.ServerExecutablePath = nativeStatus.ResolvedServerExecutablePath

            Dim promptComposer As New PromptComposer()
            Dim nativeRuntime As New NativeLlamaRuntime(runtimeOptions, runtimeSettings, promptComposer)
            nativeRuntime.Initialize()

            Return New NativeChatCompletionBackend(nativeRuntime, runtimeSettings, nativeStatus)
        End Function

        Private Shared Sub MapEndpoints(application As WebApplication)
            application.MapGet(
                "/",
                Async Function(runtimeSettings As InferenceRuntimeSettings, nativeStatus As NativeRuntimeStatus, backend As IChatCompletionBackend, cancellationToken As CancellationToken)
                    Dim health = Await backend.GetHealthSnapshotAsync(cancellationToken)
                    Return New With {
                        .name = "VisualLLM.NET",
                        .version = GetProductVersion(),
                        .status = If(health.IsReady, "ready", "degraded"),
                        .model = runtimeSettings.ModelIdentifier,
                        .backend = backend.BackendName,
                        .native_runtime_loaded = nativeStatus.IsAvailable,
                        .native_library = nativeStatus.ResolvedLibraryPath
                    }
                End Function)

            application.MapGet(
                "/healthz",
                Async Function(options As ServerOptions, runtimeSettings As InferenceRuntimeSettings, nativeStatus As NativeRuntimeStatus, backend As IChatCompletionBackend, cancellationToken As CancellationToken)
                    Dim health = Await backend.GetHealthSnapshotAsync(cancellationToken)
                    Return New With {
                        .version = GetProductVersion(),
                        .status = If(health.IsReady, "ok", "degraded"),
                        .model = runtimeSettings.ModelIdentifier,
                        .model_path = options.ModelPath,
                        .backend = backend.BackendName,
                        .native_runtime_loaded = nativeStatus.IsAvailable,
                        .native_library = nativeStatus.ResolvedLibraryPath,
                        .llama_server = nativeStatus.ResolvedServerExecutablePath,
                        .runtime_identifier = nativeStatus.RuntimeIdentifier,
                        .message = health.Message
                    }
                End Function)

            application.MapGet(
                "/v1/models",
                Async Function(backend As IChatCompletionBackend, cancellationToken As CancellationToken)
                    Return Await ChatEndpoints.GetModelsAsync(backend, cancellationToken)
                End Function)

            application.MapPost(
                "/v1/chat/completions",
                Function(
                    request As ChatCompletionRequest,
                    httpContext As HttpContext,
                    runtimeSettings As InferenceRuntimeSettings,
                    backend As IChatCompletionBackend,
                    cancellationToken As CancellationToken)
                    Return ChatEndpoints.PostChatCompletionsAsync(request, httpContext, runtimeSettings, backend, cancellationToken)
                End Function)
        End Sub

        Private Shared Sub WriteBanner(options As ServerOptions, nativeStatus As NativeRuntimeStatus, backend As IChatCompletionBackend)
            Console.WriteLine("+--------------------------------------------------+")
            Console.WriteLine($"| VisualLLM.NET v{GetProductVersion(),-31}|")
            Console.WriteLine("| OpenAI-compatible inference for .NET 10 / VB.NET |")
            Console.WriteLine("+--------------------------------------------------+")
            Console.WriteLine($"Model:    {options.ModelIdentifier}")
            Console.WriteLine($"Port:     {options.ListenPort}")
            Console.WriteLine($"Backend:  {backend.BackendName}")
            Console.WriteLine($"Runtime:  {If(nativeStatus.HasLibrary, nativeStatus.ResolvedLibraryPath, "not found")}")
            Console.WriteLine()
        End Sub

        Private Shared Function GetProductVersion() As String
            Dim informationalVersion = GetType(ServerBootstrapper).Assembly.GetCustomAttribute(Of AssemblyInformationalVersionAttribute)()
            If informationalVersion IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(informationalVersion.InformationalVersion) Then
                Return informationalVersion.InformationalVersion.Split("+"c)(0)
            End If

            Dim assemblyVersion = GetType(ServerBootstrapper).Assembly.GetName().Version
            If assemblyVersion Is Nothing Then
                Return "0.0.0"
            End If

            Return $"{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}"
        End Function
    End Class
End Namespace
