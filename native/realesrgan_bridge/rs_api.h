#pragma once
#include <stdint.h>
#ifdef RS_BUILD
#define RS_API __declspec(dllexport)
#else
#define RS_API __declspec(dllimport)
#endif
#ifdef __cplusplus
extern "C" {
#endif
typedef void (__cdecl *rs_progress)(void* user, int32_t completed, int32_t total);
RS_API uint32_t __cdecl rs_abi_version(void);
RS_API int32_t __cdecl rs_create(const wchar_t* param, const wchar_t* model, int32_t gpu,
    int32_t scale, int32_t tile, int32_t tta, void** handle, char* error, uint32_t capacity);
// Packed BGR/BGRA with positive byte strides; capacities include row padding.
RS_API int32_t __cdecl rs_process(void* handle, const uint8_t* input, uint64_t input_bytes,
    int32_t width, int32_t height, int32_t channels, int32_t input_stride,
    uint8_t* output, uint64_t output_bytes, int32_t output_stride,
    rs_progress progress, void* user, char* error, uint32_t capacity);
RS_API void __cdecl rs_request_cancel(void* handle);
// Safe with an in-flight process: requests cancellation; resources live until its return.
RS_API void __cdecl rs_destroy(void* handle);
#ifdef __cplusplus
}
#endif
