# Native media dependencies

- FFmpeg.AutoGen 9.0.1.1: MIT; https://github.com/Ruslan-B/FFmpeg.AutoGen/tree/v9.0.1.1
- FFmpeg shared build: locally rebuilt LGPLv3 (--enable-version3, --disable-gpl, --disable-nonfree). Chromaprint/FFTW and optional GPL LittleCMS plugins are removed before linking; LGPL core libraries and permissive dependencies remain. Exact archive/DLL hashes and dependency image digest are in dependencies.lock.json. Exported configuration/license strings alone do not establish the licenses of transitive dependencies; the source and linker records are supplied too.
- FFmpeg source: https://github.com/FFmpeg/FFmpeg/tree/2a571b606854520cf89804d8030c8b328e621689
- Build recipes/patches and dependency revisions: https://github.com/BtbN/FFmpeg-Builds/tree/9acad4a9ef1583096af7836cc1e9c8cbcb4d3950
- Real-ESRGAN-ncnn-vulkan: MIT, Xintao Wang and nihui; preserve both notices from upstream LICENSE.
- ncnn: BSD-3-Clause and bundled dependency notices, pinned at 6125c9f47cd14b589de0521350668cf9d3d37e3c.

Applicable license texts are included in the binary package. The v0.05 Release provides corresponding source archives, dependency sources and build information alongside the binaries: https://github.com/shadowjohn/my_cartoon_beautiful/releases/tag/v0.05 . SHA256SUMS.txt identifies and verifies the release assets.

## Real-ESRGAN bridge build

Real-ESRGAN source and modified core are under `realesrgan_bridge/upstream` (MIT). The native bridge links pinned ncnn and glslang statically. Full upstream licenses and additional file-level attribution notices are preserved under `licenses/`; include this directory with distributions. Vulkan headers/import definitions are build inputs; the system Vulkan loader/GPU driver is not redistributed. No libwebp or stb image implementation is compiled into this DLL. Exact build-input URLs and hashes are in `dependencies.lock.json`.

## Managed/runtime notices

System.Resources.Extensions 4.7.1, System.Memory 4.5.4, System.Buffers 4.5.1, System.Numerics.Vectors 4.5.0 and System.Runtime.CompilerServices.Unsafe 4.5.3 are Microsoft MIT packages; their shared license text is included. System.Resources.Extensions-THIRD-PARTY-NOTICES.txt covers Resources.Extensions; System-MIT-Packages-THIRD-PARTY-NOTICES.txt is the identical notice supplied with the other four packages. FFmpeg.AutoGen is MIT. FFmpeg includes software developed by the Independent JPEG Group; this project does not modify its JPEG implementation.

FFmpeg dependency notices are preserved under `licenses/FFmpeg-dependencies/`; GCC runtime components use GPLv3 with the GCC Runtime Library Exception. The exception permits eligible compiled programs to use those runtime components under their own terms. The pinned shared build reports LGPL version 3 or later. Full LGPLv3 and GPLv3 texts, upstream LICENSE.md and exact runtime configuration are included. The application dynamically links replaceable DLLs; no restrictions on debugging modifications to LGPL libraries are imposed.

The v0.05 source assets include the exact FFmpeg revision, pinned BtbN recipes and patches, the exact dependency source archives selected for the pinned dependency image, additional locked Rust crate sources, and the local removal/build scripts. See SOURCES.md in the same Release for asset mapping, provenance, extraction and rebuild instructions. The application/native companion my_cartoon_beautiful_v0.05-source.zip contains the tagged application and bridge source, pinned ncnn/glslang/Vulkan inputs, patches and licenses. Build verification does not imply bit-identical output across different compiler or operating-system versions.
