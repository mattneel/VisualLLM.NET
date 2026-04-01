Imports System.Text
Imports VisualLLM.Inference.Models

Namespace Services
    Public Class PromptComposer
        Public Function Compose(messages As IReadOnlyList(Of ChatMessage)) As String
            If messages Is Nothing OrElse messages.Count = 0 Then
                Return String.Empty
            End If

            Dim builder As New StringBuilder()

            For Each message In messages
                Dim role = NormalizeRole(message.Role)
                Dim content = NormalizeContent(message.Content)
                builder.Append(role)
                builder.Append(": ")
                builder.AppendLine(content)
            Next

            builder.Append("assistant:")
            Return builder.ToString()
        End Function

        Public Function GetLatestUserMessage(messages As IReadOnlyList(Of ChatMessage)) As String
            If messages Is Nothing OrElse messages.Count = 0 Then
                Return String.Empty
            End If

            For index = messages.Count - 1 To 0 Step -1
                Dim candidate = messages(index)
                If String.Equals(candidate.Role, "user", StringComparison.OrdinalIgnoreCase) Then
                    Return NormalizeContent(candidate.Content)
                End If
            Next

            Return String.Empty
        End Function

        Private Shared Function NormalizeRole(role As String) As String
            If String.IsNullOrWhiteSpace(role) Then
                Return "user"
            End If

            Return role.Trim().ToLowerInvariant()
        End Function

        Private Shared Function NormalizeContent(content As String) As String
            If String.IsNullOrWhiteSpace(content) Then
                Return String.Empty
            End If

            Return content.Trim()
        End Function
    End Class
End Namespace
