Option Strict On
Option Explicit On

Imports System
Imports System.IO
Imports System.Linq
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Drawing.Text

Namespace UI
    ' Bundled fonts (Bebas Neue, Rajdhani; SIL OFL) loaded from <exe>\Fonts.
    ' Sizes are in PIXELS. Returned Font objects are cached and shared:
    ' callers must NOT dispose them.
    Public NotInheritable Class GameFonts
        Public Enum FontWeight
            Medium
            SemiBold
            Bold
        End Enum

        Private Const Fallback As String = "Segoe UI"
        Private Shared ReadOnly sync As New Object()
        Private Shared collection As PrivateFontCollection   ' kept alive for app lifetime
        Private Shared loadedOk As Boolean
        Private Shared initDone As Boolean
        Private Shared ReadOnly cache As New Dictionary(Of String, Font)()

        Public Shared ReadOnly Property IsLoaded As Boolean
            Get
                SyncLock sync
                    EnsureLoaded()
                    Return loadedOk
                End SyncLock
            End Get
        End Property

        Public Shared Function Display(sizePx As Single) As Font
            Return GetFont("D", 0, sizePx)
        End Function

        Public Shared Function Body(sizePx As Single, Optional weight As FontWeight = FontWeight.Medium) As Font
            Return GetFont("B", CInt(weight), sizePx)
        End Function

        Private Shared Function GetFont(kind As String, weight As Integer, sizePx As Single) As Font
            SyncLock sync
                EnsureLoaded()
                Dim px As Single = CSng(Math.Max(1, Math.Round(sizePx)))
                Dim key As String = $"{kind}|{weight}|{px}"
                Dim f As Font = Nothing
                If Not cache.TryGetValue(key, f) Then
                    f = Build(kind, weight, px)
                    cache(key) = f
                End If
                Return f
            End SyncLock
        End Function

        Private Shared Function Build(kind As String, weight As Integer, px As Single) As Font
            Try
                If kind = "D" Then
                    Dim fam As FontFamily = FindFamily("bebasneue")
                    If fam IsNot Nothing Then Return New Font(fam, px, FontStyle.Regular, GraphicsUnit.Pixel)
                    Return New Font(Fallback, px, FontStyle.Bold, GraphicsUnit.Pixel)
                End If

                ' Rajdhani weights may be separate families or one family with styles
                Dim names As String() = {"rajdhanimedium", "rajdhanisemibold", "rajdhanibold"}
                Dim fw As FontFamily = FindFamily(names(weight))
                If fw IsNot Nothing Then
                    Dim st As FontStyle = FontStyle.Regular
                    If Not fw.IsStyleAvailable(st) Then st = FontStyle.Bold
                    Return New Font(fw, px, st, GraphicsUnit.Pixel)
                End If
                Dim baseFam As FontFamily = FindFamily("rajdhani")
                If baseFam IsNot Nothing Then
                    Dim st As FontStyle = If(weight = 2 AndAlso baseFam.IsStyleAvailable(FontStyle.Bold), FontStyle.Bold, FontStyle.Regular)
                    If Not baseFam.IsStyleAvailable(st) Then st = FontStyle.Bold
                    Return New Font(baseFam, px, st, GraphicsUnit.Pixel)
                End If
            Catch
                ' fall through to fallback
            End Try
            Return New Font(Fallback, px, If(weight >= 1, FontStyle.Bold, FontStyle.Regular), GraphicsUnit.Pixel)
        End Function

        ' Match a loaded family by name ignoring spaces and case
        Private Shared Function FindFamily(key As String) As FontFamily
            If collection Is Nothing Then Return Nothing
            For Each fam As FontFamily In collection.Families
                If fam.Name.Replace(" ", "").ToLowerInvariant() = key Then Return fam
            Next
            Return Nothing
        End Function

        ' Caller must hold sync
        Private Shared Sub EnsureLoaded()
            If initDone Then Return
            initDone = True
            Try
                Dim dir As String = Path.Combine(AppContext.BaseDirectory, "Fonts")
                If Not Directory.Exists(dir) Then Return
                Dim pfc As New PrivateFontCollection()
                For Each f As String In Directory.GetFiles(dir, "*.ttf")
                    pfc.AddFontFile(f)
                Next
                collection = pfc
                loadedOk = pfc.Families.Length > 0
            Catch
                loadedOk = False
            End Try
        End Sub

        ' Debug helper: names of loaded families
        Public Shared Function LoadedFamilyNames() As String()
            SyncLock sync
                EnsureLoaded()
                If collection Is Nothing Then Return New String() {}
                Return collection.Families.Select(Function(x) x.Name).ToArray()
            End SyncLock
        End Function
    End Class
End Namespace
