[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$manifest=Get-Content -LiteralPath (Join-Path $repo 'native/dependencies.lock.json') -Raw | ConvertFrom-Json
$source=$manifest.realesrgan.referenceSource
$cache=Join-Path $repo 'artifacts/native-cache'
$archive=Join-Path $cache $source.file
if(!(Test-Path -LiteralPath $archive)){Invoke-WebRequest -Uri $source.url -OutFile $archive}
if((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $source.sha256){throw 'Reference source hash mismatch'}
$extracted=Join-Path $repo 'artifacts/native-source/reference'
New-Item -ItemType Directory -Force -Path $extracted | Out-Null
& tar -xf $archive -C $extracted
if($LASTEXITCODE -ne 0){throw 'Reference source extraction failed'}
$vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$vs=& $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
$cmake=Join-Path $vs 'Common7/IDE/CommonExtensions/Microsoft/CMake/CMake/bin/cmake.exe'
$build=Join-Path $repo 'artifacts/reference-build'
$nativeBuild=Join-Path $repo 'artifacts/native-build'
$inputSources=@{}
foreach($item in $manifest.realesrgan.sources){if($item.root){$inputSources[$item.name]=Join-Path $repo "artifacts/native-source/$($item.name)/$($item.root)"}}
& $cmake -S (Join-Path $repo 'native/realesrgan_reference') -B $build -A x64 `
 "-DUPSTREAM_SOURCE=$(Join-Path $extracted "$($source.root)/src")" "-DNCNN_SOURCE=$($inputSources.ncnn)" `
 "-DNATIVE_BUILD=$nativeBuild" "-DVULKAN_INCLUDE=$(Join-Path $inputSources.vulkanHeaders 'include')" "-DVULKAN_LIBRARY=$(Join-Path $cache 'vulkan-1.lib')"
if($LASTEXITCODE -ne 0){throw 'Reference configuration failed; build native Release first'}
& $cmake --build $build --config Release --parallel 4
if($LASTEXITCODE -ne 0){throw 'Reference build failed'}
Add-Type -AssemblyName System.Drawing
$output=Join-Path $repo 'artifacts/media-tests/reference'
New-Item -ItemType Directory -Force -Path $output | Out-Null
$bitmap=[Drawing.Bitmap]::new((Join-Path $repo 'artifacts/media-tests/baseline/sync-30fps-frames/00000001.png'))
try{
 if($bitmap.Width -ne 96 -or $bitmap.Height -ne 64){throw 'Expected standard 96x64 baseline fixture'}
 $bytes=[byte[]]::new(96*64*3)
 for($y=0;$y -lt 64;$y++){for($x=0;$x -lt 96;$x++){$p=$bitmap.GetPixel($x,$y);$i=($y*96+$x)*3;$bytes[$i]=$p.B;$bytes[$i+1]=$p.G;$bytes[$i+2]=$p.R}}
 [IO.File]::WriteAllBytes((Join-Path $output 'input.bgr'),$bytes)
}finally{$bitmap.Dispose()}
$models=Join-Path $repo 'my_cartoon_beautiful/binary/realesrgan-ncnn-vulkan-v0.2.0-windows/models'
for($scale=2;$scale -le 4;$scale++){
 $raw=Join-Path $output "x$scale.bgr"
 & (Join-Path $build 'Release/reference.exe') $models $scale (Join-Path $output 'input.bgr') $raw
 if($LASTEXITCODE -ne 0){throw "Reference inference x$scale failed"}
 $bytes=[IO.File]::ReadAllBytes($raw)
 $bitmap=[Drawing.Bitmap]::new((96*$scale),(64*$scale),[Drawing.Imaging.PixelFormat]::Format24bppRgb)
 try{
  $data=$bitmap.LockBits([Drawing.Rectangle]::new(0,0,$bitmap.Width,$bitmap.Height),[Drawing.Imaging.ImageLockMode]::WriteOnly,$bitmap.PixelFormat)
  try{for($y=0;$y -lt $bitmap.Height;$y++){[Runtime.InteropServices.Marshal]::Copy($bytes,$y*$bitmap.Width*3,[IntPtr]::Add($data.Scan0,$y*$data.Stride),$bitmap.Width*3)}}finally{$bitmap.UnlockBits($data)}
  $bitmap.Save((Join-Path $output "x$scale.png"),[Drawing.Imaging.ImageFormat]::Png)
 }finally{$bitmap.Dispose()}
}
Write-Output "Unmodified upstream GPU reference ready: $output"
