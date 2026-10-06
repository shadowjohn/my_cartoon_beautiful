[CmdletBinding()]
param([Parameter(Mandatory)][string]$PackagePath,[switch]$RequireGpu,[switch]$LayoutOnly,[string]$Fixture='')
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$PackagePath=[IO.Path]::GetFullPath($PackagePath)
$required=@('my_cartoon_beautiful.exe','my_cartoon_beautiful.exe.config','System.Resources.Extensions.dll','System.Memory.dll','System.Buffers.dll','System.Numerics.Vectors.dll','System.Runtime.CompilerServices.Unsafe.dll','FFmpeg.AutoGen.dll','binary/native/realesrgan/realesrgan_bridge.dll','THIRD-PARTY-NOTICES.md','dependencies.lock.json','package-manifest.json','licenses/FFmpeg-COPYING.LGPLv3','licenses/FFmpeg-COPYING.GPLv3','licenses/FFmpeg.AutoGen-LICENSE.txt')
foreach($file in $required){if(!(Test-Path -LiteralPath (Join-Path $PackagePath $file) -PathType Leaf)){throw "Missing package file: $file"}}
foreach($scale in 2..4){foreach($ext in @('bin','param')){if(!(Test-Path -LiteralPath (Join-Path $PackagePath "binary/realesrgan-ncnn-vulkan-v0.2.0-windows/models/realesr-animevideov3-x$scale.$ext"))){throw "Missing x$scale model .$ext"}}}
$forbidden=@(Get-ChildItem -LiteralPath (Join-Path $PackagePath 'binary') -Recurse -File | Where-Object {$_.Extension -eq '.exe' -or $_.Name -match '^vcomp140d?\.dll$'})
if($forbidden.Count){throw "Legacy executables/OpenMP runtime found: $($forbidden.FullName -join ', ')"}
& (Join-Path $PSScriptRoot 'prepare_native.ps1') -VerifyOnly -RuntimeRoot (Join-Path $PackagePath 'binary/native')
$manifest=Get-Content -LiteralPath (Join-Path $PackagePath 'package-manifest.json') -Raw | ConvertFrom-Json
foreach($item in $manifest.files) {
    $file=[IO.Path]::GetFullPath((Join-Path $PackagePath $item.path))
    if(!$file.StartsWith($PackagePath.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Manifest path escapes package'}
    if(!(Test-Path -LiteralPath $file) -or (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $item.sha256){throw "Package hash mismatch: $($item.path)"}
}
if($LayoutOnly){Write-Output 'Native release layout/hash PASS';return}
if(!$Fixture){$Fixture=Join-Path $repo 'artifacts/media-tests/fixtures/sync-30fps.mp4'}
$Fixture=[IO.Path]::GetFullPath($Fixture)
if(!(Test-Path -LiteralPath $Fixture)){throw 'Create media fixtures before running release smoke'}
$runner=Join-Path $repo 'tests/NativeMediaSmokeTests/bin/Release/net472/NativeMediaSmokeTests.exe'
if(!(Test-Path -LiteralPath $runner)){throw 'Build NativeMediaSmokeTests before release smoke'}
$result=Join-Path $repo ('artifacts/acceptance/package-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $result -Force | Out-Null
$arguments=@('--release',$PackagePath,$Fixture,$result)
if($RequireGpu){$arguments+='--gpu'}
Push-Location ([IO.Path]::GetTempPath())
try { & $runner @arguments; if($LASTEXITCODE -ne 0){throw 'Isolated release smoke failed'} } finally {Pop-Location}
Write-Output "Native release smoke PASS; GPU required=$RequireGpu; evidence=$result"
