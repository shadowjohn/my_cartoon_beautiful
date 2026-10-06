# Native core provenance

The `upstream` files are from Real-ESRGAN-ncnn-vulkan commit
`37026f49824c5cf84062e7c6a5dd71445dcf610f` (MIT). `layers.cmake` preserves its
operator selection. ncnn/glslang/Vulkan build sources are pinned separately.
No CLI main, libwebp, image reader or executable is linked into this DLL.

Local core changes: null-safe partial destruction; FILE/load/pipeline/extract
and allocation result checks; allocator scope guard; cancellation at tile
boundaries; progress only after completed submission; all tiles submitted
before callbacks. RGB model arithmetic, padding and shader source remain upstream behavior. C ABI catches C++ exceptions and copies padded rows into
packed buffers. Output is copied to the caller only after successful inference.
One session reserves ncnn's global Vulkan instance. In-flight calls hold shared
ownership, so destroy requests cancellation without freeing GPU resources early.
The managed adapter additionally waits for the worker before destroying its handle.

Pinned ncnn net.cpp also needs the reviewed patch in ../patches: fail before
pipeline creation/upload after partial load, propagate upload/wait errors, and
ignore null layers during cleanup. tools/patch_native_sources.ps1 verifies the
exact original/patched SHA-256 before applying it. Empty-model GPU CTest exposed
the original native crash; this is required for in-process error handling.

The net.cpp patch also takes ownership before parsing a layer so truncated param
files clean up correctly. gpu.cpp initializes bug_buffer_image_load_zero, which
was uninitialized on non-Adreno GPUs and changed across session recreation.

BGRA alpha uses ncnn CPU bicubic separately from GPU RGB. The pinned GPU alpha
path raised VK_ERROR_DEVICE_LOST in both the old CLI and bridge on the local GPU.
Input below11x11 is rejected because the upstream single-reflection padding can
index outside smaller images. No model or shader arithmetic was changed.

Validation: tools/prepare_realesrgan_reference.ps1 compiles the untouched upstream
core as an artifact-only oracle with matching dependencies. Bridge x2/x3/x4
outputs exactly matched this oracle. The old prebuilt executable differed by
MAE0.0884-0.0949 and maximum4/255; tests retain a bounded legacy comparison
(max4, MAE0.12) plus exact rebuilt-core comparison, with magnified diff images.
