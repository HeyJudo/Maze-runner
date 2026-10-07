Option Strict On
Option Explicit On

Imports System
Imports System.Drawing
Imports System.Collections.Generic
Imports GravityMaze.Levels

Namespace Engine
    Public NotInheritable Class DungeonCueEventArgs
        Inherits EventArgs
        Public ReadOnly Property Kind As String
        Public ReadOnly Property X As Single
        Public ReadOnly Property Y As Single
        Public Sub New(kind As String, x As Single, y As Single)
            Me.Kind = kind : Me.X = x : Me.Y = y
        End Sub
    End Class

    Public NotInheritable Class DungeonDart
        Public X, Y As Single
        Public ReadOnly DX, DY As Integer
        Public Sub New(launcher As DartLauncher)
            X = launcher.X : Y = launcher.Y : DX = launcher.DX : DY = launcher.DY
        End Sub
    End Class

    ' One deterministic, pausable trap clock. No UI, rendering or input dependencies.
    Public NotInheritable Class DungeonRun
        Public Const CycleMs As Integer = 6000
        Public Const WarningMs As Integer = 800
        Public Const SpikeWarningStart As Integer = 1300
        Public Const SpikeActiveStart As Integer = 2000
        Public Const SpikeActiveEnd As Integer = 3200
        Public Const BladeRadius As Single = 0.34F
        Public Const DartRadius As Single = 0.09F
        Public Const DartStep As Single = 0.14F
        Private ReadOnly maze As MazeDefinition
        Private ReadOnly darts_ As New List(Of DungeonDart)()
        Private contacts As New HashSet(Of String)()
        Public Event Cue As EventHandler(Of DungeonCueEventArgs)
        Public ReadOnly Property Definition As DungeonDefinition
        Public Property ElapsedMs As Long
            Get
                Return clock
            End Get
            Private Set(value As Long)
                clock = value
            End Set
        End Property
        Private clock As Long
        Public Property SealMask As Integer
            Get
                Return seals
            End Get
            Private Set(value As Integer)
                seals = value
            End Set
        End Property
        Private seals As Integer
        Public ReadOnly Property SealCount As Integer
            Get
                Return (SealMask And 1) + ((SealMask >> 1) And 1)
            End Get
        End Property
        Public ReadOnly Property Darts As IReadOnlyList(Of DungeonDart)
            Get
                Return darts_
            End Get
        End Property

        Public Sub New(maze As MazeDefinition)
            Me.maze = maze
            Definition = maze.Dungeon
            If Definition Is Nothing Then Throw New ArgumentException("Dungeon runtime needs dungeon data.")
        End Sub

        Public Sub Reset()
            ElapsedMs = 0 : SealMask = 0
            darts_.Clear() : contacts.Clear()
        End Sub

        Public Function GateOpen(tile As Char) As Boolean
            Select Case tile
                Case "a"c : Return (SealMask And 1) <> 0
                Case "b"c : Return (SealMask And 2) <> 0
                Case "E"c : Return SealMask = 3
                Case Else : Return True
            End Select
        End Function

        Public Sub Collect(x As Single, y As Single)
            Dim r = CInt(Math.Floor(y)), c = CInt(Math.Floor(x))
            If r < 0 OrElse c < 0 OrElse r >= maze.RowCount OrElse c >= maze.ColumnCount Then Return
            Dim tile = maze.GetTile(r, c)
            Dim bit = If(tile = "K"c, 1, If(tile = "Q"c, 2, 0))
            If bit = 0 OrElse (SealMask And bit) <> 0 OrElse DistanceSquared(x, y, c + 0.5F, r + 0.5F) > 0.45F * 0.45F Then Return
            SealMask = SealMask Or bit
            RaiseEvent Cue(Me, New DungeonCueEventArgs("seal", c + 0.5F, r + 0.5F))
        End Sub

        Public Shared Function Phase(atMs As Long, offset As Integer, Optional period As Integer = CycleMs) As Integer
            Return CInt(((atMs + CLng(offset)) Mod period + period) Mod period)
        End Function

        Public Function SpikeActive(spike As SpikeTrap, Optional atMs As Long = -1) As Boolean
            Dim p = Phase(If(atMs < 0, ElapsedMs, atMs), spike.OffsetMs)
            Return p >= SpikeActiveStart AndAlso p < SpikeActiveEnd
        End Function

        Public Function SpikeWarning(spike As SpikeTrap) As Boolean
            Dim p = Phase(ElapsedMs, spike.OffsetMs)
            Return p >= SpikeWarningStart AndAlso p < SpikeActiveStart
        End Function

        Public Shared Function BladePosition(blade As BladeTrap, atMs As Long) As PointF
            Dim p = Phase(atMs, blade.OffsetMs, blade.PeriodMs)
            Dim half = blade.PeriodMs / 2.0
            Const dwell As Double = 600
            Dim t As Double
            If p < dwell Then
                t = 0
            ElseIf p < half Then
                t = (p - dwell) / (half - dwell)
                t = t * t * (3 - 2 * t)
            ElseIf p < half + dwell Then
                t = 1
            Else
                t = 1 - (p - half - dwell) / (half - dwell)
                t = t * t * (3 - 2 * t)
            End If
            Return New PointF(CSng(blade.X1 + (blade.X2 - blade.X1) * t), CSng(blade.Y1 + (blade.Y2 - blade.Y1) * t))
        End Function

        Public Function Solid(row As Integer, column As Integer) As Boolean
            If row < 0 OrElse column < 0 OrElse row >= maze.RowCount OrElse column >= maze.ColumnCount Then Return True
            Dim tile = maze.GetTile(row, column)
            Return tile = "1"c OrElse Not GateOpen(tile)
        End Function

        ' Return at most one damage cue per frame, but consume every dart and record every contact.
        Public Function Advance(oldX As Single, oldY As Single, x As Single, y As Single) As DungeonCueEventArgs
            Dim previous = ElapsedMs
            ElapsedMs += 16
            Dim hit As DungeonCueEventArgs = Nothing
            Dim nowContacts As New HashSet(Of String)()
            For i As Integer = 0 To Definition.Spikes.Count - 1
                Dim spike = Definition.Spikes(i)
                If SpikeActive(spike) AndAlso TouchesTile(x, y, spike.Column, spike.Row, GameEngine.BallRadius) Then
                    Dim id = "s" & i
                    nowContacts.Add(id)
                    If Not contacts.Contains(id) AndAlso hit Is Nothing Then hit = New DungeonCueEventArgs("spike", x, y)
                End If
                Dim oldPhase = Phase(previous, spike.OffsetMs)
                Dim newPhase = Phase(ElapsedMs, spike.OffsetMs)
                If oldPhase < SpikeWarningStart AndAlso newPhase >= SpikeWarningStart AndAlso DistanceSquared(x, y, spike.Column + 0.5F, spike.Row + 0.5F) < 36 Then
                    RaiseEvent Cue(Me, New DungeonCueEventArgs("warning", spike.Column + 0.5F, spike.Row + 0.5F))
                End If
            Next
            For i As Integer = 0 To Definition.Blades.Count - 1
                Dim blade = Definition.Blades(i)
                Dim before = BladePosition(blade, previous), after = BladePosition(blade, ElapsedMs)
                If SweptTouches(before.X, before.Y, after.X, after.Y, oldX, oldY, x, y, BladeRadius + GameEngine.BallRadius) Then
                    Dim id = "b" & i
                    nowContacts.Add(id)
                    If Not contacts.Contains(id) AndAlso hit Is Nothing Then hit = New DungeonCueEventArgs("blade", after.X, after.Y)
                End If
            Next
            contacts = nowContacts
            For Each launcher In Definition.Launchers
                Dim before = Phase(previous, launcher.OffsetMs), after = Phase(ElapsedMs, launcher.OffsetMs)
                If (after < before OrElse (previous = 0 AndAlso before = 0)) AndAlso DistanceSquared(x, y, launcher.X, launcher.Y) < 36 Then
                    RaiseEvent Cue(Me, New DungeonCueEventArgs("warning", launcher.X, launcher.Y))
                End If
                For Each shot In {WarningMs, WarningMs + 250, WarningMs + 500}
                    If (before < shot AndAlso after >= shot) OrElse (after < before AndAlso after >= shot) Then darts_.Add(New DungeonDart(launcher))
                Next
            Next
            For i As Integer = darts_.Count - 1 To 0 Step -1
                Dim dart = darts_(i)
                Dim nx = dart.X + dart.DX * DartStep, ny = dart.Y + dart.DY * DartStep
                If Solid(CInt(Math.Floor(ny)), CInt(Math.Floor(nx))) Then
                    darts_.RemoveAt(i)
                ElseIf SweptTouches(dart.X, dart.Y, nx, ny, oldX, oldY, x, y, DartRadius + GameEngine.BallRadius) Then
                    If hit Is Nothing Then hit = New DungeonCueEventArgs("dart", nx, ny)
                    darts_.RemoveAt(i)
                Else
                    dart.X = nx : dart.Y = ny
                End If
            Next
            Return hit
        End Function

        Public Shared Function SweptTouches(ax As Single, ay As Single, bx As Single, by As Single,
                                            px As Single, py As Single, qx As Single, qy As Single, radius As Single) As Boolean
            Dim x0 As Double = ax - px, y0 As Double = ay - py
            Dim dx As Double = (bx - qx) - x0, dy As Double = (by - qy) - y0
            Dim length = dx * dx + dy * dy
            Dim t = If(length < 0.0000001, 0, Math.Clamp(-(x0 * dx + y0 * dy) / length, 0, 1))
            Return (x0 + dx * t) ^ 2 + (y0 + dy * t) ^ 2 <= radius * radius
        End Function

        Public Shared Function TouchesTile(x As Single, y As Single, column As Integer, row As Integer, radius As Single) As Boolean
            Dim dx = x - Math.Clamp(x, CSng(column), column + 1.0F)
            Dim dy = y - Math.Clamp(y, CSng(row), row + 1.0F)
            Return dx * dx + dy * dy < radius * radius
        End Function

        Private Shared Function DistanceSquared(x As Single, y As Single, a As Single, b As Single) As Single
            Return (x - a) * (x - a) + (y - b) * (y - b)
        End Function
    End Class
End Namespace
