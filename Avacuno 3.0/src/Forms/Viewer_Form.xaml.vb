﻿#Region "IMPORTS"

Imports System.Drawing
Imports System.Windows
Imports System.Windows.Forms
Imports System.Windows.Controls
Imports System.Windows.Interop
Imports System.Runtime.InteropServices
Imports SMT.Multimedia.DirectShow
Imports SMT.Multimedia.Players.M2TS
Imports SMT.Win.WinAPI.Constants
Imports SMT.Multimedia.Enums

#End Region 'IMPORTS

Partial Public Class Viewer_Form

#Region "PROPERTIES"

    Private WithEvents ViewerHost As cVideoWindow
    Private Player As cM2TSPlayer
    Private DrainHandle As IntPtr

#End Region 'PROPERTIES

#Region "CONSTRUCTOR"

    Public Sub New(ByRef nPlayer As cM2TSPlayer, ByVal nDrainHandle As IntPtr)
        InitializeComponent()
        Player = nPlayer
        DrainHandle = nDrainHandle
    End Sub

#End Region 'CONSTRUCTOR

#Region "UI EVENTS"

    Private Sub ViewerWindow_KeyUp(ByVal sender As Object, ByVal e As System.Windows.Input.KeyEventArgs) Handles Me.KeyUp
        If e.Key = Key.Escape Then
            Me.CancelFullScreen()
        End If
    End Sub

    Private Sub Viewer_Loaded(ByVal sender As Object, ByVal e As System.Windows.RoutedEventArgs) Handles Me.Loaded
        Try
            ViewerHost = New cVideoWindow(Player.Graph.VideoWin, Me.bdViewer.ActualWidth, Me.bdViewer.ActualHeight, DrainHandle)
            Me.ViewerHost.Margin = New System.Windows.Thickness(0)
            'bdViewer.Child = ViewerHost
            Canvas.SetTop(ViewerHost, 0)
            Canvas.SetLeft(ViewerHost, 0)
            Me.cvViewer.Children.Add(ViewerHost)

            Canvas.SetTop(Me.bdVidWin, -2)
            Canvas.SetLeft(Me.bdVidWin, -2)

            imgIcon.Source = Me.Icon
            'AddHandler ViewerHost.MessageHook, AddressOf HandleWindowMessage
        Catch ex As Exception
            Throw New Exception("Problem with Viewer_Loaded(). Error: " & ex.Message)
        End Try
    End Sub

    Private Sub OnDragMoveWindow(ByVal sender As Object, ByVal e As MouseButtonEventArgs)
        Me.DragMove()
    End Sub

    Private Sub ViewerWindow_MouseDoubleClick(ByVal sender As Object, ByVal e As System.Windows.Input.MouseButtonEventArgs) Handles Me.MouseDoubleClick
        Me.SetFullScreen()
    End Sub

    Private Sub ViewerWindow_SizeChanged(ByVal sender As Object, ByVal e As System.Windows.SizeChangedEventArgs) Handles Me.SizeChanged
        Me.rtTitleBar.Width = Me.Width - 3
        Me.cvTitleBar.Width = Me.Width - 3
    End Sub

    'Private Sub Viewer_Closing(ByVal sender As Object, ByVal e As System.ComponentModel.CancelEventArgs) Handles Me.Closing
    '    RemoveHandler ViewerHost.MessageHook, AddressOf HandleWindowMessage
    'End Sub

    'Private Function HandleWindowMessage(ByVal hwnd As IntPtr, ByVal msg As Integer, ByVal wParam As IntPtr, ByVal lParam As IntPtr, ByRef handled As Boolean) As IntPtr
    '    handled = False
    '    Return IntPtr.Zero
    'End Function

#End Region 'UI EVENTS

#Region "VIEWER SIZE"

    Private VidWinSize As System.Windows.Size
    Public PreFS_Location As System.Windows.Point

    ''' <summary>
    ''' Provides available space for the video window.
    ''' Setting this sizes the appropriate controls to gurantee that the desired amount of space is available.
    ''' </summary>
    ''' <value></value>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Private Property ClientSize() As System.Drawing.Size
        Get
            Return New System.Drawing.Size(Me.bdViewer.ActualWidth, Me.bdViewer.ActualHeight)
        End Get
        Set(ByVal value As System.Drawing.Size)
            Dim wD As Integer = 0
            Dim hD As Integer = 0
            If InFullscreen Then
                Me.Width = value.Width
                Me.Height = value.Height
                wD = (Me.bdVidWin.BorderThickness.Left * 2) + 1
                _ClientRectangle = New System.Windows.Rect(0, 0, value.Width - (wD), value.Height)
            Else
                wD += Me.bdForm.BorderThickness.Left * 2
                hD += (Me.bdForm.BorderThickness.Top * 2) + 26

                'make space for the lime green video border
                wD += Me.bdVidWin.BorderThickness.Left * 2
                hD += Me.bdVidWin.BorderThickness.Top * 2

                'wD += 1
                'hD += 1
                wD -= 1
                hD -= 1

                Me.Width = value.Width + wD
                Me.Height = value.Height + hD

                _ClientRectangle = New System.Windows.Rect(0, 0, value.Width, value.Height)
            End If
        End Set
    End Property

    Private ReadOnly Property ClientRectangle() As System.Windows.Rect
        Get
            Return _ClientRectangle
            'Return New System.Windows.Rect(0, 0, Me.bdViewer.ActualWidth, Me.bdViewer.ActualHeight)
            'Return New System.Windows.Rect(0, 0, Me.ViewerHost.ActualWidth, Me.ViewerHost.ActualHeight)
            'Return New System.Windows.Rect(0, 0, VidWinSize.Width, VidWinSize.Height)
        End Get
    End Property
    Private _ClientRectangle As System.Windows.Rect

    Public Property ViewerSize() As eViewerSize
        Get
            Return _ViewerSize
        End Get
        Set(ByVal value As eViewerSize)
            If Not value = eViewerSize.Fullscreen And InFullscreen Then
                Me.CancelFullScreen()
            End If

            If value = eViewerSize.Fullscreen Then
                SetFullScreen()
            Else
                Dim s() As String = Split(value.ToString.ToLower, "_")
                Dim s1() As String = Split(s(UBound(s)), "x")
                Me.ClientSize = New System.Drawing.Size(s1(0), s1(1))
                If Player Is Nothing Then Exit Property
                If Player.Graph Is Nothing Then Exit Property
                If Player.Graph.VideoWin Is Nothing Then Exit Property
            End If

            _ViewerSize = value
            ScaleVideoWindow()

        End Set
    End Property
    Private _ViewerSize As eViewerSize = eViewerSize.HD_Quarter_480x270

    Public ReadOnly Property InFullscreen() As Boolean
        Get
            Return _InFullscreen 'Me.FormBorderStyle = Windows.Forms.FormBorderStyle.None
        End Get
    End Property
    Private _InFullscreen As Boolean = False

    Public Property PreFS_Size() As eViewerSize
        Get
            Return _PreFS_Size
        End Get
        Set(ByVal value As eViewerSize)
            If value = eViewerSize.Fullscreen Then Exit Property
            _PreFS_Size = value
        End Set
    End Property
    Private _PreFS_Size As eViewerSize

    Private Sub ScaleVideoWindow()
        Try
            Dim ClientAreaAR As Double = Math.Round(Me.ClientRectangle.Width / Me.ClientRectangle.Height, 2)
            Dim VideoAR As Double = Math.Round(Player.CurrentVideoDimensions.Width / Player.CurrentVideoDimensions.Height, 2)
            If ClientAreaAR <> VideoAR Then
                'we must scale
                Dim Ratio As Double = Me.ClientRectangle.Width / Player.CurrentVideoDimensions.Width
                Dim VidWinHeight As Integer = Math.Round(Player.CurrentVideoDimensions.Height * Ratio, 0)
                Dim VerticalOffset As Integer = Math.Abs(ClientRectangle.Height - VidWinHeight) / 2
                Player.Graph.VideoWin.SetWindowPosition(0, 0, ClientRectangle.Width, VidWinHeight)
                Me.ViewerHost.Width = ClientRectangle.Width
                Me.ViewerHost.Height = VidWinHeight
            Else
                'default
                Player.Graph.VideoWin.SetWindowPosition(0, 0, Me.ClientRectangle.Width, Me.ClientRectangle.Height)
                Me.ViewerHost.Width = ClientRectangle.Width
                Me.ViewerHost.Height = ClientRectangle.Height
            End If
            VidWinSize = New System.Windows.Size(Me.ViewerHost.Width, Me.ViewerHost.Height)
            Me.cvViewer.Width = VidWinSize.Width
            Me.cvViewer.Height = VidWinSize.Height

            Me.bdVidWin.Height = VidWinSize.Height + (Me.bdVidWin.BorderThickness.Top * 2)
            Me.bdVidWin.Width = VidWinSize.Width + (Me.bdVidWin.BorderThickness.Left * 2)

            Me.txtCaption.Text = "Viewer - " & Me.ViewerHost.Width & "x" & Me.ViewerHost.Height
        Catch ex As Exception
            Throw New Exception("Problem with ScaleVideoWindow(). Error: " & ex.Message, ex)
        End Try
    End Sub

    Private Sub SetFullScreen()
        Try
            PreFS_Location = New System.Windows.Point(Me.Left, Me.Top)
            PreFS_Size = ViewerSize
            Dim scr As System.Drawing.Rectangle = Screen.GetBounds(New System.Drawing.Point(Me.Left, Me.Top))
            _InFullscreen = True
            Me.ClientSize = New System.Drawing.Size(scr.Width, scr.Height)
            Me.Top = scr.Top '+ 15 '- 34
            Me.Left = scr.Left '+ 15 '- 5
            Me.cvTitleBar.Height = 0
            Me.bdTitleBar.Height = 0
            Me.bdForm.BorderThickness = New System.Windows.Thickness(0)
            Topmost = True
        Catch ex As Exception
            Throw New Exception("Problem with SetFullScreen(). Error: " & ex.Message, ex)
        End Try
    End Sub

    Public Sub CancelFullScreen()
        Try
            If Not InFullscreen Then Exit Sub
            _InFullscreen = False
            Me.Top = Me.PreFS_Location.Y
            Me.Left = Me.PreFS_Location.X
            Topmost = False
            ViewerSize = PreFS_Size
            Me.cvTitleBar.Height = 25.6
            Me.bdTitleBar.Height = 26
            Me.bdForm.BorderThickness = New System.Windows.Thickness(2.4)
        Catch ex As Exception
            Throw New Exception("Problem with CancelFullScreen(). Error: " & ex.Message, ex)
        End Try
    End Sub

#Region "Viewer Size:Context Menu"

    Private Sub RightClick() Handles ViewerHost.evRightClick
        'Me.bdViewer_ContextMenuOpening(Me.bdViewer, Nothing)
        Try
            Dim CM As New System.Windows.Controls.ContextMenu
            Dim tMI As System.Windows.Controls.MenuItem
            For Each s As String In [Enum].GetNames(GetType(eViewerSize))
                tMI = New System.Windows.Controls.MenuItem()
                tMI.Header = Replace(s, "_", " ")
                AddHandler tMI.Click, AddressOf ViewerSizeMenuClick
                CM.Items.Add(tMI)
            Next
            CM.PlacementTarget = Me.bdViewer
            CM.IsOpen = True
            'Me.bdViewer.ContextMenu = CM
        Catch ex As Exception
            Debug.WriteLine("Problem with bdViewer_ContextMenuOpening(). Error: " & ex.Message)
        End Try
    End Sub

    'Private Sub bdViewer_ContextMenuOpening(ByVal sender As Object, ByVal e As System.Windows.Controls.ContextMenuEventArgs) Handles bdViewer.ContextMenuOpening
    '    Try
    '        Dim CM As New System.Windows.Controls.ContextMenu
    '        Dim tMI As System.Windows.Controls.MenuItem
    '        For Each s As String In [Enum].GetNames(GetType(eViewerSize))
    '            tMI = New System.Windows.Controls.MenuItem()
    '            tMI.Header = Replace(s, "_", " ")
    '            AddHandler tMI.Click, AddressOf ViewerSizeMenuClick
    '            CM.Items.Add(tMI)
    '        Next
    '        CM.PlacementTarget = sender
    '        CM.IsOpen = True
    '        'Me.bdViewer.ContextMenu = CM
    '    Catch ex As Exception
    '        Debug.WriteLine("Problem with bdViewer_ContextMenuOpening(). Error: " & ex.Message)
    '    End Try
    'End Sub

    Private Sub ViewerSizeMenuClick(ByVal sender As Object, ByVal e As RoutedEventArgs)
        Try
            Dim MI As System.Windows.Controls.MenuItem = CType(sender, System.Windows.Controls.MenuItem)
            Me.ViewerSize = [Enum].Parse(GetType(eViewerSize), Replace(MI.Header, " ", "_"))
        Catch ex As Exception
            MsgBox("Problem with ViewerSizeMenuClick(). Error: " & ex.Message)
        End Try
    End Sub

    'Private Sub miNTSC_Half_Click(ByVal sender As System.Object, ByVal e As System.EventArgs)
    '    Me.ViewerSize = eViewerSize.HalfNTSC
    'End Sub

    'Private Sub miNTSC_Full_Click(ByVal sender As System.Object, ByVal e As System.EventArgs)
    '    Me.ViewerSize = eViewerSize.FullNTSC
    'End Sub

    'Private Sub miNTSC_Anamorphic_Click(ByVal sender As System.Object, ByVal e As System.EventArgs)
    '    Me.ViewerSize = eViewerSize.AnamorphicNTSC
    'End Sub

    'Private Sub miPAL_Half_Click(ByVal sender As System.Object, ByVal e As System.EventArgs)
    '    Me.ViewerSize = eViewerSize.HalfPAL
    'End Sub

    'Private Sub miPAL_Full_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles miPAL_Full.Click
    '    Me.ViewerSize = eViewerSize.FullPAL
    'End Sub

    'Private Sub miPAL_Anamorphic_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles miPAL_Anamorphic.Click
    '    Me.ViewerSize = eViewerSize.AnamorphicPAL
    'End Sub

    'Private Sub miHD_Quarter_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles miHD_Quarter.Click
    '    Me.ViewerSize = eViewerSize.QuarterHD
    'End Sub

    'Private Sub miHD_Half_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles miHD_Half.Click
    '    Me.ViewerSize = eViewerSize.HalfHD
    'End Sub

    'Private Sub miHD_Full_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles miHD_Full.Click
    '    Me.ViewerSize = eViewerSize.FullHD
    'End Sub

    'Private Sub miFullscreen_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles miFullscreen.Click
    '    Me.ViewerSize = eViewerSize.Fullscreen
    'End Sub

#End Region 'VIEWER SIZE:CONTEXT MENU

#End Region 'VIEWER SIZE

#Region "PLAYER EVENT HANDLING"

    Public Sub StreamEnd()
        Me.ViewerHost.Dispose()
        Me.cvViewer.Children.Clear()
    End Sub

#End Region 'PLAYER EVENT HANDLING

End Class
