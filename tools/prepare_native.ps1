[CmdletBinding()]
param([switch]$VerifyOnly, [string]$RuntimeRoot='')
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if(!$RuntimeRoot){$RuntimeRoot=Join-Path $repo 'artifacts/native-runtime'}
$RuntimeRoot=[IO.Path]::GetFullPath($RuntimeRoot)
$manifest=Get-Content -LiteralPath (Join-Path $repo 'native/dependencies.lock.json') -Raw | ConvertFrom-Json
$runtime=Join-Path $RuntimeRoot 'ffmpeg'
function Assert-Hash([string]$File,[string]$Expected){
    if(!(Test-Path -LiteralPath $File -PathType Leaf)){throw "Missing native dependency: $File"}
    if((Get-FileHash -LiteralPath $File -Algorithm SHA256).Hash -ne $Expected){throw "Native dependency hash mismatch: $File"}
}
if(!$VerifyOnly){
    $cache=Join-Path $repo 'artifacts/native-cache'
    New-Item -ItemType Directory -Force -Path $cache,$runtime | Out-Null
    $archive=Join-Path $cache 'ffmpeg-9.0-lgpl-shared.zip'
    if(!(Test-Path -LiteralPath $archive)){
        Invoke-WebRequest -Uri $manifest.ffmpeg.url -OutFile ($archive+'.partial')
        Assert-Hash ($archive+'.partial') $manifest.ffmpeg.sha256
        Move-Item -LiteralPath ($archive+'.partial') -Destination $archive
    }
    Assert-Hash $archive $manifest.ffmpeg.sha256
    $extracted=Join-Path $cache $manifest.ffmpeg.sha256
    if(!(Test-Path -LiteralPath $extracted)){Expand-Archive -LiteralPath $archive -DestinationPath $extracted}
    $bins=@(Get-ChildItem -LiteralPath $extracted -Directory | ForEach-Object {Join-Path $_.FullName 'bin'} | Where-Object {Test-Path -LiteralPath $_})
    if($bins.Count -ne 1){throw 'Unexpected FFmpeg archive layout'}
    foreach($dll in $manifest.ffmpeg.dlls){
        $source=Join-Path $bins[0] $dll.name
        Assert-Hash $source $dll.sha256
        Copy-Item -LiteralPath $source -Destination (Join-Path $runtime $dll.name) -Force
    }
}
foreach($dll in $manifest.ffmpeg.dlls){Assert-Hash (Join-Path $runtime $dll.name) $dll.sha256}
Write-Output "FFmpeg dependencies verified: $runtime"
