Option Strict On
Option Explicit On

Imports System
Imports System.Drawing

Namespace Rendering
    ' Shared presentation state for any maze dimensions. Never changes physics or input.
    Public NotInheritable Class BoardCamera
        Private _zoomIndex As Integer
        Private _zoom As Single = 1
        Private _focus As New PointF(0.5F, 0.5F)
        Private _ready As Boolean

        Public ReadOnly Property Zoom As Single
            Get
                Return _zoom
            End Get
        End Property

        Public ReadOnly Property PreferredZoom As Single
            Get
                Return If(_zoomIndex = 0, 1.0F, If(_zoomIndex = 1, 1.5F, 2.0F))
            End Get
        End Property

        Public ReadOnly Property Label As String
            Get
                Return If(_zoomIndex = 0, "FULL MAZE", If(_zoomIndex = 1, "1.5x", "2x"))
            End Get
        End Property

        Public ReadOnly Property Focus As PointF
            Get
                Return _focus
            End Get
        End Property

        Public Sub CycleZoom()
            _zoomIndex = (_zoomIndex + 1) Mod 3
        End Sub

        Public Sub ResetTracking()
            _ready = False
            _zoom = 1
        End Sub

        Public Sub Recenter(x As Single, y As Single)
            If Not Single.IsFinite(x) OrElse Not Single.IsFinite(y) Then Return
            _focus = New PointF(Math.Clamp(x, 0, 1), Math.Clamp(y, 0, 1))
            _ready = True
        End Sub

        Public Sub Advance(x As Single, y As Single, elapsedMs As Single,
                           enabled As Boolean, overview As Boolean)
            If Not Single.IsFinite(x) OrElse Not Single.IsFinite(y) OrElse
               Not Single.IsFinite(elapsedMs) OrElse elapsedMs < 0 Then Return
            If Not _ready Then Recenter(x, y)
            Dim targetZoom = If(enabled AndAlso Not overview, PreferredZoom, 1.0F)
            If Not enabled OrElse overview Then
                _zoom = 1 ' Overview must show the entire board immediately.
            Else
                _zoom += (targetZoom - _zoom) * CSng(1 - Math.Exp(-elapsedMs / 160.0))
                If Math.Abs(_zoom - targetZoom) < 0.001F Then _zoom = targetZoom
            End If
            Dim deadZone As Single = 0.10F / targetZoom
            Dim blend = CSng(1 - Math.Exp(-elapsedMs / 180.0))
            Dim dx = x - _focus.X, dy = y - _focus.Y
            If Math.Abs(dx) > deadZone Then _focus.X += (dx - Math.Sign(dx) * deadZone) * blend
            If Math.Abs(dy) > deadZone Then _focus.Y += (dy - Math.Sign(dy) * deadZone) * blend
            _focus.X = Math.Clamp(_focus.X, 0, 1)
            _focus.Y = Math.Clamp(_focus.Y, 0, 1)
        End Sub

        ' Translate/scale the whole board layer, including its sphere and shadows, then draw HUD separately.
        Public Function Frame(viewport As Rectangle, columns As Integer, rows As Integer,
                              projection As BoardProjection) As (Scale As Single, Offset As PointF)
            If viewport.Width < 32 OrElse viewport.Height < 32 OrElse columns <= 0 OrElse rows <= 0 Then Return (1, PointF.Empty)
            If _zoom = 1 Then Return (1, PointF.Empty)
            Dim margin As Single = Math.Min(viewport.Width, viewport.Height) * 0.04F
            Dim tileSize = Math.Min((viewport.Width - 2 * margin) / columns,
                                   (viewport.Height - 2 * margin) / rows)
            Dim width = tileSize * columns, height = tileSize * rows
            Dim board As New RectangleF(viewport.Left + (viewport.Width - width) / 2,
                                        viewport.Top + (viewport.Height - height) / 2, width, height)
            Dim center = Map(New PointF(board.Left + _focus.X * width, board.Top + _focus.Y * height), viewport, projection)
            Dim a = Map(New PointF(board.Left, board.Top), viewport, projection)
            Dim b = Map(New PointF(board.Right, board.Top), viewport, projection)
            Dim c = Map(New PointF(board.Left, board.Bottom), viewport, projection)
            Dim d = Map(New PointF(board.Right, board.Bottom), viewport, projection)
            Dim minX = Math.Min(Math.Min(a.X, b.X), Math.Min(c.X, d.X))
            Dim maxX = Math.Max(Math.Max(a.X, b.X), Math.Max(c.X, d.X))
            Dim minY = Math.Min(Math.Min(a.Y, b.Y), Math.Min(c.Y, d.Y))
            Dim maxY = Math.Max(Math.Max(a.Y, b.Y), Math.Max(c.Y, d.Y))
            Return (_zoom, New PointF(ClampOffset(viewport.Left, viewport.Right, minX, maxX, center.X),
                                     ClampOffset(viewport.Top, viewport.Bottom, minY, maxY, center.Y)))
        End Function

        Private Function ClampOffset(viewStart As Single, viewEnd As Single, boardStart As Single,
                                     boardEnd As Single, focus As Single) As Single
            Dim viewCenter = (viewStart + viewEnd) / 2
            If (boardEnd - boardStart) * _zoom <= viewEnd - viewStart Then
                Return viewCenter - (boardStart + boardEnd) / 2 * _zoom
            End If
            Return Math.Clamp(viewCenter - focus * _zoom, viewEnd - boardEnd * _zoom, viewStart - boardStart * _zoom)
        End Function

        Private Shared Function Map(point As PointF, viewport As Rectangle, projection As BoardProjection) As PointF
            If projection Is Nothing Then Return point
            Dim mapped = projection.Project((point.X - viewport.Left) * 2.0 / viewport.Width - 1,
                                            (point.Y - viewport.Top) * 2.0 / viewport.Height - 1)
            Return New PointF(viewport.Left + (mapped.X + 1) * viewport.Width / 2,
                              viewport.Top + (mapped.Y + 1) * viewport.Height / 2)
        End Function
    End Class
End Namespace
