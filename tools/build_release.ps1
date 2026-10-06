[CmdletBinding()]
param(
    [string]$Version = "dev",
    [string]$Configuration = "Release",
    [string]$OutputRoot = "",
    [switch]$SkipBuild,
    [switch]$SkipNativeBuild,
    [switch]$NoZip
)

$ErrorActionPreference = "Stop"
if ($Version -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]*$' -or $Version.EndsWith('.')) { throw 'Version must be a safe single filename component' }

function Resolve-FullPath {
    param([string]$Path)
    $full = [System.IO.Path]::GetFullPath($Path)
    return $full.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
}

function Assert-UnderPath {
    param(
        [string]$ChildPath,
        [string]$ParentPath
    )
    $child = Resolve-FullPath $ChildPath
    $parent = Resolve-FullPath $ParentPath
    if (-not ($child.StartsWith($parent + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase))) {
        throw "Refuse to operate outside output root. Child=[$child], Parent=[$parent]"
    }
}

$repoRoot = Resolve-FullPath (Join-Path $PSScriptRoot "..")
$solutionPath = Join-Path $repoRoot "my_cartoon_beautiful\my_cartoon_beautiful.sln"
$projectDir = Join-Path $repoRoot "my_cartoon_beautiful"
$buildDir = Join-Path $projectDir "bin\$Configuration"

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot "artifacts\release"
}
$OutputRoot = Resolve-FullPath $OutputRoot
$stageDir = Join-Path $OutputRoot "my_cartoon_beautiful_$Version"
$zipPath = Join-Path $OutputRoot "my_cartoon_beautiful_$Version.zip"
$hashPath = "$zipPath.sha256"

Assert-UnderPath -ChildPath $stageDir -ParentPath $OutputRoot
Assert-UnderPath -ChildPath $zipPath -ParentPath $OutputRoot
Assert-UnderPath -ChildPath $hashPath -ParentPath $OutputRoot

Write-Host "[release] repo      : $repoRoot"
Write-Host "[release] version   : $Version"
Write-Host "[release] config    : $Configuration"
Write-Host "[release] output    : $OutputRoot"

if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot "prepare_native.ps1")
    if (-not $SkipNativeBuild) { & (Join-Path $PSScriptRoot "build_native.ps1") -Configuration $Configuration }
    Write-Host "[release] dotnet build..."
    dotnet build $solutionPath --configuration $Configuration -p:Platform=x64
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed with exit code $LASTEXITCODE"
    }
}

$exePath = Join-Path $buildDir "my_cartoon_beautiful.exe"
if (-not (Test-Path -LiteralPath $exePath)) {
    throw "Build output not found: $exePath"
}

New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null
if (Test-Path -LiteralPath $stageDir) {
    # 只清掉本次輸出資料夾，避免 release 與 artifacts 其他檔案被誤刪。
    Remove-Item -LiteralPath $stageDir -Recurse -Force
}
New-Item -ItemType Directory -Path $stageDir -Force | Out-Null

Write-Host "[release] staging files..."
Copy-Item -LiteralPath (Join-Path $buildDir "my_cartoon_beautiful.exe") -Destination $stageDir -Force
$configPath = Join-Path $buildDir "my_cartoon_beautiful.exe.config"
if (Test-Path -LiteralPath $configPath) {
    Copy-Item -LiteralPath $configPath -Destination $stageDir -Force
}
Get-ChildItem -LiteralPath $buildDir -Filter '*.dll' | Copy-Item -Destination $stageDir -Force
$nativeRoot = Join-Path $repoRoot 'artifacts/native-runtime'
& (Join-Path $PSScriptRoot 'prepare_native.ps1') -VerifyOnly -RuntimeRoot $nativeRoot
$nativeDestination = Join-Path $stageDir 'binary/native'
New-Item -ItemType Directory -Path $nativeDestination -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $nativeRoot 'ffmpeg'),(Join-Path $nativeRoot 'realesrgan') -Destination $nativeDestination -Recurse
$modelRoot = Join-Path $projectDir 'binary/realesrgan-ncnn-vulkan-v0.2.0-windows'
$modelDestination = Join-Path $stageDir 'binary/realesrgan-ncnn-vulkan-v0.2.0-windows'
New-Item -ItemType Directory -Path (Join-Path $modelDestination 'models') -Force | Out-Null
foreach($scale in 2..4) { foreach($ext in @('bin','param')) {
    Copy-Item -LiteralPath (Join-Path $modelRoot "models/realesr-animevideov3-x$scale.$ext") -Destination (Join-Path $modelDestination 'models')
} }
Copy-Item -LiteralPath (Join-Path $modelRoot 'LICENSE') -Destination $modelDestination
Copy-Item -LiteralPath (Join-Path $repoRoot 'native/licenses') -Destination $stageDir -Recurse
Copy-Item -LiteralPath (Join-Path $repoRoot 'native/THIRD-PARTY-NOTICES.md'),(Join-Path $repoRoot 'native/dependencies.lock.json'),(Join-Path $repoRoot 'native/ffmpeg-build-info.txt'),(Join-Path $repoRoot 'LICENSE') -Destination $stageDir
$provenance = [ordered]@{ schemaVersion=1; commit=(& git -C $repoRoot rev-parse HEAD); configuration=$Configuration; platform='x64'; builtUtc=[DateTime]::UtcNow.ToString('o'); files=@() }
$provenance.files = @(Get-ChildItem -LiteralPath $stageDir -Recurse -File | ForEach-Object { @{ path=$_.FullName.Substring($stageDir.Length+1).Replace('\','/'); sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() } })
$provenance | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $stageDir 'package-manifest.json') -Encoding utf8
& (Join-Path $PSScriptRoot 'verify_native_release.ps1') -PackagePath $stageDir -LayoutOnly

if (-not $NoZip) {
    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }
    if (Test-Path -LiteralPath $hashPath) {
        Remove-Item -LiteralPath $hashPath -Force
    }

    Write-Host "[release] zip..."
    Compress-Archive -Path (Join-Path $stageDir "*") -DestinationPath $zipPath -Force
    $hash = Get-FileHash -LiteralPath $zipPath -Algorithm SHA256
    "{0}  {1}" -f $hash.Hash.ToLowerInvariant(), (Split-Path -Leaf $zipPath) | Set-Content -LiteralPath $hashPath -Encoding UTF8
    Write-Host "[release] zip       : $zipPath"
    Write-Host "[release] sha256    : $hashPath"
}

Write-Host "[release] stage     : $stageDir"
Write-Host "[release] done"
