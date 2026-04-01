Imports System.Text.RegularExpressions

Namespace Services
    Public NotInheritable Class TokenEstimator
        Private Shared ReadOnly TokenRegex As New Regex("\S+", RegexOptions.Compiled)

        Private Sub New()
        End Sub

        Public Shared Function Estimate(content As String) As Integer
            If String.IsNullOrWhiteSpace(content) Then
                Return 0
            End If

            Return TokenRegex.Matches(content).Count
        End Function
    End Class
End Namespace
