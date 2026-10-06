# Native core provenance

The `upstream` files are from Real-ESRGAN-ncnn-vulkan commit
`37026f49824c5cf84062e7c6a5dd71445dcf610f` (MIT). `layers.cmake` preserves its
operator selection. ncnn/glslang/Vulkan build sources are pinned separately.
No CLI main, libwebp, image reader or executable is linked into this DLL.

Local core changes: null-safe partial destruction; FILE/load/pipeline/extract
and allocation result checks; allocator scope guard; cancellation at tile
boundaries; progress only after completed submission; all tiles submitted
before callbacks. BGR/BGRA, model arithmetic, padding and shader source remain
upstream behavior. C ABI catches C++ exceptions and copies padded rows into
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
