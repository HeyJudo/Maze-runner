Option Strict On
Option Explicit On

Imports System
Imports System.Globalization
Imports System.IO.Ports
Imports System.Threading

Namespace Input
    ' Reads the MPU-6050 tilt board over USB serial and exposes it as ITiltInput (-1..1 per axis).
    ' Plug-and-play: finds the board on any COM port, reconnects if unplugged, zeroes itself on connect.
    ' Accepts both line formats:
    '   "0.25,-0.40"                     processed tilt (the PRD contract)
    '   "X: 0.12 Y: -4.90 Z: 8.49"       raw acceleration in m/s^2 (the current sketch)
    ' Anything else (e.g. "MPU6050 connected!") is ignored.
    Public NotInheritable Class ArduinoController
        Implements ITiltInput, IDisposable

        ' ── Calibration knobs (real sensors and mountings differ; tune here) ──
        Public Property PortName As String = Nothing       ' Nothing = auto-detect
        Public Property BaudRate As Integer = 115200
        Public Property FullTiltDegrees As Single = 20.0F  ' board angle that counts as full tilt (raw format only)
        Public Property Deadzone As Single = 0.08F         ' ignore wobble near level
        Public Property Smoothing As Single = 0.35F        ' 0..1, weight of each new sample (1 = no smoothing)
        Public Property SwapAxes As Boolean = False        ' sensor mounted rotated 90 degrees
        Public Property InvertX As Boolean = False         ' ball goes left when it should go right
        Public Property InvertY As Boolean = False         ' ball goes up when it should go down

        Private Const Gravity As Single = 9.80665F
        Private Const CalibrationSamples As Integer = 15

        Private _tiltX As Single
        Private _tiltY As Single
        Private _offsetX As Single
        Private _offsetY As Single
        Private _calX As Single
        Private _calY As Single
        Private _calCount As Integer = -1         ' >= 0 while collecting a calibration
        Private _connectedPort As String
        Private _port As SerialPort
        Private _thread As Thread
        Private _stopping As Boolean

        Public ReadOnly Property TiltX As Single Implements ITiltInput.TiltX
            Get
                Return If(IsConnected, _tiltX, 0.0F)
            End Get
        End Property

        Public ReadOnly Property TiltY As Single Implements ITiltInput.TiltY
            Get
                Return If(IsConnected, _tiltY, 0.0F)
            End Get
        End Property

        Public ReadOnly Property IsConnected As Boolean
            Get
                Return _connectedPort IsNot Nothing
            End Get
        End Property

        Public ReadOnly Property ConnectedPort As String
            Get
                Return _connectedPort
            End Get
        End Property

        Public ReadOnly Property IsCalibrating As Boolean
            Get
                Return _calCount >= 0
            End Get
        End Property

        ' Starts the background reader. Never blocks the UI and never throws.
        Public Sub Start()
            If _thread IsNot Nothing Then Return
            _thread = New Thread(AddressOf Run) With {.IsBackground = True, .Name = "ArduinoController"}
            _thread.Start()
        End Sub

        ' Hold the board level, then call: the next few readings become the new "flat".
        Public Sub Calibrate()
            _calX = 0
            _calY = 0
            _calCount = 0
        End Sub

        ' ── Background loop: find → read until it fails → find again ─────────
        Private Sub Run()
            While Not _stopping
                Try
                    Dim found As SerialPort = FindBoard()
                    If found Is Nothing Then
                        Thread.Sleep(2000)
                        Continue While
                    End If
                    _port = found
                    _connectedPort = found.PortName
                    Calibrate()
                    While Not _stopping
                        Accept(found.ReadLine())   ' ReadTimeout ends this loop if the board goes quiet
                    End While
                Catch
                    ' Unplugged, timed out or port error: drop it and search again.
                Finally
                    _connectedPort = Nothing
                    _tiltX = 0
                    _tiltY = 0
                    ClosePort(_port)
                    _port = Nothing
                End Try
                If Not _stopping Then Thread.Sleep(500)
            End While
        End Sub

        ' Opens each candidate port and keeps the first one that sends a readable tilt line.
        ' Uno-style boards reset when the port opens, so allow ~3 s for the sketch to boot.
        Private Function FindBoard() As SerialPort
            Dim names As String() = If(PortName IsNot Nothing, {PortName}, SerialPort.GetPortNames())
            For Each name As String In names
                If _stopping Then Return Nothing
                Dim sp As SerialPort = Nothing
                Try
                    sp = New SerialPort(name, BaudRate) With {.ReadTimeout = 1500, .NewLine = vbLf, .DtrEnable = True}
                    sp.Open()
                    Dim deadline As DateTime = DateTime.UtcNow.AddSeconds(3.5)
                    While DateTime.UtcNow < deadline
                        Dim ax, ay As Single
                        Dim raw As Boolean
                        Dim line As String
                        Try
                            line = sp.ReadLine()
                        Catch ex As TimeoutException
                            Continue While   ' still booting
                        End Try
                        If TryParse(line, ax, ay, raw) Then Return sp
                    End While
                Catch
                    ' Not our board (busy, wrong device, nothing sent): try the next port.
                End Try
                ClosePort(sp)
            Next
            Return Nothing
        End Function

        Private Sub Accept(line As String)
            Dim ax, ay As Single
            Dim raw As Boolean
            If Not TryParse(line, ax, ay, raw) Then Return

            ' Raw m/s^2 → fraction of full tilt. The axis pointing downhill reads negative, so X is negated:
            ' tipping the right edge down must roll the ball right.
            Dim x As Single
            Dim y As Single
            If raw Then
                Dim full As Single = CSng(Math.Sin(FullTiltDegrees * Math.PI / 180.0))
                x = -ax / Gravity / full
                y = ay / Gravity / full
            Else
                x = ax
                y = ay
            End If
            If SwapAxes Then
                Dim t As Single = x : x = y : y = t
            End If
            If InvertX Then x = -x
            If InvertY Then y = -y

            If _calCount >= 0 Then
                _calX += x
                _calY += y
                _calCount += 1
                If _calCount >= CalibrationSamples Then
                    _offsetX = _calX / CalibrationSamples
                    _offsetY = _calY / CalibrationSamples
                    _calCount = -1
                End If
                Return
            End If

            _tiltX += (Shape(x - _offsetX) - _tiltX) * Smoothing
            _tiltY += (Shape(y - _offsetY) - _tiltY) * Smoothing
        End Sub

        ' Deadzone with a smooth ramp after it (no jump at the edge), clamped to -1..1.
        Private Function Shape(v As Single) As Single
            Dim a As Single = Math.Abs(v)
            If a <= Deadzone Then Return 0.0F
            Return Math.Sign(v) * Math.Min(1.0F, (a - Deadzone) / (1.0F - Deadzone))
        End Function

        ' Parses one serial line. raw = True for "X: .. Y: .. Z: .." (m/s^2), False for "x,y" (already -1..1).
        Public Shared Function TryParse(line As String, ByRef x As Single, ByRef y As Single, ByRef raw As Boolean) As Boolean
            If String.IsNullOrWhiteSpace(line) Then Return False
            Dim s As String = line.Trim()
            Dim inv As CultureInfo = CultureInfo.InvariantCulture
            Dim parts As String() = s.Split(","c)
            If parts.Length = 2 AndAlso Single.TryParse(parts(0), NumberStyles.Float, inv, x) AndAlso
               Single.TryParse(parts(1), NumberStyles.Float, inv, y) Then
                raw = False
                Return True
            End If
            Dim ix As Integer = s.IndexOf("X:", StringComparison.OrdinalIgnoreCase)
            Dim iy As Integer = s.IndexOf("Y:", StringComparison.OrdinalIgnoreCase)
            Dim iz As Integer = s.IndexOf("Z:", StringComparison.OrdinalIgnoreCase)
            If ix < 0 OrElse iy < ix Then Return False
            Dim xText As String = s.Substring(ix + 2, iy - ix - 2).Trim()
            Dim yText As String = If(iz > iy, s.Substring(iy + 2, iz - iy - 2), s.Substring(iy + 2)).Trim()
            If Single.TryParse(xText, NumberStyles.Float, inv, x) AndAlso Single.TryParse(yText, NumberStyles.Float, inv, y) Then
                raw = True
                Return True
            End If
            Return False
        End Function

        Private Shared Sub ClosePort(sp As SerialPort)
            If sp Is Nothing Then Return
            Try
                sp.Close()
                sp.Dispose()
            Catch
            End Try
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            _stopping = True
            ClosePort(_port)
        End Sub
    End Class
End Namespace
