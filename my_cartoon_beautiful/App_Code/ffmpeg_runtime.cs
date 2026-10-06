using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;
namespace utility_app
{
    public sealed unsafe class FfmpegRuntime
    {
        private static readonly object Sync = new object();
        private static FfmpegRuntime loaded;
        private static Exception bindingFailure;
        private static readonly List<IntPtr> Modules = new List<IntPtr>();
        private static readonly string[] Names = { "avutil", "swresample", "swscale", "avcodec", "avformat", "avfilter", "avdevice" };
        private static readonly int[] Majors = { 61, 7, 10, 63, 63, 12, 63 };
        public string DirectoryPath { get; private set; }
        public string Version { get; private set; }
        public string Configuration { get; private set; }
        public string License { get; private set; }
        [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
        private static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);
        [DllImport("kernel32.dll", CharSet=CharSet.Ansi, ExactSpelling=true, SetLastError=true)]
        private static extern IntPtr GetProcAddress(IntPtr module, string name);
        [DllImport("kernel32.dll")] private static extern bool FreeLibrary(IntPtr module);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate uint VersionFunction();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr StringFunction();
        private FfmpegRuntime() { }
        public static FfmpegRuntime Load(string directory)
        {
            if (!Environment.Is64BitProcess) throw new NativeMediaException("runtime", -1, "Windows x64 process required");
            string path = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar);
            lock (Sync)
            {
                if(bindingFailure!=null)throw new NativeMediaException("runtime",-1,"FFmpeg binding initialization failed; restart required",bindingFailure);
                for (int i=0; i<Names.Length; i++)
                    if (!File.Exists(Path.Combine(path, Names[i]+"-"+Majors[i]+".dll")))
                        throw new NativeMediaException("runtime", -1, "Missing library: " + Names[i] + " in " + path);
                if (loaded != null)
                {
                    if (!string.Equals(loaded.DirectoryPath, path, StringComparison.OrdinalIgnoreCase))
                        throw new NativeMediaException("runtime", -1, "FFmpeg is already initialized from a different directory");
                    return loaded;
                }
                // Validate through raw exports before AutoGen caches any function pointers.
                var pending=new List<IntPtr>();
                var candidate=new FfmpegRuntime { DirectoryPath=path };
                try {
                    for (int i=0; i<Names.Length; i++) {
                        IntPtr module=LoadLibraryEx(Path.Combine(path, Names[i]+"-"+Majors[i]+".dll"), IntPtr.Zero, 0x100|0x1000);
                        if(module==IntPtr.Zero)throw new NativeMediaException("runtime",Marshal.GetLastWin32Error(),"Cannot load "+Names[i]);
                        pending.Add(module);
                        IntPtr entry=GetProcAddress(module,Names[i]+"_version");
                        if(entry==IntPtr.Zero)throw new NativeMediaException("runtime",-1,"Missing version export: "+Names[i]);
                        uint version=((VersionFunction)Marshal.GetDelegateForFunctionPointer(entry,typeof(VersionFunction)))();
                        if((version>>16)!=Majors[i])throw new NativeMediaException("runtime",-1,"ABI mismatch: "+Names[i]);
                    }
                    candidate.Version=ReadString(pending[0],"av_version_info");
                    candidate.Configuration=ReadString(pending[3],"avcodec_configuration");
                    candidate.License=ReadString(pending[3],"avcodec_license");
                    if(candidate.Configuration.Contains("--enable-gpl") || candidate.Configuration.Contains("--enable-nonfree"))
                        throw new NativeMediaException("runtime",-1,"Expected the pinned LGPL FFmpeg build");
                } catch { for(int i=pending.Count-1;i>=0;i--)FreeLibrary(pending[i]);throw; }
                // After binding starts, retain modules even on failure: cached delegates cannot be unloaded safely.
                Modules.AddRange(pending);
                try { ffmpeg.RootPath=path;ffmpeg.av_log_set_level(ffmpeg.AV_LOG_ERROR); }
                catch(Exception ex) { bindingFailure=ex;throw new NativeMediaException("runtime",-1,"FFmpeg binding initialization failed; restart required",ex); }
                loaded=candidate;
                return loaded;
            }
        }
        private static string ReadString(IntPtr module,string name)
        {
            IntPtr entry=GetProcAddress(module,name);
            if(entry==IntPtr.Zero)throw new NativeMediaException("runtime",-1,"Missing export: "+name);
            return Marshal.PtrToStringAnsi(((StringFunction)Marshal.GetDelegateForFunctionPointer(entry,typeof(StringFunction)))());
        }
        public static void Check(int result, string stage)
        {
            if(result>=0) return;
            byte* buffer=stackalloc byte[1024];
            ffmpeg.av_strerror(result, buffer, 1024);
            throw new NativeMediaException(stage, result, Marshal.PtrToStringAnsi((IntPtr)buffer));
        }
    }
}
