# Native media dependencies

- FFmpeg.AutoGen 9.0.1.1: MIT; https://github.com/Ruslan-B/FFmpeg.AutoGen/tree/v9.0.1.1
- FFmpeg shared build: LGPLv3 (--enable-version3, no --enable-gpl/nonfree); exact archive and DLL hashes in dependencies.lock.json. Native configuration/license strings are recorded by the smoke harness; this does not change FFmpeg licensing.
- FFmpeg source: https://github.com/FFmpeg/FFmpeg/tree/2a571b606854520cf89804d8030c8b328e621689
- Build recipes/patches and dependency revisions: https://github.com/BtbN/FFmpeg-Builds/tree/6c9aec5fc9a72ec3abedd1fa84db141fa18cf52b
- Real-ESRGAN-ncnn-vulkan: MIT, Xintao Wang and nihui; preserve both notices from upstream LICENSE.
- ncnn: BSD-3-Clause and bundled dependency notices, pinned at 6125c9f47cd14b589de0521350668cf9d3d37e3c.

Release packaging must include applicable license texts and corresponding source/build information. Local smoke success does not certify a public distribution; no release has been published by this work.

## Real-ESRGAN bridge build

Real-ESRGAN source and modified core are under `realesrgan_bridge/upstream` (MIT). The native bridge links pinned ncnn and glslang statically. Full upstream licenses and additional file-level attribution notices are preserved under `licenses/`; include this directory with distributions. Vulkan headers/import definitions are build inputs; the system Vulkan loader/GPU driver is not redistributed. No libwebp or stb image implementation is compiled into this DLL. Exact build-input URLs and hashes are in `dependencies.lock.json`.

## Managed/runtime notices

System.Resources.Extensions 4.7.1, System.Memory 4.5.4, System.Buffers 4.5.1, System.Numerics.Vectors 4.5.0 and System.Runtime.CompilerServices.Unsafe 4.5.3 are Microsoft MIT packages; license text is included. FFmpeg.AutoGen is MIT. FFmpeg includes software developed by the Independent JPEG Group; this project does not modify its JPEG implementation.

The pinned shared build reports LGPL version 3 or later. Full LGPLv3 and GPLv3 texts, upstream LICENSE.md and exact runtime configuration are included. The application dynamically links replaceable DLLs; no restrictions on debugging modifications to LGPL libraries are imposed.

This local staging bundle is not a published release. Before public binary distribution, assemble and provide the corresponding FFmpeg sources, BtbN patches/build scripts, and the sources/notices of its linked external libraries at the pinned revisions. The links above are provenance, not a claim that this complete source bundle has been assembled.
