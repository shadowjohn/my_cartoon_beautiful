[CmdletBinding()]
param([ValidateSet('Release','Debug')][string]$Configuration='Release', [switch]$RequireGpu, [switch]$SkipTests, [int]$Jobs=4)
$ErrorActionPreference='Stop'
if($SkipTests -and $RequireGpu){throw 'RequireGpu cannot be combined with SkipTests'}
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$cache=Join-Path $repo 'artifacts/native-cache'
$sourceRoot=Join-Path $repo 'artifacts/native-source'
$build=Join-Path $repo 'artifacts/native-build'
$vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
if(!(Test-Path -LiteralPath $vswhere)){throw 'Visual Studio C++ build tools and vswhere are required'}
$vs=& $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if(!$vs){throw 'Install the Visual Studio Desktop development with C++ workload'}
$cmake=Join-Path $vs 'Common7/IDE/CommonExtensions/Microsoft/CMake/CMake/bin/cmake.exe'
if(!(Test-Path -LiteralPath $cmake)){$cmake=(Get-Command cmake -ErrorAction Stop).Source}
$ctest=Join-Path (Split-Path $cmake) 'ctest.exe'
$vcVersion=(Get-Content -LiteralPath (Join-Path $vs 'VC/Auxiliary/Build/Microsoft.VCToolsVersion.default.txt') -Raw).Trim()
$lib=Join-Path $vs "VC/Tools/MSVC/$vcVersion/bin/Hostx64/x64/lib.exe"
$manifest=Get-Content -LiteralPath (Join-Path $repo 'native/dependencies.lock.json') -Raw | ConvertFrom-Json
New-Item -ItemType Directory -Force -Path $cache,$sourceRoot | Out-Null
$paths=@{}
foreach($source in $manifest.realesrgan.sources){
    $archive=Join-Path $cache $source.file
    if(!(Test-Path -LiteralPath $archive)){
        Invoke-WebRequest -Uri $source.url -OutFile ($archive+'.partial')
        if((Get-FileHash -LiteralPath ($archive+'.partial') -Algorithm SHA256).Hash -ne $source.sha256){throw "Download hash mismatch: $($source.name)"}
        Move-Item -LiteralPath ($archive+'.partial') -Destination $archive
    }
    if((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $source.sha256){throw "Source hash mismatch: $($source.name)"}
    if($source.root){
        $extracted=Join-Path $sourceRoot $source.name
        $paths[$source.name]=Join-Path $extracted $source.root
        $marker=Join-Path $extracted '.complete'
        if(!(Test-Path -LiteralPath $marker)){
            Write-Output "Extracting verified source: $($source.name)"
            # Overwrite permits repair of an interrupted extraction, without deleting other sources.
            & tar -xf $archive -C (New-Item -ItemType Directory -Force -Path $extracted).FullName
            if($LASTEXITCODE -ne 0){throw "Source extraction failed: $($source.name)"}
            [IO.File]::WriteAllText($marker,$source.sha256)
        }
    }else{$paths[$source.name]=$archive}
}
$ncnnGlslang=Join-Path $paths.ncnn 'glslang'
if(!(Test-Path -LiteralPath (Join-Path $ncnnGlslang 'CMakeLists.txt'))){
    New-Item -ItemType Directory -Force -Path $ncnnGlslang | Out-Null
    Get-ChildItem -LiteralPath $paths.glslang -Force | Copy-Item -Destination $ncnnGlslang -Recurse -Force
}
& (Join-Path $PSScriptRoot 'patch_native_sources.ps1') -NcnnSource $paths.ncnn
$vulkanLib=Join-Path $cache 'vulkan-1.lib'
& $lib /nologo /machine:x64 "/def:$($paths.vulkanDef)" "/out:$vulkanLib"
if($LASTEXITCODE -ne 0){throw 'Vulkan import library creation failed'}
$glslangBuild=Join-Path $repo 'artifacts/glslang-build'
$common=@('-A','x64','-DCMAKE_POLICY_VERSION_MINIMUM=3.5','-DCMAKE_POLICY_DEFAULT_CMP0091=NEW',
    '-DCMAKE_MSVC_RUNTIME_LIBRARY=MultiThreaded$<$<CONFIG:Debug>:Debug>','-DLLVM_USE_CRT_RELEASE=MT','-DLLVM_USE_CRT_DEBUG=MTd')
& $cmake --fresh -S $paths.glslang -B $glslangBuild @common '-DBUILD_EXTERNAL=OFF' '-DENABLE_OPT=OFF' '-DENABLE_GLSLANG_BINARIES=ON' '-DENABLE_SPVREMAPPER=OFF' '-DENABLE_HLSL=OFF' '-DENABLE_CTEST=OFF' '-DBUILD_SHARED_LIBS=OFF'
if($LASTEXITCODE -ne 0){throw 'Shader compiler configuration failed'}
& $cmake --build $glslangBuild --config $Configuration --target glslangValidator --parallel $Jobs
if($LASTEXITCODE -ne 0){throw 'Shader compiler build failed'}
$validator=Join-Path $glslangBuild "StandAlone/$Configuration/glslangValidator.exe"
$modelArg='-DRS_MODEL_DIR='
if($RequireGpu){$modelArg='-DRS_MODEL_DIR='+(Join-Path $repo 'my_cartoon_beautiful/binary/realesrgan-ncnn-vulkan-v0.2.0-windows/models')}
& $cmake --fresh -S (Join-Path $repo 'native/realesrgan_bridge') -B $build @common $modelArg `
    "-DNCNN_SOURCE_DIR=$($paths.ncnn)" "-DGLSLANGVALIDATOR_EXECUTABLE=$validator" `
    "-DVulkan_INCLUDE_DIR=$(Join-Path $paths.vulkanHeaders 'include')" "-DVulkan_LIBRARY=$vulkanLib" `
    '-DNCNN_BUILD_WITH_STATIC_CRT=ON' '-DNCNN_VULKAN=ON' '-DNCNN_SYSTEM_GLSLANG=OFF' '-DNCNN_DISABLE_RTTI=OFF' '-DNCNN_DISABLE_EXCEPTION=OFF' `
    '-DNCNN_INSTALL_SDK=OFF' '-DNCNN_BUILD_BENCHMARK=OFF' '-DNCNN_BUILD_TOOLS=OFF' '-DNCNN_BUILD_EXAMPLES=OFF' '-DNCNN_BUILD_TESTS=OFF' '-DNCNN_PIXEL_ROTATE=OFF'
if($LASTEXITCODE -ne 0){throw 'Native bridge configuration failed'}
& $cmake --build $build --config $Configuration --parallel $Jobs
if($LASTEXITCODE -ne 0){throw 'Native bridge build failed'}
if($SkipTests){Write-Warning 'Native ABI/GPU execution NOT-RUN (SkipTests requested)'}else{
    & $ctest --test-dir $build -C $Configuration --output-on-failure
    if($LASTEXITCODE -ne 0){throw 'Native ABI/GPU tests failed'}
}
$runtime=Join-Path $repo 'artifacts/native-runtime/realesrgan'
New-Item -ItemType Directory -Force -Path $runtime | Out-Null
Copy-Item -LiteralPath (Join-Path $build "$Configuration/realesrgan_bridge.dll") -Destination $runtime -Force
Write-Output "Native bridge staged: $runtime (GPU tests required: $RequireGpu)"
