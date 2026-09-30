Imports System.Collections.Generic
Imports System.IO
Imports System.Windows.Media

Namespace Audio

    ''' <summary>
    ''' Plays WAV sound effects via WPF MediaPlayer. UI-thread only. Never throws.
    ''' Pure audio: no gameplay logic.
    ''' </summary>
    Public NotInheritable Class SoundManager
        Implements IDisposable

        Private Const PoolSize As Integer = 4

        Private NotInheritable Class Voice
            Public ReadOnly Pool As New List(Of MediaPlayer)()
            Public Next_ As Integer
            Public LoopPlayer As MediaPlayer
            Public Looping As Boolean
            Public Uri As Uri
        End Class

        Private ReadOnly _voices As New Dictionary(Of String, Voice)(StringComparer.OrdinalIgnoreCase)
        Private _muted As Boolean
        Private _master As Single = 0.8F
        Private _disposed As Boolean

        Public Sub New(soundsDirectory As String)
            Try
                If String.IsNullOrEmpty(soundsDirectory) OrElse Not Directory.Exists(soundsDirectory) Then Return
                For Each path In Directory.GetFiles(soundsDirectory, "*.wav")
                    Try
                        Dim v As New Voice With {.Uri = New Uri(System.IO.Path.GetFullPath(path))}
                        For i = 1 To PoolSize
                            v.Pool.Add(MakePlayer(v.Uri, 0))
                        Next
                        _voices(System.IO.Path.GetFileNameWithoutExtension(path)) = v
                    Catch
                    End Try
                Next
            Catch
            End Try
        End Sub

        Private Shared Function MakePlayer(u As Uri, vol As Double) As MediaPlayer
            Dim p As New MediaPlayer()
            p.Open(u)
            p.Volume = vol
            Return p
        End Function

        Public Property Muted As Boolean
            Get
                Return _muted
            End Get
            Set(value As Boolean)
                _muted = value
                Try
                    For Each v In _voices.Values
                        If v.LoopPlayer IsNot Nothing Then v.LoopPlayer.Volume = If(value, 0, _loopVol(v))
                    Next
                Catch
                End Try
            End Set
        End Property

        Public Property MasterVolume As Single
            Get
                Return _master
            End Get
            Set(value As Single)
                _master = Math.Max(0.0F, Math.Min(1.0F, value))
            End Set
        End Property

        Private ReadOnly _loopVols As New Dictionary(Of Voice, Single)()

        Private Function _loopVol(v As Voice) As Double
            Dim x As Single = 1.0F
            _loopVols.TryGetValue(v, x)
            Return Eff(x)
        End Function

        Private Function Eff(volume As Single) As Double
            If _muted Then Return 0.0
            Return Math.Max(0.0F, Math.Min(1.0F, volume)) * _master
        End Function

        Public Sub Play(name As String, Optional volume As Single = 1.0F)
            Try
                Dim v As Voice = Nothing
                If _disposed OrElse _muted OrElse name Is Nothing OrElse Not _voices.TryGetValue(name, v) Then Return
                Dim p = v.Pool(v.Next_)
                v.Next_ = (v.Next_ + 1) Mod v.Pool.Count
                p.Volume = Eff(volume)
                p.Position = TimeSpan.Zero
                p.Play()
            Catch
            End Try
        End Sub

        Public Sub StartLoop(name As String, Optional volume As Single = 1.0F)
            Try
                Dim v As Voice = Nothing
                If _disposed OrElse name Is Nothing OrElse Not _voices.TryGetValue(name, v) Then Return
                _loopVols(v) = volume
                If v.Looping Then
                    v.LoopPlayer.Volume = Eff(volume)
                    Return
                End If
                If v.LoopPlayer Is Nothing Then
                    v.LoopPlayer = MakePlayer(v.Uri, 0)
                    AddHandler v.LoopPlayer.MediaEnded,
                        Sub()
                            Try
                                If v.Looping Then
                                    v.LoopPlayer.Position = TimeSpan.Zero
                                    v.LoopPlayer.Play()
                                End If
                            Catch
                            End Try
                        End Sub
                End If
                v.Looping = True
                v.LoopPlayer.Volume = Eff(volume)
                v.LoopPlayer.Position = TimeSpan.Zero
                v.LoopPlayer.Play()
            Catch
            End Try
        End Sub

        Public Sub SetLoopVolume(name As String, volume As Single)
            Try
                Dim v As Voice = Nothing
                If name Is Nothing OrElse Not _voices.TryGetValue(name, v) Then Return
                _loopVols(v) = volume
                If v.Looping Then v.LoopPlayer.Volume = Eff(volume)
            Catch
            End Try
        End Sub

        Public Sub StopLoop(name As String)
            Try
                Dim v As Voice = Nothing
                If name Is Nothing OrElse Not _voices.TryGetValue(name, v) Then Return
                v.Looping = False
                If v.LoopPlayer IsNot Nothing Then v.LoopPlayer.Stop()
            Catch
            End Try
        End Sub

        Public Sub StopAll()
            Try
                For Each v In _voices.Values
                    v.Looping = False
                    If v.LoopPlayer IsNot Nothing Then v.LoopPlayer.Stop()
                    For Each p In v.Pool
                        p.Stop()
                    Next
                Next
            Catch
            End Try
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            If _disposed Then Return
            StopAll()
            _disposed = True
            Try
                For Each v In _voices.Values
                    For Each p In v.Pool
                        p.Close()
                    Next
                    If v.LoopPlayer IsNot Nothing Then v.LoopPlayer.Close()
                Next
                _voices.Clear()
            Catch
            End Try
        End Sub

    End Class

End Namespace
