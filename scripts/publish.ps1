<#
.SYNOPSIS
    发布 Windows x64 单文件产物，并生成可直接上传 GitHub Release 的资产。

.DESCRIPTION
    .NET 8 的 PublishSingleFile 默认不会把 native 依赖（wpfgfx_cor3.dll、
    PresentationNative_cor3.dll、e_sqlite3.dll 等）打进 exe，而是把它们留在
    exe 旁边。如果只把 exe 上传到 Release，用户下载后双击会先转圈、随后因
    DllNotFoundException 静默退出。

    本脚本统一加上 -p:IncludeNativeLibrariesForSelfExtract=true，让 native 依赖
    在运行时自动解压到 %TEMP%\.net\<进程名>\<hash>，保证单 exe 独立可运行。

.EXAMPLE
    pwsh scripts/publish.ps1 -Version 1.3.1
#>
param(
    [Parameter(Mandatory = $false)]
    [string]$Version = 'dev'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src/HeartRateMonitor.App/HeartRateMonitor.App.csproj'
if (-not (Test-Path $project)) { throw "找不到项目文件: $project" }

$baseArgs = @(
    '-c', 'Release',
    '-r', 'win-x64',
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:DebugType=None',
    '-p:DebugSymbols=false'
)

$targets = @(
    @{ Name = 'framework-dependent'; Display = '框架依赖'; Extra = @('--self-contained', 'false') },
    @{ Name = 'self-contained'; Display = '自包含'; Extra = @('--self-contained', 'true', '-p:EnableCompressionInSingleFile=true') }
)

$releaseDir = Join-Path $root 'publish/release'
if (Test-Path $releaseDir) { Remove-Item $releaseDir -Recurse -Force }
New-Item -ItemType Directory -Force $releaseDir | Out-Null

$results = @()
foreach ($target in $targets) {
    $outDir = Join-Path $root "publish/$($target.Name)"
    if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }
    New-Item -ItemType Directory -Force $outDir | Out-Null

    Write-Host "==> 发布$($target.Display)单文件到 $outDir"
    & dotnet publish $project @baseArgs @($target.Extra) -o $outDir
    if ($LASTEXITCODE -ne 0) { throw "发布失败: $($target.Name)" }

    $leftover = Get-ChildItem $outDir -File | Where-Object { $_.Extension -ne '.exe' }
    if ($leftover) {
        throw "发布目录里仍有非 exe 文件，说明 native 依赖没有打包进单文件: $($leftover.Name -join ', ')"
    }

    $exe = Get-ChildItem $outDir -Filter '*.exe' | Select-Object -First 1
    if (-not $exe) { throw "发布目录里没有 exe: $outDir" }

    $assetName = if ($target.Name -eq 'self-contained') {
        "HeartRate-$Version-win-x64-self-contained.exe"
    } else {
        "HeartRate-$Version-win-x64-framework-dependent.exe"
    }

    $assetPath = Join-Path $releaseDir $assetName
    Copy-Item $exe.FullName $assetPath -Force
    $sizeMb = [math]::Round((Get-Item $assetPath).Length / 1MB, 1)
    $results += [pscustomobject]@{ Asset = $assetName; SizeMB = $sizeMb; Path = $assetPath }
    Write-Host ("    -> {0} ({1} MB)" -f $assetName, $sizeMb)
}

Write-Host ''
Write-Host '发布完成，可上传的资产：'
$results | Format-Table -AutoSize
Write-Host "目录: $releaseDir"
