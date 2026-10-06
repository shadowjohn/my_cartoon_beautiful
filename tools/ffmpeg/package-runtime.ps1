[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$BuildRoot,
    [Parameter(Mandatory=$true)][string]$DependencyNotices
)
$ErrorActionPreference='Stop'
$BuildRoot=(Resolve-Path -LiteralPath $BuildRoot).Path
$DependencyNotices=(Resolve-Path -LiteralPath $DependencyNotices).Path
if (!(Get-Item -LiteralPath $DependencyNotices).PSIsContainer -or !(Get-ChildItem -LiteralPath $DependencyNotices -File -Recurse | Select-Object -First 1)) { throw 'Dependency license notices are missing' }
if (!(Test-Path -LiteralPath (Join-Path $BuildRoot 'SUCCESS.txt'))) { throw 'Build completion marker is missing' }
$names=@('avcodec-63.dll','avdevice-63.dll','avfilter-12.dll','avformat-63.dll','avutil-61.dll','swresample-7.dll','swscale-10.dll')
$bins=@(Get-ChildItem -LiteralPath (Join-Path $BuildRoot 'bin') -Filter '*.dll')
if (@(Compare-Object ($bins.Name|Sort-Object) ($names|Sort-Object)).Count) { throw 'Unexpected FFmpeg runtime DLL set' }
Add-Type -TypeDefinition @"
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
public static class FfmpegReleaseMetadata {
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    private static extern IntPtr LoadLibraryExW(string path, IntPtr file, uint flags);
    [DllImport("kernel32.dll", CharSet=CharSet.Ansi, SetLastError=true)]
    private static extern IntPtr GetProcAddress(IntPtr module, string name);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr GetText();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate uint GetVersion();
    public static IntPtr Load(string path) {
        var module=LoadLibraryExW(path,IntPtr.Zero,8);
        if(module==IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(),path);
        return module;
    }
    public static string ReadText(IntPtr module,string name) {
        var address=GetProcAddress(module,name);
        if(address==IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(),name);
        return Marshal.PtrToStringAnsi(((GetText)Marshal.GetDelegateForFunctionPointer(address,typeof(GetText)))());
    }
    public static uint ReadVersion(IntPtr module,string name) {
        var address=GetProcAddress(module,name);
        if(address==IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(),name);
        return ((GetVersion)Marshal.GetDelegateForFunctionPointer(address,typeof(GetVersion)))();
    }
}
"@
$metadata=@()
foreach ($entry in @(@{Name='avutil-61.dll';Prefix='avutil';Major=61},@{Name='avcodec-63.dll';Prefix='avcodec';Major=63})) {
    $module=[FfmpegReleaseMetadata]::Load((Join-Path (Join-Path $BuildRoot 'bin') $entry.Name))
    if ($entry.Prefix -eq 'avutil') { $versionInfo=[FfmpegReleaseMetadata]::ReadText($module,'av_version_info') }
    $license=[FfmpegReleaseMetadata]::ReadText($module,($entry.Prefix+'_license'))
    $configuration=[FfmpegReleaseMetadata]::ReadText($module,($entry.Prefix+'_configuration'))
    $version=[FfmpegReleaseMetadata]::ReadVersion($module,($entry.Prefix+'_version'))
    if (($version -shr 16) -ne $entry.Major) { throw "Wrong ABI: $($entry.Name)" }
    if ($license -ne 'LGPL version 3 or later') { throw "Unexpected FFmpeg license: $license" }
    if ($configuration -match '(?:^| )--enable-(?:gpl|nonfree|chromaprint|vapoursynth)(?: |$)') { throw 'A forbidden configure option is enabled' }
    foreach($disabled in @('gpl','nonfree','chromaprint','vapoursynth')) {
        if ($configuration -notmatch "(?:^| )--disable-$disabled(?: |$)") { throw "Missing explicit disable: $disabled" }
    }
    $metadata += [ordered]@{library=$entry.Name;version=$version;versionInfo=$versionInfo;license=$license;configuration=$configuration}
}
$metadata | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $BuildRoot 'exported-metadata.json') -Encoding UTF8
$manifest=[ordered]@{
    schemaVersion=1
    sourceRevision='2a571b606854520cf89804d8030c8b328e621689'
    sourceArchiveSha256='25c3b714ebbb3d43ffd2a5126d8ce34217dade0d8cd4829c618666e3e19b3858'
    dependencyRecipeRevision='9acad4a9ef1583096af7836cc1e9c8cbcb4d3950'
    dependencyImage='ghcr.io/btbn/ffmpeg-builds/win64-lgpl-shared-9.0@sha256:e0b0c4e3ff1dc7f5529b6174398c212dfa1358ec9f9a802e22e5cacba872032b'
    disabled=@('chromaprint','fftw','lcms2_fast_float','lcms2_threaded','vapoursynth','gpl','nonfree')
    dlls=@($bins | Sort-Object Name | ForEach-Object { [ordered]@{name=$_.Name;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()} })
}
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $BuildRoot 'build-manifest.json') -Encoding UTF8
$name='ffmpeg-n9.0.2-17-g2a571b6068-win64-lgpl-shared-no-gpl-deps-v1'
$stage=Join-Path $BuildRoot $name
if(Test-Path -LiteralPath $stage){throw "Package staging directory already exists: $stage"}
New-Item -ItemType Directory -Path (Join-Path $stage 'bin') -Force | Out-Null
foreach($dll in $bins){Copy-Item -LiteralPath $dll.FullName -Destination (Join-Path $stage 'bin')}
foreach($item in @('build-manifest.json','exported-metadata.json','dependency-image.json','provenance.txt','dll-sha256.txt','dll-imports.txt','removed-gpl-components.txt','dependency-archive-sha256.txt','dependency-pkg-config.txt','config.h','config.mak','COPYING.LGPLv2.1','COPYING.LGPLv3','COPYING.GPLv2','COPYING.GPLv3','LICENSE.md')) {
    Copy-Item -LiteralPath (Join-Path $BuildRoot $item) -Destination $stage
}
New-Item -ItemType Directory -Path (Join-Path $stage 'licenses') | Out-Null
Copy-Item -LiteralPath $DependencyNotices -Destination (Join-Path $stage 'licenses/dependencies') -Recurse
$archive=Join-Path $BuildRoot ($name+'.zip')
Compress-Archive -LiteralPath $stage -DestinationPath $archive -CompressionLevel Optimal
$archiveHash=(Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText(($archive+'.sha256'),($archiveHash+'  '+[IO.Path]::GetFileName($archive)+[Environment]::NewLine),(New-Object Text.UTF8Encoding($false)))
Write-Output "Runtime archive: $archive"
Write-Output "SHA256: $archiveHash"
