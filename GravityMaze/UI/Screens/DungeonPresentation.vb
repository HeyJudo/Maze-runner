Option Strict On
Option Explicit On

Namespace UI.Screens
    ' Copy is derived from the engine mask; this class owns no progression state.
    Public NotInheritable Class DungeonPresentation
        Private Sub New()
        End Sub
        Public Shared Function Objective(mask As Integer, escaping As Boolean) As String
            If escaping Then Return "Escape through the new passage and return to the entrance."
            Select Case mask And 3
                Case 0 : Return "Find the Crypt and Armory keys."
                Case 1 : Return "Find the Armory key in the east wing."
                Case 2 : Return "Find the Crypt key in the west wing."
                Case Else : Return "Vault unlocked — head north and take the vault key."
            End Select
        End Function
        Public Shared Function VaultWarning(mask As Integer, escaping As Boolean) As String
            If (mask And 3) = 3 AndAlso Not escaping AndAlso (mask And 4) = 0 Then
                Return "Taking the vault key starts the collapse. Escape through the new passage and return to the entrance."
            End If
            Return ""
        End Function
        Public Shared ReadOnly Rules As String() = {
            "Walls cost 1/4 heart; traps and falls cost 1 heart.",
            "Amber trap warnings signal approaching danger; wait on safe floor.",
            "Crumbling floor gives way shortly after contact; lava and open pits cause falls.",
            "A lit checkpoint returns you after a fall; collected keys stay.",
            "During escape, the collapse front is the clock. Cracks warn it is approaching; cave-in warnings come before rubble.",
            "The inbound vault gate closes; a new passage opens in the upper-left.",
            "Escape checkpoints replace exploration checkpoints. Before the first escape checkpoint, falls return you to the vault."}
    End Class
End Namespace
