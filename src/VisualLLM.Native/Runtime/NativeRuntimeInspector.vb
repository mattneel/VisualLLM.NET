Imports System.IO
Imports System.Linq
Imports System.Runtime.InteropServices

Namespace Runtime
    Public NotInheritable Class NativeRuntimeInspector
        Private Sub New()
        End Sub

        Public Shared Function Inspect(options As NativeRuntimeOptions) As NativeRuntimeStatus
            Dim runtimeIdentifier = GetCurrentRuntimeIdentifier()
            Dim searchRoots = BuildSearchRoots(options, runtimeIdentifier)
            Dim resolvedExecutable = ResolveExecutable(options, runtimeIdentifier, searchRoots)
            Dim resolvedLibrary = ResolveLibrary(options, searchRoots)

            Dim status As New NativeRuntimeStatus With {
                .RuntimeIdentifier = runtimeIdentifier,
                .ResolvedLibraryPath = resolvedLibrary,
                .ResolvedServerExecutablePath = resolvedExecutable,
                .SearchRoots = searchRoots
            }

            status.IsAvailable = status.HasLibrary OrElse status.HasServerExecutable
            status.Message = BuildMessage(status)

            Return status
        End Function

        Public Shared Function GetCurrentRuntimeIdentifier() As String
            Dim osPart As String
            If RuntimeInformation.IsOSPlatform(OSPlatform.Windows) Then
                osPart = "win"
            ElseIf RuntimeInformation.IsOSPlatform(OSPlatform.OSX) Then
                osPart = "osx"
            Else
                osPart = "linux"
            End If

            Dim architecturePart = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant()
            Return $"{osPart}-{architecturePart}"
        End Function

        Private Shared Function ResolveExecutable(
            options As NativeRuntimeOptions,
            runtimeIdentifier As String,
            searchRoots As IReadOnlyCollection(Of String)) As String

            Dim executableName = If(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "llama-server.exe", "llama-server")
            Dim candidates As New List(Of String)()

            If Not String.IsNullOrWhiteSpace(options.ServerExecutablePath) Then
                candidates.Add(options.ServerExecutablePath)
            End If

            For Each root In searchRoots
                candidates.Add(Path.Combine(root, executableName))
                candidates.Add(Path.Combine(root, runtimeIdentifier, executableName))
            Next

            candidates.AddRange(ResolveFromPath(executableName))

            Return candidates.
                Select(AddressOf NormalizePath).
                FirstOrDefault(Function(candidate) File.Exists(candidate))
        End Function

        Private Shared Function ResolveLibrary(options As NativeRuntimeOptions, searchRoots As IReadOnlyCollection(Of String)) As String
            Dim candidates As New List(Of String)()

            If Not String.IsNullOrWhiteSpace(options.LibraryPath) Then
                candidates.Add(options.LibraryPath)
            End If

            Dim libraryName As String
            If RuntimeInformation.IsOSPlatform(OSPlatform.Windows) Then
                libraryName = "llama.dll"
            ElseIf RuntimeInformation.IsOSPlatform(OSPlatform.OSX) Then
                libraryName = "libllama.dylib"
            Else
                libraryName = "libllama.so"
            End If

            For Each root In searchRoots
                candidates.Add(Path.Combine(root, libraryName))
            Next

            Return candidates.
                Select(AddressOf NormalizePath).
                FirstOrDefault(Function(candidate) File.Exists(candidate))
        End Function

        Private Shared Function BuildSearchRoots(options As NativeRuntimeOptions, runtimeIdentifier As String) As List(Of String)
            Dim roots As New List(Of String)()
            Dim currentDirectory = Environment.CurrentDirectory
            Dim baseDirectory = AppContext.BaseDirectory

            roots.Add(baseDirectory)
            roots.Add(Path.Combine(baseDirectory, "runtimes", runtimeIdentifier, "native"))
            roots.Add(Path.Combine(currentDirectory, "runtimes", runtimeIdentifier, "native"))
            roots.Add(Path.Combine(currentDirectory, "build", "llama.cpp", "bin"))
            roots.Add(Path.Combine(currentDirectory, "build", "llama.cpp", "bin", "Release"))
            roots.Add(Path.Combine(currentDirectory, "build", "llama.cpp", runtimeIdentifier, "bin"))
            roots.Add(Path.Combine(currentDirectory, "build", "llama.cpp", runtimeIdentifier, "bin", "Release"))
            roots.Add(Path.Combine(currentDirectory, "build", "native", runtimeIdentifier, "bin"))
            roots.Add(Path.Combine(currentDirectory, "build", "native", runtimeIdentifier, "bin", "Release"))
            roots.Add(Path.Combine(currentDirectory, "tools", "llama.cpp", runtimeIdentifier))
            roots.Add(Path.Combine(currentDirectory, "artifacts", "llama.cpp", runtimeIdentifier))

            If Not String.IsNullOrWhiteSpace(options.WorkingDirectory) Then
                roots.Add(options.WorkingDirectory)
                roots.Add(Path.Combine(options.WorkingDirectory, "runtimes", runtimeIdentifier, "native"))
                roots.Add(Path.Combine(options.WorkingDirectory, "build", "llama.cpp", "bin"))
                roots.Add(Path.Combine(options.WorkingDirectory, "build", "llama.cpp", "bin", "Release"))
                roots.Add(Path.Combine(options.WorkingDirectory, "build", "llama.cpp", runtimeIdentifier, "bin"))
                roots.Add(Path.Combine(options.WorkingDirectory, "build", "llama.cpp", runtimeIdentifier, "bin", "Release"))
            End If

            Return roots.
                Select(AddressOf NormalizePath).
                Where(Function(path) Not String.IsNullOrWhiteSpace(path)).
                Distinct(StringComparer.OrdinalIgnoreCase).
                ToList()
        End Function

        Private Shared Function ResolveFromPath(fileName As String) As IEnumerable(Of String)
            Dim pathValue = Environment.GetEnvironmentVariable("PATH")
            If String.IsNullOrWhiteSpace(pathValue) Then
                Return Enumerable.Empty(Of String)()
            End If

            Return pathValue.
                Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).
                Select(Function(pathSegment) Path.Combine(pathSegment.Trim(), fileName))
        End Function

        Private Shared Function NormalizePath(pathValue As String) As String
            If String.IsNullOrWhiteSpace(pathValue) Then
                Return String.Empty
            End If

            Return Path.GetFullPath(pathValue.Trim())
        End Function

        Private Shared Function BuildMessage(status As NativeRuntimeStatus) As String
            If status.HasLibrary Then
                Return $"Found native llama library at '{status.ResolvedLibraryPath}'."
            End If

            If status.HasServerExecutable Then
                Return $"Found portable llama-server runtime at '{status.ResolvedServerExecutablePath}', but no shared llama library yet."
            End If

            Return "No portable llama.cpp runtime was found. Build the shared library into runtimes/<rid>/native or set VISUALLLM_NATIVE_LIBRARY."
        End Function
    End Class
End Namespace
