<#
.SYNOPSIS
    为 HeartRate Monitor（心率监控）创建/删除快捷启动图标。

.DESCRIPTION
    在桌面和开始菜单创建带应用图标的快捷方式，指向已发布的 HeartRateMonitor.App.exe。
    自动按以下顺序查找可执行文件（可用 -ExePath 显式指定）：
      1. publish\self-contained\HeartRateMonitor.App.exe      （免安装，推荐）
      2. publish\framework-dependent\HeartRateMonitor.App.exe （需 .NET 8 桌面运行时）
      3. src\...\bin\Release\...\win-x64\...                  （Release 构建产物）
      4. src\...\bin\Debug\...                                （Debug 构建产物）

.PARAMETER ExePath
    HeartRateMonitor.App.exe 的完整路径。

.PARAMETER IconPath
    图标文件路径，默认使用 src\HeartRateMonitor.App\Assets\app.ico。

.PARAMETER ShortcutName
    快捷方式显示名称，默认“心率监控”。

.PARAMETER AutoStart
    额外将应用写入 HKCU\Software\Microsoft\Windows\CurrentVersion\Run，实现开机自启。

.PARAMETER Uninstall
    删除本脚本创建的桌面/开始菜单快捷方式（以及 -AutoStart 写入的注册表项）。

.EXAMPLE
    pwsh -File scripts\Install-Shortcut.ps1
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\Install-Shortcut.ps1 -AutoStart
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\Install-Shortcut.ps1 -Uninstall
#>

[CmdletBinding()]
param(
    [string]$ExePath,
    [string]$IconPath,
    [string]$ShortcutName = '心率监控',
    [switch]$NoDesktop,
    [switch]$NoStartMenu,
    [switch]$AutoStart,
    [switch]$Uninstall
)

$ErrorActionPreference = 'Stop'

$Root = Split-Path -Parent $PSScriptRoot
$RunKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$RunValueName = 'HeartRateMonitor'

$DesktopDir = [Environment]::GetFolderPath('Desktop')
$StartMenuDir = Join-Path ([Environment]::GetFolderPath('Programs')) $ShortcutName
$DesktopLnk = Join-Path $DesktopDir "$ShortcutName.lnk"
$StartMenuLnk = Join-Path $StartMenuDir "$ShortcutName.lnk"

function Resolve-AppExe {
    param([string]$Explicit)

    if ($Explicit) {
        if (-not (Test-Path -LiteralPath $Explicit)) { throw "指定的可执行文件不存在: $Explicit" }
        return (Resolve-Path -LiteralPath $Explicit).Path
    }

    $candidates = @(
        (Join-Path $Root 'publish\self-contained\HeartRateMonitor.App.exe'),
        (Join-Path $Root 'publish\framework-dependent\HeartRateMonitor.App.exe'),
        (Join-Path $Root 'src\HeartRateMonitor.App\bin\Release\net8.0-windows10.0.22621.0\win-x64\HeartRateMonitor.App.exe'),
        (Join-Path $Root 'src\HeartRateMonitor.App\bin\Debug\net8.0-windows10.0.22621.0\HeartRateMonitor.App.exe')
    )
    foreach ($c in $candidates) {
        if (Test-Path -LiteralPath $c) { return (Resolve-Path -LiteralPath $c).Path }
    }
    throw "未找到 HeartRateMonitor.App.exe，请先执行 dotnet publish，或用 -ExePath 指定路径。"
}

# ---------------------------------------------------------------- 卸载
if ($Uninstall) {
    foreach ($p in @($DesktopLnk, $StartMenuLnk)) {
        if (Test-Path -LiteralPath $p) {
            Remove-Item -LiteralPath $p -Force
            Write-Host "已删除快捷方式: $p"
        }
    }
    if (Test-Path -LiteralPath $StartMenuDir) {
        $left = Get-ChildItem -LiteralPath $StartMenuDir -Force
        if (-not $left) { Remove-Item -LiteralPath $StartMenuDir -Force }
    }
    if (Get-ItemProperty -Path $RunKey -Name $RunValueName -ErrorAction SilentlyContinue) {
        Remove-ItemProperty -Path $RunKey -Name $RunValueName -Force
        Write-Host "已移除开机自启注册表项: $RunKey\$RunValueName"
    }
    Write-Host '卸载完成。' -ForegroundColor Green
    return
}

# ---------------------------------------------------------------- 定位文件
$exe = Resolve-AppExe -Explicit $ExePath
if (-not $IconPath) { $IconPath = Join-Path $Root 'src\HeartRateMonitor.App\Assets\app.ico' }
$iconSource = if (Test-Path -LiteralPath $IconPath) { (Resolve-Path -LiteralPath $IconPath).Path } else { $exe }

Write-Host "可执行文件 : $exe"
Write-Host "图标来源   : $iconSource"

# ---------------------------------------------------------------- 创建快捷方式
$shell = New-Object -ComObject WScript.Shell

function New-AppShortcut {
    param([string]$LnkPath)

    $dir = Split-Path -Parent $LnkPath
    if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    if (Test-Path -LiteralPath $LnkPath) { Remove-Item -LiteralPath $LnkPath -Force }

    $sc = $shell.CreateShortcut($LnkPath)
    $sc.TargetPath = $exe
    $sc.WorkingDirectory = Split-Path -Parent $exe
    $sc.IconLocation = "$iconSource,0"
    $sc.Description = '心率监控 - BLE 心率实时监测'
    $sc.WindowStyle = 1
    $sc.Save()
    Write-Host "已创建快捷方式: $LnkPath" -ForegroundColor Green
}

if (-not $NoDesktop) { New-AppShortcut -LnkPath $DesktopLnk }
if (-not $NoStartMenu) { New-AppShortcut -LnkPath $StartMenuLnk }

if ($AutoStart) {
    New-ItemProperty -Path $RunKey -Name $RunValueName -Value "`"$exe`"" -PropertyType String -Force | Out-Null
    Write-Host "已设置开机自启: $RunKey\$RunValueName -> $exe" -ForegroundColor Green
}

[System.Runtime.InteropServices.Marshal]::ReleaseComObject($shell) | Out-Null
Write-Host '完成。现在可以双击桌面上的“心率监控”图标启动应用。' -ForegroundColor Cyan
