[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$SourceArchive,
    [string]$OutputRoot = '',
    [ValidateRange(1,64)][int]$Jobs = 8,
    [string]$ContainerName = 'my-cartoon-ffmpeg-lgpl-v005'
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (!$OutputRoot) { $OutputRoot = Join-Path $repo 'artifacts/lgpl-ffmpeg-rebuild' }
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
$SourceArchive = (Resolve-Path -LiteralPath $SourceArchive).Path
$image = 'ghcr.io/btbn/ffmpeg-builds/win64-lgpl-shared-9.0@sha256:e0b0c4e3ff1dc7f5529b6174398c212dfa1358ec9f9a802e22e5cacba872032b'
$expectedSource = '25c3b714ebbb3d43ffd2a5126d8ce34217dade0d8cd4829c618666e3e19b3858'
if ((Get-FileHash -LiteralPath $SourceArchive -Algorithm SHA256).Hash -ne $expectedSource) { throw 'FFmpeg source archive SHA-256 mismatch' }
if (Test-Path -LiteralPath (Join-Path $OutputRoot 'SUCCESS.txt')) { throw 'Output already contains a completed build. Select a new OutputRoot.' }
New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null
& docker pull $image
if ($LASTEXITCODE) { throw 'Pinned FFmpeg dependency image could not be pulled' }
& docker image inspect $image | Set-Content -LiteralPath (Join-Path $OutputRoot 'dependency-image.json') -Encoding UTF8
if ($LASTEXITCODE) { throw 'Could not inspect dependency image' }
$arguments = @('run', '--name', $ContainerName,
    '--mount', "type=bind,source=$OutputRoot,target=/out",
    '--mount', "type=bind,source=$SourceArchive,target=/input/ffmpeg.zip,readonly",
    '--mount', "type=bind,source=$PSScriptRoot,target=/scripts,readonly",
    '--env', "BUILD_JOBS=$Jobs", $image, 'bash', '/scripts/build-in-container.sh')
& docker @arguments
if ($LASTEXITCODE) { throw "FFmpeg build failed. Inspect $OutputRoot/build.log and retained container $ContainerName." }
if (!(Test-Path -LiteralPath (Join-Path $OutputRoot 'SUCCESS.txt'))) { throw 'FFmpeg build did not produce success marker' }
$dlls = @(Get-ChildItem -LiteralPath (Join-Path $OutputRoot 'bin') -Filter '*.dll')
$expectedNames = @('avcodec-63.dll','avdevice-63.dll','avfilter-12.dll','avformat-63.dll','avutil-61.dll','swresample-7.dll','swscale-10.dll')
if (@(Compare-Object ($dlls.Name | Sort-Object) ($expectedNames | Sort-Object)).Count) { throw 'Unexpected FFmpeg DLL ABI majors' }
$manifest = [ordered]@{
    schemaVersion = 1
    sourceRevision = '2a571b606854520cf89804d8030c8b328e621689'
    sourceArchiveSha256 = $expectedSource
    dependencyRecipeRevision = '9acad4a9ef1583096af7836cc1e9c8cbcb4d3950'
    dependencyImage = $image
    disabled = @('chromaprint','fftw','lcms2_fast_float','lcms2_threaded','vapoursynth','gpl','nonfree')
    dlls = @($dlls | Sort-Object Name | ForEach-Object { [ordered]@{ name=$_.Name; sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() } })
}
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $OutputRoot 'build-manifest.json') -Encoding UTF8
Write-Output "FFmpeg LGPL build completed: $OutputRoot"
# The container is retained for audit or incremental investigation. This script never deletes other Docker data.
