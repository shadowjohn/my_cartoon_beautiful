using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
namespace utility_app
{
    internal sealed class RealEsrganHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        internal RealEsrganHandle(IntPtr value):base(true) { SetHandle(value); }
        protected override bool ReleaseHandle() { RealEsrganNative.Destroy(handle);return true; }
    }
    internal static class RealEsrganNative
    {
        private const string Library="realesrgan_bridge.dll";
        private static readonly object Sync=new object();
        private static string directory;
        private static IntPtr module;
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]internal delegate void Progress(IntPtr user,int done,int total);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate uint AbiVersion();
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern IntPtr LoadLibraryEx(string path,IntPtr file,uint flags);
        [DllImport("kernel32.dll",CharSet=CharSet.Ansi,ExactSpelling=true)]private static extern IntPtr GetProcAddress(IntPtr library,string name);
        [DllImport("kernel32.dll")]private static extern bool FreeLibrary(IntPtr library);
        internal static void Load(string nativeDirectory)
        {
            string path=Path.GetFullPath(nativeDirectory).TrimEnd(Path.DirectorySeparatorChar);
            lock(Sync) {
                if(directory!=null) {
                    if(!string.Equals(directory,path,StringComparison.OrdinalIgnoreCase))throw new NativeMediaException("upscale-runtime",-1,"Native library is already loaded from another directory");
                    return;
                }
                if(!Environment.Is64BitProcess)throw new NativeMediaException("upscale-runtime",-1,"Windows x64 required");
                string dll=Path.Combine(path,Library);
                if(!File.Exists(dll))throw new NativeMediaException("upscale-runtime",-1,"Missing library: "+dll);
                IntPtr candidate=LoadLibraryEx(dll,IntPtr.Zero,0x100|0x1000);
                if(candidate==IntPtr.Zero)throw new NativeMediaException("upscale-runtime",Marshal.GetLastWin32Error(),"Cannot load "+dll);
                try {
                    IntPtr entry=GetProcAddress(candidate,"rs_abi_version");
                    if(entry==IntPtr.Zero || ((AbiVersion)Marshal.GetDelegateForFunctionPointer(entry,typeof(AbiVersion)))()!=1)
                        throw new NativeMediaException("upscale-runtime",-1,"Real-ESRGAN ABI mismatch");
                    module=candidate;directory=path;candidate=IntPtr.Zero;
                } finally {if(candidate!=IntPtr.Zero)FreeLibrary(candidate);}
            }
        }
        [DllImport(Library,EntryPoint="rs_create",CallingConvention=CallingConvention.Cdecl,CharSet=CharSet.Unicode,ExactSpelling=true)]
        internal static extern int Create(string param,string model,int gpu,int scale,int tile,int tta,out IntPtr handle,[Out]byte[] error,uint capacity);
        [DllImport(Library,EntryPoint="rs_process",CallingConvention=CallingConvention.Cdecl,ExactSpelling=true)]
        internal static extern int Process(RealEsrganHandle handle,byte[] input,ulong inputBytes,int width,int height,int channels,int inputStride,
            [Out]byte[] output,ulong outputBytes,int outputStride,Progress progress,IntPtr user,[Out]byte[] error,uint capacity);
        [DllImport(Library,EntryPoint="rs_request_cancel",CallingConvention=CallingConvention.Cdecl,ExactSpelling=true)]
        internal static extern void Cancel(RealEsrganHandle handle);
        [DllImport(Library,EntryPoint="rs_destroy",CallingConvention=CallingConvention.Cdecl,ExactSpelling=true)]
        internal static extern void Destroy(IntPtr handle);
        internal static NativeMediaException Error(int code,byte[] buffer) {
            int length=Array.IndexOf(buffer,(byte)0);if(length<0)length=buffer.Length;
            return new NativeMediaException("upscale",code,Encoding.UTF8.GetString(buffer,0,length));
        }
    }
}
