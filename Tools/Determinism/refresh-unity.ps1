param()
$ErrorActionPreference = 'Stop'
$taskProjectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskProjectName = Split-Path -Leaf $taskProjectRoot
$taskEditors = @(Get-Process Unity -ErrorAction Stop | Where-Object { $_.MainWindowHandle -ne 0 -and $_.MainWindowTitle.StartsWith($taskProjectName + ' -') })
if ($taskEditors.Count -ne 1) { throw 'Cannot identify exactly one Unity window for this project.' }
Add-Type -TypeDefinition @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class AfpsUnityMenuBridge {
    [DllImport("user32.dll")] public static extern IntPtr GetMenu(IntPtr window);
    [DllImport("user32.dll")] public static extern int GetMenuItemCount(IntPtr menu);
    [DllImport("user32.dll")] public static extern IntPtr GetSubMenu(IntPtr menu, int index);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetMenuString(IntPtr menu, uint item, StringBuilder value, int maximum, uint flags);
    [DllImport("user32.dll")] public static extern uint GetMenuItemID(IntPtr menu, int index);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr window, uint message, IntPtr word, IntPtr data);
}
'@
$taskWindow = $taskEditors[0].MainWindowHandle
$taskMenu = [AfpsUnityMenuBridge]::GetMenu($taskWindow)
for ($taskIndex = 0; $taskIndex -lt [AfpsUnityMenuBridge]::GetMenuItemCount($taskMenu); $taskIndex++) {
    $taskLabel = New-Object Text.StringBuilder 256
    [void][AfpsUnityMenuBridge]::GetMenuString($taskMenu, $taskIndex, $taskLabel, 256, 1024)
    if ($taskLabel.ToString().Replace('&', '') -ne 'Assets') { continue }
    $taskAssets = [AfpsUnityMenuBridge]::GetSubMenu($taskMenu, $taskIndex)
    for ($taskItem = 0; $taskItem -lt [AfpsUnityMenuBridge]::GetMenuItemCount($taskAssets); $taskItem++) {
        $taskText = New-Object Text.StringBuilder 256
        [void][AfpsUnityMenuBridge]::GetMenuString($taskAssets, $taskItem, $taskText, 256, 1024)
        if ($taskText.ToString().Replace('&', '') -notmatch '^Refresh(?:\t|$)') { continue }
        $taskCommand = [AfpsUnityMenuBridge]::GetMenuItemID($taskAssets, $taskItem)
        if (-not [AfpsUnityMenuBridge]::PostMessage($taskWindow, 0x111, [IntPtr]([long]$taskCommand), [IntPtr]::Zero)) { throw 'Unable to request Unity refresh.' }
        Write-Output 'Unity Assets/Refresh requested for this project.'
        exit 0
    }
}
throw 'Unity Assets/Refresh menu entry was not found.'
