Attribute VB_Name = "StoneMaster"
Option Explicit

Public Sub ShowStoneMaster()
    On Error GoTo EH
    Application.FrameWork.AddDocker "B0F8A8D1-11D8-4F65-A3B3-05E7F20A3A01", _
        "StoneMaster.Corel.Docker.StoneDocker", _
        "Addons\StoneMaster\StoneMaster.Corel.dll"
    Application.FrameWork.ShowDocker "B0F8A8D1-11D8-4F65-A3B3-05E7F20A3A01"
    Exit Sub
EH:
    MsgBox "StoneMaster açılamadı: " & Err.Description, vbExclamation
End Sub
