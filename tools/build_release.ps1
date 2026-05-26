[CmdletBinding()]
param(
    [string]$Version = "dev",
    [string]$Configuration = "Release",
    [string]$OutputRoot = "",
    [switch]$SkipBuild,
    [switch]$NoZip
)

$ErrorActionPreference = "Stop"

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
    if (-not ($child.Equals($parent, [System.StringComparison]::OrdinalIgnoreCase) -or $child.StartsWith($parent + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase))) {
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
    Write-Host "[release] dotnet build..."
    dotnet build $solutionPath --configuration $Configuration
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
$binaryPath = Join-Path $buildDir "binary"
if (Test-Path -LiteralPath $binaryPath) {
    Copy-Item -LiteralPath $binaryPath -Destination $stageDir -Recurse -Force
}

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
