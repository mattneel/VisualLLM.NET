Imports VisualLLM.Server.Hosting
Imports Xunit

Namespace Server
    Public Class ServerOptionsTests
        <Fact>
        Public Sub Parse_ReadsCommandLineValues()
            Dim options = ServerOptions.Parse({
                "--model", "C:\Models\mistral.gguf",
                "--model-alias", "demo",
                "--native-library", "C:\llama\llama.dll",
                "--port", "8088",
                "--context", "8192",
                "--threads", "12",
                "--gpu-layers", "0",
                "--temperature", "0.2",
                "--no-banner"
            })

            Assert.Equal("C:\Models\mistral.gguf", options.ModelPath)
            Assert.Equal("demo", options.ModelIdentifier)
            Assert.Equal("C:\llama\llama.dll", options.NativeLibraryPath)
            Assert.Equal(8088, options.ListenPort)
            Assert.Equal(8192, options.ContextLength)
            Assert.Equal(12, options.ThreadCount)
            Assert.Equal(0, options.GpuLayers)
            Assert.Equal(0.2, options.Temperature, 3)
            Assert.False(options.ShowBanner)
        End Sub

        <Fact>
        Public Sub Parse_ThrowsForUnknownSwitch()
            Dim exception = Assert.Throws(Of ArgumentException)(
                Sub()
                    ServerOptions.Parse({"--wat"})
                End Sub)

            Assert.Contains("--wat", exception.Message)
        End Sub
    End Class
End Namespace
