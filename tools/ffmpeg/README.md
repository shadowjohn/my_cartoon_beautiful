# Rebuild the LGPL FFmpeg runtime

This reproducible local build uses the exact FFmpeg revision expected by FFmpeg.AutoGen 9.0.1.1 and a digest-pinned BtbN cross-compiler/dependency image. Docker Desktop must be running Linux containers. Build output is ignored by Git.

1. Obtain `FFmpeg-2a571b606854520cf89804d8030c8b328e621689.zip` from the corresponding source bundle, or from `https://codeload.github.com/FFmpeg/FFmpeg/zip/2a571b606854520cf89804d8030c8b328e621689`.
2. Run from the repository root:

   ```powershell
   .\tools\ffmpeg\rebuild.ps1 -SourceArchive .\artifacts\release-source-cache\FFmpeg-2a571b606854520cf89804d8030c8b328e621689.zip -Jobs 8
   ```

   Use `-OutputRoot C:\path\with\space\available` when the checkout drive has insufficient space. The source ZIP SHA-256 is checked before Docker runs; the pinned image needs about 2.1 GB to download plus extracted/build space.

The build retains FFmpeg shared-library ABI majors 63/63/12/63/61/7/10. It removes Chromaprint, FFTW, and the two GPL LittleCMS plugin archives from the build prefix before compilation; removes the LittleCMS plugin pkg-config link flags; and explicitly disables GPL, nonfree, Chromaprint, and unused VapourSynth integration. The core LittleCMS library remains available. Unchanged upstream optional GPL sources may still be present in the source bundle but cannot be linked through these removed archives.

`build.log` contains GNU ld archive traces. `config.h`, `config.mak`, `config.log`, dependency archive hashes, original image configuration, deleted-library hashes, DLL hashes/imports, and `build-manifest.json` provide the build record. The retained Docker container is named `my-cartoon-ffmpeg-lgpl-v005` by default; use `-ContainerName` for a second independent build. The script does not delete other Docker data.

The output includes `ffmpeg.exe` and `ffprobe.exe` for build verification only. Product and native-runtime ZIPs contain the seven DLLs; the application does not invoke these command-line tools. Run the repository native media and package tests against the new DLLs before updating `native/dependencies.lock.json` or publishing.

The dependency image can eventually be removed by its upstream registry. The corresponding source bundle also includes pinned dependency sources and build recipes; use those to rebuild the dependency image when the recorded digest is no longer available. An independently rebuilt toolchain/image need not produce byte-identical DLLs; record and verify its new hashes before use.

After a successful build, create the DLL-only runtime ZIP and verify its exported license/configuration:

```powershell
.\tools\ffmpeg\package-runtime.ps1 -BuildRoot .\artifacts\lgpl-ffmpeg-rebuild -DependencyNotices .\native\licenses\FFmpeg-dependencies
```
