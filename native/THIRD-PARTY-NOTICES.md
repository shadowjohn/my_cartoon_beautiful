# Native media dependencies

- FFmpeg.AutoGen 9.0.1.1: MIT; https://github.com/Ruslan-B/FFmpeg.AutoGen/tree/v9.0.1.1
- FFmpeg shared build: LGPL variant; exact archive and DLL hashes in dependencies.lock.json. Native configuration/license strings are recorded by the smoke harness; this does not change FFmpeg licensing.
- FFmpeg source: https://github.com/FFmpeg/FFmpeg/tree/2a571b606854520cf89804d8030c8b328e621689
- Build recipes/patches and dependency revisions: https://github.com/BtbN/FFmpeg-Builds/tree/6c9aec5fc9a72ec3abedd1fa84db141fa18cf52b
- Real-ESRGAN-ncnn-vulkan: MIT, Xintao Wang and nihui; preserve both notices from upstream LICENSE.
- ncnn: BSD-3-Clause and bundled dependency notices, pinned at 6125c9f47cd14b589de0521350668cf9d3d37e3c.

Release packaging must include applicable license texts and corresponding source/build information. Local smoke success does not certify a public distribution; no release has been published by this work.

## Real-ESRGAN bridge build

Real-ESRGAN source and modified core are under `realesrgan_bridge/upstream` (MIT). The native bridge links pinned ncnn and glslang statically. Full upstream licenses and additional file-level attribution notices are preserved under `licenses/`; include this directory with distributions. Vulkan headers/import definitions are build inputs; the system Vulkan loader/GPU driver is not redistributed. No libwebp or stb image implementation is compiled into this DLL. Exact build-input URLs and hashes are in `dependencies.lock.json`.
