cmake_minimum_required(VERSION 3.15)
project(realesrgan_reference LANGUAGES CXX)
set(CMAKE_MSVC_RUNTIME_LIBRARY MultiThreaded)
set(CMAKE_CXX_STANDARD 17)
# Development-only oracle: unmodified core, same compiler/ncnn/shader inputs as bridge.
add_executable(reference main.cpp "${UPSTREAM_SOURCE}/realesrgan.cpp")
target_compile_definitions(reference PRIVATE _CRT_SECURE_NO_WARNINGS NOMINMAX)
target_include_directories(reference PRIVATE "${UPSTREAM_SOURCE}" "${NCNN_SOURCE}/src"
 "${NATIVE_BUILD}/ncnn/src" "${NATIVE_BUILD}" "${VULKAN_INCLUDE}")
target_link_libraries(reference PRIVATE
 "${NATIVE_BUILD}/ncnn/src/Release/ncnn.lib"
 "${NATIVE_BUILD}/ncnn/glslang/SPIRV/Release/SPIRV.lib"
 "${NATIVE_BUILD}/ncnn/glslang/glslang/Release/glslang.lib"
 "${NATIVE_BUILD}/ncnn/glslang/glslang/Release/MachineIndependent.lib"
 "${NATIVE_BUILD}/ncnn/glslang/glslang/Release/GenericCodeGen.lib"
 "${NATIVE_BUILD}/ncnn/glslang/OGLCompilersDLL/Release/OGLCompiler.lib"
 "${NATIVE_BUILD}/ncnn/glslang/glslang/OSDependent/Windows/Release/OSDependent.lib"
 "${VULKAN_LIBRARY}")
