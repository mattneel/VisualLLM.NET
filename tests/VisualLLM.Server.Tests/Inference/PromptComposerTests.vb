Imports VisualLLM.Inference.Models
Imports VisualLLM.Inference.Services
Imports Xunit

Namespace Inference
    Public Class PromptComposerTests
        <Fact>
        Public Sub Compose_AppendsAssistantCue()
            Dim composer As New PromptComposer()
            Dim messages = New List(Of ChatMessage) From {
                New ChatMessage With {.Role = "system", .Content = "Behave."},
                New ChatMessage With {.Role = "user", .Content = "Hello there"}
            }

            Dim prompt = composer.Compose(messages)

            Assert.Contains("system: Behave.", prompt)
            Assert.Contains("user: Hello there", prompt)
            Assert.EndsWith("assistant:", prompt)
        End Sub
    End Class
End Namespace
