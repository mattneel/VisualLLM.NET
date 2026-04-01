Imports System.Globalization
Imports System.IO
Imports VisualLLM.Inference.Services
Imports VisualLLM.Native.Runtime

Namespace Hosting
    Public Class ServerOptions
        Public Property ModelPath As String = String.Empty
        Public Property ModelAlias As String = "default"
        Public Property NativeLibraryPath As String = String.Empty
        Public Property ContextLength As Integer = 4096
        Public Property ThreadCount As Integer = Environment.ProcessorCount
        Public Property GpuLayers As Integer = 99
        Public Property Temperature As Double = 0.7
        Public Property ListenPort As Integer = 5000
        Public Property BackendPreference As String = "auto"
        Public Property ShowBanner As Boolean = True
        Public Property EnableGracefulDegradation As Boolean = True
        Public Property DefaultMaxTokens As Integer = 256

        Public ReadOnly Property ModelIdentifier As String
            Get
                If Not String.IsNullOrWhiteSpace(ModelAlias) Then
                    Return ModelAlias.Trim()
                End If

                If Not String.IsNullOrWhiteSpace(ModelPath) Then
                    Return Path.GetFileNameWithoutExtension(ModelPath.Trim())
                End If

                Return "default"
            End Get
        End Property

        Public Function ToInferenceRuntimeSettings(backendName As String) As InferenceRuntimeSettings
            Return New InferenceRuntimeSettings With {
                .ModelIdentifier = ModelIdentifier,
                .ModelPath = ModelPath,
                .DefaultMaxTokens = DefaultMaxTokens,
                .ContextLength = ContextLength,
                .ThreadCount = ThreadCount,
                .DefaultTemperature = Temperature,
                .BackendName = backendName
            }
        End Function

        Public Function ToNativeRuntimeOptions() As NativeRuntimeOptions
            Return New NativeRuntimeOptions With {
                .LibraryPath = NativeLibraryPath,
                .ModelPath = ModelPath,
                .WorkingDirectory = Environment.CurrentDirectory,
                .ContextLength = ContextLength,
                .ThreadCount = ThreadCount,
                .GpuLayers = GpuLayers
            }
        End Function

        Public Shared Function Parse(arguments As IReadOnlyList(Of String), Optional environmentReader As Func(Of String, String) = Nothing) As ServerOptions
            Dim readEnvironment = If(environmentReader, AddressOf Environment.GetEnvironmentVariable)
            Dim options As New ServerOptions With {
                .ModelPath = ReadString(readEnvironment, "VISUALLLM_MODEL_PATH", String.Empty),
                .ModelAlias = ReadString(readEnvironment, "VISUALLLM_MODEL_ALIAS", "default"),
                .NativeLibraryPath = ReadString(readEnvironment, "VISUALLLM_NATIVE_LIBRARY", String.Empty),
                .ContextLength = ReadInteger(readEnvironment, "VISUALLLM_CONTEXT_LENGTH", 4096),
                .ThreadCount = ReadInteger(readEnvironment, "VISUALLLM_THREAD_COUNT", Environment.ProcessorCount),
                .GpuLayers = ReadInteger(readEnvironment, "VISUALLLM_GPU_LAYERS", 99),
                .Temperature = ReadDouble(readEnvironment, "VISUALLLM_TEMPERATURE", 0.7),
                .ListenPort = ReadInteger(readEnvironment, "VISUALLLM_LISTEN_PORT", 5000),
                .BackendPreference = ReadString(readEnvironment, "VISUALLLM_BACKEND", "auto"),
                .ShowBanner = ReadBoolean(readEnvironment, "VISUALLLM_SHOW_BANNER", True),
                .EnableGracefulDegradation = ReadBoolean(readEnvironment, "VISUALLLM_GRACEFUL_DEGRADATION", True),
                .DefaultMaxTokens = ReadInteger(readEnvironment, "VISUALLLM_MAX_TOKENS", 256)
            }

            Dim index = 0

            While index < arguments.Count
                Dim argument = arguments(index)

                Select Case argument
                    Case "--model"
                        options.ModelPath = ReadRequiredArgument(arguments, argument, index)
                    Case "--model-alias"
                        options.ModelAlias = ReadRequiredArgument(arguments, argument, index)
                    Case "--native-library"
                        options.NativeLibraryPath = ReadRequiredArgument(arguments, argument, index)
                    Case "--context", "--context-length"
                        options.ContextLength = ParseIntegerValue(ReadRequiredArgument(arguments, argument, index), argument)
                    Case "--threads", "--thread-count"
                        options.ThreadCount = ParseIntegerValue(ReadRequiredArgument(arguments, argument, index), argument)
                    Case "--gpu-layers"
                        options.GpuLayers = ParseIntegerValue(ReadRequiredArgument(arguments, argument, index), argument)
                    Case "--temperature"
                        options.Temperature = ParseDoubleValue(ReadRequiredArgument(arguments, argument, index), argument)
                    Case "--listen-port", "--port"
                        options.ListenPort = ParseIntegerValue(ReadRequiredArgument(arguments, argument, index), argument)
                    Case "--backend"
                        options.BackendPreference = ReadRequiredArgument(arguments, argument, index)
                    Case "--max-tokens"
                        options.DefaultMaxTokens = ParseIntegerValue(ReadRequiredArgument(arguments, argument, index), argument)
                    Case "--no-banner"
                        options.ShowBanner = False
                    Case "--graceful-degradation"
                        options.EnableGracefulDegradation = True
                    Case "--strict"
                        options.EnableGracefulDegradation = False
                    Case Else
                        Throw New ArgumentException($"Unknown argument '{argument}'.")
                End Select

                index += 1
            End While

            Return options
        End Function

        Private Shared Function ReadRequiredArgument(arguments As IReadOnlyList(Of String), switchName As String, ByRef index As Integer) As String
            If index + 1 >= arguments.Count Then
                Throw New ArgumentException($"Missing value for '{switchName}'.")
            End If

            index += 1
            Return arguments(index)
        End Function

        Private Shared Function ReadString(environmentReader As Func(Of String, String), name As String, defaultValue As String) As String
            Dim value = environmentReader(name)
            If String.IsNullOrWhiteSpace(value) Then
                Return defaultValue
            End If

            Return value.Trim()
        End Function

        Private Shared Function ReadInteger(environmentReader As Func(Of String, String), name As String, defaultValue As Integer) As Integer
            Dim value = environmentReader(name)
            If String.IsNullOrWhiteSpace(value) Then
                Return defaultValue
            End If

            Return ParseIntegerValue(value, name)
        End Function

        Private Shared Function ReadDouble(environmentReader As Func(Of String, String), name As String, defaultValue As Double) As Double
            Dim value = environmentReader(name)
            If String.IsNullOrWhiteSpace(value) Then
                Return defaultValue
            End If

            Return ParseDoubleValue(value, name)
        End Function

        Private Shared Function ReadBoolean(environmentReader As Func(Of String, String), name As String, defaultValue As Boolean) As Boolean
            Dim value = environmentReader(name)
            If String.IsNullOrWhiteSpace(value) Then
                Return defaultValue
            End If

            Select Case value.Trim().ToLowerInvariant()
                Case "1", "true", "yes", "on"
                    Return True
                Case "0", "false", "no", "off"
                    Return False
                Case Else
                    Throw New ArgumentException($"Invalid boolean value '{value}' for '{name}'.")
            End Select
        End Function

        Private Shared Function ParseIntegerValue(value As String, source As String) As Integer
            Dim parsedValue As Integer
            If Integer.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, parsedValue) Then
                Return parsedValue
            End If

            Throw New ArgumentException($"Invalid integer value '{value}' for '{source}'.")
        End Function

        Private Shared Function ParseDoubleValue(value As String, source As String) As Double
            Dim parsedValue As Double
            If Double.TryParse(value, NumberStyles.Float Or NumberStyles.AllowThousands, CultureInfo.InvariantCulture, parsedValue) Then
                Return parsedValue
            End If

            Throw New ArgumentException($"Invalid floating-point value '{value}' for '{source}'.")
        End Function
    End Class
End Namespace
