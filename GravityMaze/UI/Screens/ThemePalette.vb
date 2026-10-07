Option Strict On
Option Explicit On

Imports System.Drawing

Namespace UI.Screens
    ' Colours and copy that make each level's screens (intro, HUD, results) feel like part of that level.
    Public NotInheritable Class ThemePalette
        Public ReadOnly Property Accent As Color       ' highlights, selected item, big numbers
        Public ReadOnly Property AccentSoft As Color   ' glows, secondary highlights
        Public ReadOnly Property Text As Color
        Public ReadOnly Property TextDim As Color
        Public ReadOnly Property Panel As Color        ' panel fill (alpha included)
        Public ReadOnly Property Backdrop As Color     ' form / canvas background
        Public ReadOnly Property Tagline As String

        Private Sub New(accent As Color, accentSoft As Color, text As Color, textDim As Color,
                        panel As Color, backdrop As Color, tagline As String)
            Me.Accent = accent
            Me.AccentSoft = accentSoft
            Me.Text = text
            Me.TextDim = textDim
            Me.Panel = panel
            Me.Backdrop = backdrop
            Me.Tagline = tagline
        End Sub

        ' Neutral brand palette for the title and global menus.
        Public Shared ReadOnly Brand As New ThemePalette(
            Color.FromArgb(255, 196, 64), Color.FromArgb(255, 140, 40),
            Color.FromArgb(242, 238, 230), Color.FromArgb(150, 146, 160),
            Color.FromArgb(215, 14, 12, 20), Color.FromArgb(10, 9, 14), "")

        Public Shared Function ForTheme(themeName As String) As ThemePalette
            Select Case themeName
                Case "Forgotten Keep"
                    Return New ThemePalette(
                        Color.FromArgb(255, 177, 73), Color.FromArgb(179, 122, 255),
                        Color.FromArgb(241, 230, 251), Color.FromArgb(168, 146, 185),
                        Color.FromArgb(225, 23, 16, 33), Color.FromArgb(15, 11, 23),
                        "Two seals. One final escape.")
                Case "Frozen Labyrinth"
                    Return New ThemePalette(
                        Color.FromArgb(120, 220, 255), Color.FromArgb(60, 160, 240),
                        Color.FromArgb(232, 244, 255), Color.FromArgb(140, 170, 200),
                        Color.FromArgb(220, 8, 16, 32), Color.FromArgb(8, 14, 26),
                        "The floor is ice. Brake early.")
                Case "Neon Velocity"
                    Return New ThemePalette(
                        Color.FromArgb(255, 70, 210), Color.FromArgb(0, 230, 255),
                        Color.FromArgb(245, 236, 255), Color.FromArgb(160, 140, 190),
                        Color.FromArgb(220, 14, 8, 28), Color.FromArgb(10, 8, 20),
                        "Boosts launch you. Pits don't forgive.")
                Case Else
                    Return New ThemePalette(
                        Color.FromArgb(255, 190, 110), Color.FromArgb(220, 130, 60),
                        Color.FromArgb(246, 236, 220), Color.FromArgb(180, 160, 135),
                        Color.FromArgb(220, 26, 18, 12), Color.FromArgb(24, 20, 17),
                        "Tilt the board. Find the exit.")
            End Select
        End Function
    End Class
End Namespace
