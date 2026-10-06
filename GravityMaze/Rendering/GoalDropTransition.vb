Option Strict On
Option Explicit On

Imports System

Namespace Rendering
    Public Enum GoalDropPhase
        Drop
        Lift
        Landing
        Ready
        Finished
    End Enum

    ' A clock and presentation values only; the shell owns level loading and scoring.
    Public NotInheritable Class GoalDropTransition
        Public Const DropMs As Single = 650
        Public Const LiftMs As Single = 650
        Public Const LandingMs As Single = 450
        Public Const ReadyMs As Single = 650
        Public Const NextLevelMs As Single = DropMs + LiftMs + LandingMs + ReadyMs
        Public Const ExitMs As Single = DropMs + LiftMs
        Public Const LandingContactMs As Single = ExitMs + LandingMs * 0.65F
        Public ReadOnly Property HasNextLevel As Boolean
        Private _elapsedMs As Single
        Public ReadOnly Property ElapsedMs As Single
            Get
                Return _elapsedMs
            End Get
        End Property

        Public Sub New(hasNextLevel As Boolean)
            Me.HasNextLevel = hasNextLevel
        End Sub

        Public ReadOnly Property Phase As GoalDropPhase
            Get
                If ElapsedMs < DropMs Then Return GoalDropPhase.Drop
                If ElapsedMs < ExitMs Then Return GoalDropPhase.Lift
                If Not HasNextLevel Then Return GoalDropPhase.Finished
                If ElapsedMs < ExitMs + LandingMs Then Return GoalDropPhase.Landing
                If ElapsedMs < NextLevelMs Then Return GoalDropPhase.Ready
                Return GoalDropPhase.Finished
            End Get
        End Property

        Public ReadOnly Property Progress As Single
            Get
                Select Case Phase
                    Case GoalDropPhase.Drop : Return ElapsedMs / DropMs
                    Case GoalDropPhase.Lift : Return (ElapsedMs - DropMs) / LiftMs
                    Case GoalDropPhase.Landing : Return (ElapsedMs - ExitMs) / LandingMs
                    Case GoalDropPhase.Ready : Return (ElapsedMs - ExitMs - LandingMs) / ReadyMs
                    Case Else : Return 1
                End Select
            End Get
        End Property

        Public ReadOnly Property LandingHeight As Single
            Get
                If Phase <> GoalDropPhase.Landing Then Return 0
                Dim p As Single = Progress
                If p <= 0.65F Then
                    Dim fall As Single = p / 0.65F
                    Return 3.0F * (1.0F - fall * fall)
                End If
                Return CSng(0.18 * Math.Sin((p - 0.65F) / 0.35F * Math.PI))
            End Get
        End Property

        Public Sub Advance(ms As Single)
            If Not Single.IsFinite(ms) OrElse ms < 0 Then Throw New ArgumentOutOfRangeException(NameOf(ms))
            _elapsedMs = Math.Min(If(HasNextLevel, NextLevelMs, ExitMs), ElapsedMs + ms)
        End Sub

        Public Shared Function Ease(p As Single) As Single
            p = Math.Clamp(p, 0.0F, 1.0F)
            Return p * p * (3.0F - 2.0F * p)
        End Function
    End Class
End Namespace
