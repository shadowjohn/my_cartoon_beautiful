# Native DLL Integration Phase A Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在 Windows x64 WinForms 內直接呼叫 FFmpeg 與 Real-ESRGAN DLL，保留既有 PNG 轉檔流程，執行時不啟動這兩個外部 exe。

**Architecture:** 保留 Form1 與六步驟協調；FFmpeg 操作集中到 managed backend，Real-ESRGAN 以 C ABI DLL + SafeHandle adapter 整合。兩個 native 元件先各自通過 smoke tests，再接入 UI，最後驗證完整發佈包。

**Tech Stack:** .NET Framework 4.7.2、WinForms、Windows x64、FFmpeg.AutoGen + 匹配的 LGPL shared FFmpeg、MSVC/CMake、ncnn/Vulkan。

**Spec:** ../specs/2026-10-06-native-dll-integration-design.md（已獲使用者確認；4.7.2 是本輪相容性查核後採用的目標）。

## Global Constraints

- 採用 .NET Framework 4.7.2；主程式、native libraries、建置與打包統一為 Windows x64。
- 保留 WinForms、六個步驟、30 fps、x1/x2/x3/x4、PNG 檔案 SHA-256 去重與補回、保留暫存選項。
- 仍以 PNG 作為各步驟間的磁碟介面。模型沿用既有 realesr-animevideov3 x2/x3/x4 .param/.bin。
- FFmpeg.AutoGen 與 native libraries 以一組相依鎖定；禁止獨立升級其中一端。
- 現有碼與文件保留各自 BOM/encoding/newline；只 stage 本任務檔案，不使用 git add -A。
- 不改 D:/mytools/Real-ESRGAN-ncnn-vulkan 工作樹；建置來源必須可由本 repository 的 pinned manifest 還原。
- 新下載／產生的 SDK、原始碼 cache、native binaries、影片與大型報告放 artifacts/，不新增到 Git；必要的 bridge、上游少量 core source/patch、manifest、notices 與測試程式納入版本控制。
- native 失敗不能視為完成；成功須 flush、寫完 trailer、確認輸出後才回報。取消為 OperationCanceledException，不混作一般錯誤。
- 第一階段單一 job/GPU。Phase B 的廣泛重構、UI 改版、記憶體串流與效能優化不執行。
- 各 task 結束追加 history 的本機證據與未驗證邊界；僅提交本 task 明列的目標檔案，不推送或發佈。

## Review Focus

1. 中文／空白路徑與非工作目錄啟動：Task 1、5、9 必須驗證 loader/model/input/output 不依賴 cwd。
2. 非 30 fps、非零 timestamps、B-frames 與音訊延遲：Task 2、3、7 比對 fps filter、flush、A/V 同步。
3. BGR/BGRA、正負／padding stride、尺寸乘法溢位：Task 4、5 明確拒絕或轉換，不得越界或偏色。
4. 取消、初始化失敗、關窗後 callback：Task 4、5、8 驗證釋放順序、無 callback 存取已銷毀 UI、可再執行。
5. 缺 DLL／ABI 不匹配、無 NVENC／GPU、發佈漏相依：Task 1、4、7、9 失敗可診斷，x1 不要求 ESRGAN/GPU。

## File Structure

路徑均相對 repository root D:/mytools/my_cartoon_beautiful；所有下列新增路徑目前皆為計畫，尚不存在。

| 檔案 | 責任 |
| --- | --- |
| my_cartoon_beautiful/App_Code/media_contracts.cs | 共用資料契約、錯誤、進度與 backend 介面 |
| my_cartoon_beautiful/App_Code/ffmpeg_runtime.cs | 固定目錄載入、ABI/codec 檢查與 native log |
| my_cartoon_beautiful/App_Code/ffmpeg_media_backend.cs | IMediaBackend facade／probe |
| my_cartoon_beautiful/App_Code/ffmpeg_video_decoder.cs | 解碼、fps filter、PNG 序列 |
| my_cartoon_beautiful/App_Code/ffmpeg_audio_transcoder.cs | 音訊解码／轉碼與 AudioAsset |
| my_cartoon_beautiful/App_Code/ffmpeg_video_muxer.cs | 30 fps H.264、音訊 packet remux、MP4 trailer |
| my_cartoon_beautiful/App_Code/realesrgan_native.cs | P/Invoke、SafeHandle、ABI 宣告 |
| my_cartoon_beautiful/App_Code/realesrgan_upscaler.cs | PNG I/O、stride、job/session、取消 |
| native/realesrgan_bridge/{CMakeLists.txt,bridge.h,bridge.cpp,tests/bridge_tests.cpp} | C ABI、native lifetime／error tests |
| native/realesrgan_bridge/upstream/ | 固定 revision 的 core .h/.cpp、4 個 shader 與 LICENSE；不包含 main.cpp |
| native/dependencies.lock.json、native/THIRD-PARTY-NOTICES.md | 精確來源、版本、hash、configure 與授權 |
| tools/{prepare_native.ps1,build_native.ps1,create_media_fixtures.ps1,verify_native_release.ps1} | 還原相依、編譯、建立測試資料、乾淨發佈驗證 |
| tests/NativeMediaSmokeTests/{NativeMediaSmokeTests.csproj,Program.cs,Assertions.cs,*Tests.cs} | net472 x64 實際 backend 測試，不以 net10 代替產品 runtime 驗收 |

既有修改點：csproj、sln、app.config、App_Code/app.cs、App_Code/include.cs、Form1.cs、tools/build_release.ps1、.github/workflows/build-dotnet-framework.yml、README.md、history.md。

## Shared Interfaces

使用 C# 7.3 可表達的 class/enum，避免以較新語法迫使 framework 升級。

```csharp
public enum AudioMode { Aac, Mp3, Vorbis, PcmWav }
public enum EncoderPreference { PreferNvenc, SoftwareOnly }
public sealed class MediaInfo { public TimeSpan? Duration; public int Width, Height; public bool HasVideo, HasAudio; }
public sealed class FrameSequence { public string DirectoryPath; public int Count, Width, Height; public TimeSpan SourceStartTime; /* 固定 30 fps、00000001.png 起 */ }
public sealed class AudioAsset { public string Path; public AudioMode Mode; public TimeSpan Duration, SourceStartTime; }
public sealed class MediaProgress { public string Stage; public long Completed; public long? Total; }
public sealed class NativeMediaException : Exception { public string Stage; public int NativeCode; public NativeMediaException(string stage, int code, string message, Exception inner = null); }
public interface IMediaBackend {
    MediaInfo Probe(string input, CancellationToken token);
    FrameSequence ExtractFrames(string input, string directory, IProgress<MediaProgress> progress, CancellationToken token);
    AudioAsset ExtractAudio(string input, string outputBase, AudioMode mode, IProgress<MediaProgress> progress, CancellationToken token);
    void EncodeMp4(FrameSequence frames, AudioAsset audio, string output, EncoderPreference encoder, IProgress<MediaProgress> progress, CancellationToken token);
}
```

上述介面為簽名示意，Exception constructor 須提供實作。SourceStartTime 使用共同輸入時間軸；輸出以 video/audio 起點較早者作共同零點，不能分別歸零造成不同步。若既有 CLI baseline 在異常 timestamps 下本來不同步，需記錄差異，不靜默宣稱一致。FfmpegMediaBackend 實作 IMediaBackend，constructor 接收已驗證的 FfmpegRuntime。FfmpegRuntime.Load(string absoluteNativeDirectory) 載入並驗證版本；只在同一 process 初始化一次且不得熱切換版本。native 操作在背景 worker 同步执行；app.cs 各 async 步驟 await worker，不在 UI thread 做 native 迴圈。

RealEsrganOptions 定義 int Scale=2/3/4、GpuId=-1（預設 GPU）、TileSize=0（auto），bool Tta=false。RealEsrganUpscaler(string nativeDirectory, string modelDirectory, RealEsrganOptions options) 實作 IDisposable；方法 void UpscalePng(string input, string output, IProgress<MediaProgress> progress, CancellationToken token)。一次 job 重用一個 instance。

Native C ABI 全部 extern C、cdecl，錯誤訊息為 caller-owned UTF-8 buffer，路徑為 Windows UTF-16。狀態碼：0=成功、1=取消、-1=參數／buffer、-2=模型、-3=GPU、-4=推論、-5=busy。

```c
int32_t rs_create(const wchar_t* param_path, const wchar_t* model_path,
  int32_t gpu_id, int32_t scale, int32_t tile_size, int32_t tta,
  void** out_handle, char* error_utf8, uint32_t error_capacity);
int32_t rs_process(void* handle, const uint8_t* input, uint64_t input_bytes,
  int32_t width, int32_t height, int32_t channels, int32_t input_stride,
  uint8_t* output, uint64_t output_bytes, int32_t output_stride,
  void (__cdecl *progress)(void*, int32_t, int32_t), void* user,
  char* error_utf8, uint32_t error_capacity);
void rs_request_cancel(void* handle);
void rs_destroy(void* handle);
uint32_t rs_abi_version(void); /* 本介面回傳 1 */
```

handle 只允許一個 process 呼叫；取消狀態對該 session sticky，取消後 dispose/recreate 再開始 job，避免 start/reset 與取消競態。request_cancel 可與 process 同時呼叫；destroy 必須在 process 返回且取消 callback unregister 後執行。callback 只傳完成 tile 數／總 tile 數，不跨界丟例外。第一版 create 拒絕同 process 內第二個未釋放 session（busy），因此不引入共享 GPU session 管理架構。

## Task 1: net472/x64、FFmpeg loader 與相依鎖版

**Files:** 新增 media_contracts.cs、ffmpeg_runtime.cs、native/dependencies.lock.json、native/THIRD-PARTY-NOTICES.md、tools/prepare_native.ps1、tests/NativeMediaSmokeTests/{csproj,Program.cs,Assertions.cs,RuntimeTests.cs}；修改產品 csproj、sln、app.config 與 CI platform。

**Interfaces:** 產出 shared contracts、FfmpegRuntime.Load；prepare_native.ps1 -VerifyOnly 驗證 manifest 所列 SHA-256/ABI/相依存在，不下載；一般模式只還原到 artifacts/native-cache 與指定 staging 目錄。

- [ ] **1. 建立 RuntimeTests：** assert Environment.Is64BitProcess；正確 DLL 可回報版本與 avformat；缺 DLL、故意不匹配的 lock/ABI 回報 NativeMediaException；改 cwd 到暫存目錄仍可載入絕對路徑。
- [ ] **2. 建置／執行 --suite runtime，確認尚無 loader 時失敗。** Harness 缺 GPU 或依賴時回報 NOT-RUN 並 exit 2，不能印 PASS；一般 assertion 失敗 exit 1。
- [ ] **3. 切換 v4.7.2 + x64、Prefer32Bit=false，加入 app.config supportedRuntime .NETFramework,Version=v4.7.2。** CI 與 sln 使用 x64；保留 resource dependency 與既有 encoding。
- [ ] **4. 凍結一組 binding/shared binary 並實作 loader。** 使用下方「已定位候選」固定組合，完成 net472 smoke 後才寫入 lock，包含 exact package version、asset URL、upstream SHA/tag、SHA-256、DLL majors、configure/license、原始碼位置。不能以 latest URL 當鎖版。先強制 libopenh264 編碼並 decode 回讀；確認 PNG、AAC、libmp3lame、libvorbis、PCM 可用。若候選 shared build 缺必要 codec 或授權不符，Task 1 未完成，記錄原因並調整候選，不進入下游。
- [ ] **5. 驗證：** dotnet build my_cartoon_beautiful/my_cartoon_beautiful.sln -c Release -p:Platform=x64；dotnet build tests/NativeMediaSmokeTests/NativeMediaSmokeTests.csproj -c Release；執行 tests/NativeMediaSmokeTests/bin/Release/net472/NativeMediaSmokeTests.exe --suite runtime。必須均 exit 0，輸出 NativeMediaSmokeTests: PASS [runtime]。
- [ ] **6. 記錄 loader 實測組合並 commit 本 task 明列檔案。** Commit message: build: target net472 x64 and pin native media dependencies。

## Task 2: 對照 fixture、metadata 與固定 30 fps PNG

**Files:** 新增 tools/create_media_fixtures.ps1、ffmpeg_media_backend.cs、ffmpeg_video_decoder.cs、tests/NativeMediaSmokeTests/{FixturePaths.cs,DecodeTests.cs}；更新產品 explicit Compile items。

**Interfaces:** 消費 Task 1 runtime/contracts；產出 Probe、ExtractFrames。FixturePaths.Root 指向 artifacts/media-tests/fixtures，生成結果和舊版本 baseline 不進 Git。

- [ ] **1. 建立 fixture：** 96x64、2 秒、30 fps（60 frames）、已知 RGB 區塊、1 秒處白色閃光 + 音訊 click；另做 24 fps、VFR、非零起始 PTS、無音軌與損壞檔。測試工具可使用既有 CLI 產生／檢查，產品執行時不得依賴它。
- [ ] **2. 用舊 exe 命令建立 baseline 與 JSON 摘要。** 記錄每條命令、exit code、frame 數、大小、timestamp、audio modes 的實際結果；不把舊流程失敗算成新流程必須假成功。
- [ ] **3. 建立 DecodeTests 並先看失敗：** assert 2 秒 CFR 的 Count=60、Width=96、Height=64；檔名從 00000001.png 到 00000060.png；24 fps/VFR 結果與 baseline 相同的 30 fps 抽補幀；無 video／損壞輸入明確失敗。測試可用中文空白路徑。
- [ ] **4. 實作 Probe 與 ExtractFrames。** libavformat 取得媒體資訊；decoder send/receive、fps=30 filter 與 flush；正確處理 time base／色彩／rotation/SAR 的 baseline 行為。幀數以實際寫完為準，Duration*30 僅用進度估計。
- [ ] **5. 執行 harness --suite decode，必須 PASS [decode]。** 紅藍區塊不得互換；取消後不回傳完整 FrameSequence；末幀 flush 與再執行正常。
- [ ] **6. commit 明列檔案及 manifest/baseline 描述（不含媒體檔）。** Message: feat: decode video to png through ffmpeg libraries。

## Task 3: 音訊 DLL 路徑

**Files:** 新增 ffmpeg_audio_transcoder.cs、tests/NativeMediaSmokeTests/AudioTests.cs；擴充 ffmpeg_media_backend.cs、更新 Compile items。

**Interfaces:** 消費 IMediaBackend；產出 AudioAsset。Aac=192k/.aac、Mp3=libmp3lame q4/.mp3、Vorbis=libvorbis q4/.ogg、PcmWav=PCM WAV/.wav，保持 UI 選项語意。

- [ ] **1. 建立 AudioTests 並先看失敗：** 四種選項正確 codec/副檔名與可解碼 sample；無音軌拋 NativeMediaException 並指出 no audio stream，不產生成功 AudioAsset；取消可再次執行。
- [ ] **2. 實作 ExtractAudio。** 檢查 stream、sample format/layout/rate，以 swresample + encoder FIFO/flush 寫合法檔案；保留時間戳資訊以供同步，不能一律丟棄 stream 起始 offset。
- [ ] **3. 執行 harness --suite audio，必須 PASS [audio]。** 以 click 時間量測偏移；只因副檔名存在不算通過。encoder 不存在需具體回報名稱。
- [ ] **4. commit 明列檔案。** Message: feat: extract audio through ffmpeg libraries。

## Task 4: Real-ESRGAN C ABI DLL

**Files:** 新增 native/realesrgan_bridge/ 完整檔案、tools/build_native.ps1；更新 native manifest/notices、prepare_native.ps1。

**Interfaces:** 消費 spec 固定 ESRGAN revision 37026f49824c5cf84062e7c6a5dd71445dcf610f、ncnn 6125c9f47cd14b589de0521350668cf9d3d37e3c；產出上方 rs_* ABI 與 realesrgan_bridge.dll。

- [ ] **1. 建立 native CTest：** abi=1、缺模型回傳 -2 且 handle=null；非法 scale/尺寸/stride/capacity 回傳 -1；第二個 live session 回傳 -5；create 途中失敗後可 destroy/recreate。先用未實作 target 確認測試失敗。
- [ ] **2. 完成 pinned source、shader 與 Windows x64 CMake build。** prepare/build scripts 使用 VS developer environment；定位 Vulkan headers/lib/glslangValidator，未找到時列出缺項。以官方來源取得必要 SDK/dependency 後鎖定版本與 hash；不把開發者絕對路徑写入 repo。bridge 不需要 libwebp/main.cpp。
- [ ] **3. 實作 create/destroy 與核心錯誤傳遞。** 保留 GPU default、auto tile（heap budget >1900/550/190 → 200/100/64，否則 32）、prepadding=10、TTA=false。補 FILE*／load return／pipeline／extract 檢查與 null-safe cleanup；所有改动記錄為 upstream 小範圍差異。
- [ ] **4. 實作 process/cancel。** channels 僅 3/4，native 接受正 stride；檢查 input/output 容量與 64-bit 乘法；以 packed staging 適配核心。tile/GPU submission 完成後 callback 與檢查 atomic cancel；早退也要等提交工作收尾並回收 allocator。
- [ ] **5. 執行 tools/build_native.ps1 -Configuration Release，接著 ctest --test-dir artifacts/native-build -C Release --output-on-failure。** GPU tests 須在本機 GPU 跑；CI 缺 GPU 只能報 NOT-RUN。缺 SDK 或子模組不能標已完成。
- [ ] **6. commit bridge/source notices/腳本/lock 等明列檔案。** Message: feat: build realesrgan native bridge with safe lifetime。

## Task 5: C# PNG adapter 與同模型對照

**Files:** 新增 realesrgan_native.cs、realesrgan_upscaler.cs、tests/NativeMediaSmokeTests/UpscaleTests.cs；更新 Compile items。

**Interfaces:** 消費 rs_* ABI；產出 RealEsrganUpscaler。managed lifetime 保護整個 process 與取消 callback，使用完成才 Dispose。

- [ ] **1. 建立 UpscaleTests 並先看失敗：** 96x64 PNG 在 x2/3/4 輸出 192x128、288x192、384x256；測試 RGB/BGRA、alpha、padding/negative Bitmap stride、中文模型路徑。
- [ ] **2. 實作 P/Invoke、SafeHandle、PNG 讀寫與 stride 正規化。** callback delegate/user data 保持存活；不可跨 native callback 丟 managed exception；在 output 寫完前不回報完成。模型每 job 載入一次。
- [ ] **3. 執行與既有 v0.2.0 exe 相同模型、scale、GPU、tile、TTA 的 baseline 對照。** 記錄像素相等與否、MAE、最大差值和差異圖。相同環境設定先要求逐像素一致；若不一致，該對照不得自動 PASS，先查版本／色彩／GPU 精度差異並記錄可審核結論。
- [ ] **4. 執行 harness --suite upscale。** 必須通過尺寸／色彩／alpha／重用／缺模型／取消後新 job；取消中 request_cancel 不與 destroy 競態。提供 native error code 和診斷。
- [ ] **5. commit 明列檔案。** Message: feat: call realesrgan dll from png workflow。

## Task 6: 接回拆幀、音訊與 AI 步驟

**Files:** 修改 App_Code/app.cs、App_Code/include.cs、Form1.cs（僅 backend/job 初始化）；新增 tests/NativeMediaSmokeTests/PipelineTests.cs。

**Interfaces:** 消費 IMediaBackend、RealEsrganUpscaler；保留 step1..step6 方法與 UI 進度區間。job 內保留 FrameSequence/AudioAsset/MediaInfo，避免重複探測。

- [ ] **1. 建立 pipeline 測試並先看失敗：** x1 不載入 ESRGAN DLL/GPU；重複 frame SHA-256 去重後只推論 unique PNG，補回連續 60 frames；x2+ 缺模型明確失敗；保留暫存符合勾選。
- [ ] **2. 替換 metadata、step2/step3/step4 的命令列與掃檔進度。** 去重依 PNG bytes；frame/tile 完成後回報進度，進度固定映射至現有 constants；保持舊成功的命名、UI 音訊與倍數設定。
- [ ] **3. 執行 harness --suite pipeline-stages 與既有 UtilityTests/ProcessRunnerTests。** 驗證對應 stage 無 ffmpeg/ESRGAN 子程序；step5 在 Task 7 前仍屬尚未遷移，不宣稱整個 app 已去除 exe。
- [ ] **4. commit 明列檔案。** Message: refactor: connect native decoding audio and upscale stages。

## Task 7: MP4 封裝、NVENC fallback 與完整短片

**Files:** 新增 ffmpeg_video_muxer.cs、tests/NativeMediaSmokeTests/MuxTests.cs；修改 ffmpeg_media_backend.cs、app.cs step5、include.cs encoder helper、Compile items。

**Interfaces:** 產出 EncodeMp4；SoftwareOnly 為測試可控的軟體分支，PreferNvenc 先初始化 NVENC，失敗且軟體可用時重建 encoder context 回退。

- [ ] **1. 建立 MuxTests 並先看失敗：** 60 frames→30 fps MP4；2 秒 fixture duration 差 <=1/30 秒；白閃光/audio click 同步誤差 <=1/30 秒；decoder 可讀至 EOF，輸出尺寸與 x1..x4 相符。
- [ ] **2. 實作 PNG 解碼到 yuv420p、H.264 編碼、packet timestamps rescale/interleave、音訊 remux。** AAC ADTS 到 MP4 需正確 extradata／bitstream filter；flush delayed packets 與 trailer。用 output.partial.mp4 暫存，同目錄完成驗證後才 replace/move 到使用者授權的 output；失敗保留錯誤並不標成功；測試預先存在的 output 在失敗／取消時 bytes 不變，成功後才替換。
- [ ] **3. 驗證 NVENC 和強制 SoftwareOnly 兩條路。** 不以 avcodec_find_encoder 找到名稱當可用；實際開啟 encoder。MP3/Vorbis/PCM 與 MP4 逐項依 muxer 支援判斷，失敗清楚報告，禁止靜默轉成另一音訊設定。
- [ ] **4. 執行 harness --suite mux 及 --suite pipeline。** x1+AAC 與 x2/3/4 短片必須 PASS；量測編碼選項與軟體回退，輸出不存在／損壞／未 flush 皆失敗。
- [ ] **5. commit 明列檔案。** Message: feat: encode and mux mp4 without ffmpeg process。

## Task 8: UI 取消、關窗與錯誤收尾

**Files:** 修改 Form1.cs、app.cs；新增 tests/NativeMediaSmokeTests/LifecycleTests.cs。

**Interfaces:** Form1 保存 Task activeJob 與 CancellationTokenSource；Close 取消後 await activeJob，釋放完再關閉。job 中持有 backend/session，finally 釋放，進度 sink 在 job 完成後停止。

- [ ] **1. 建立 LifecycleTests 並先看失敗：** 在 decode/audio/upscale/encode 中取消，不走成功訊息；開始→取消→再次開始成功；錯誤有 stage/code/message；disposing UI 後無 Invoke/callback；native session 歸零。
- [ ] **2. 改為合作式取消與等候收尾。** 運行中 FormClosing 先 e.Cancel=true，重入防護，Cancel 後等待 activeJob 完成再 Close；移除這條路徑的 Environment.Exit 強制退出與 native 工作期間的 temp cleanup。
- [ ] **3. 執行 harness --suite lifecycle，並人工操作 WinForms。** 轉檔時 UI 可回應；目前 GPU submission 可等候，不承諾立即取消；keep-temp 開／關、cancel／error／success 均核對。
- [ ] **4. 記錄人工驗證與未驗證分支並 commit。** Message: fix: await native job cancellation before form shutdown。

## Task 9: 完整打包、去除 exe 相依與驗收

**Files:** 修改 tools/build_release.ps1、產品 csproj content、CI、README；新增 tools/verify_native_release.ps1；更新 manifest/notices/history。

**Interfaces:** build_release.ps1 增加/固定 x64 輸出與 native runtime layout；verify_native_release.ps1 -PackagePath <staging> -RequireGpu 驗證完整能力。NativeMediaSmokeTests 支援 --native-root 指向 staging 原生相依；報告放 artifacts/acceptance。

- [ ] **1. 建立 release 驗證失敗案例：** staging 移除一個 managed/native DLL 時 fail；錯誤 ABI fail；stage 內沒有 ffmpeg.exe/ESRGAN exe 時 x1/x2 仍完成。測試用工具獨立放 staging 外，不混入發佈包。
- [ ] **2. 修改腳本收錄 managed DLL、runtime/native DLL、models、config 與 notices，排除舊工具 exe。** loader 使用 app base，不依 PATH/cwd；修補只刪 staging 的絕對路徑驗證；模型維持既有來源，避免 duplicate bulk commit。
- [ ] **3. 確認所有六處外部呼叫已移除產品執行路徑。** 以原始碼搜尋 + process start 事件追蹤驗證實際轉檔沒有新增 ffmpeg/ESRGAN 子程序；開發 fixture 工具的 CLI 不計為產品依賴。
- [ ] **4. 執行 Release build、兩組既有 tests、native CTest、完整 smoke suites 與乾淨 staging 驗證。** CI 執行 CPU tests/native build；GPU evidence 來自本機，不混為 CI 通過。
- [ ] **5. 同一 app instance 執行 5 次短片及一段 10 分鐘片段，记录 private bytes、native/GPU memory 峰值與各 job idle 值。** 確認 completed job 不持續累積 handle/session／allocator。若有成長趨勢先調查；不能只跑一次就宣稱無洩漏。長片性能只記錄，不以「必須更快」作未承諾驗收。
- [ ] **6. README 記錄 net472/x64、依賴與實測步驟；history 區分 local/native/UI/GPU/release evidence。** 未取得的環境或輸入 coverage 留 pending。
- [ ] **7. commit 本 task 明列檔案，最後做一次整體 review，停止無新發現的 review loop。** Message: build: package native media runtime and verify no cli dependency。不自動 push/tag/release。

## Execution Notes

- 可獨立驗證的三段：Task 1–3 FFmpeg decode/audio；Task 4–5 ESRGAN bridge；Task 6–9 整合／輸出／發佈。Task 4 的 source/build 不依賴 FFmpeg，但由同一 ABI 契約協調。
- 建議由主代理在本聊天依 task 順序實作，末尾交獨立 reviewer 檢查；這批任務共享 media contract、native lifetime 與 packaging，逐步串接較容易定位失敗。
- 各 task 測試只驗真正風險與 native 行為；不為文件／csproj 字串寫鏡像測試。
- 相依鎖版是 Task 1 的必交成果；本計畫完成不代表 DLL 已下載／載入／通過 GPU 測試。
- 本文件仍待使用者檢視與選擇執行方式；本輪只寫規格／計畫與 history。

## 已定位候選（未下載／未執行，Task 1 通過後才正式鎖定）

- Binding：FFmpeg.AutoGen 9.0.1.1（netstandard2.0）。
- Native release：BtbN FFmpeg-Builds tag autobuild-2026-09-30-13-08。
- Asset：ffmpeg-n9.0.2-17-g2a571b6068-win64-lgpl-shared-9.0.zip；asset ID 600950900；size 76972461 bytes。
- 上游 API digest：SHA-256 7157177b8a6cb2174c1650ba8c71b363f2c78cba5330f88c4c02cf5b2b880646；下載後必須重新計算比對。
- FFmpeg revision：2a571b606854520cf89804d8030c8b328e621689；BtbN build scripts：6c9aec5fc9a72ec3abedd1fa84db141fa18cf52b。
- Binding 預期 majors：avcodec/avformat/avdevice=63、avutil=61、avfilter=12、swresample=7、swscale=10；實際 DLL 必須逐一核對。
- 軟體 H.264 指定 libopenh264；建置 script 的 enable 設定只證明候選配置，Task 1 仍要真正編碼與回讀。
- 發佈者有 retention policy；固定 URL/hash 不等於永久可下載。採用後在 local artifact storage 保存核對過的 binary/source，記錄重建來源，不把大型 archive 放進 Git。
- NuGet：https://www.nuget.org/packages/FFmpeg.AutoGen/9.0.1.1
- Release：https://github.com/BtbN/FFmpeg-Builds/releases/tag/autobuild-2026-09-30-13-08
- Digest：https://api.github.com/repos/BtbN/FFmpeg-Builds/releases/tags/autobuild-2026-09-30-13-08
- ABI：https://github.com/Ruslan-B/FFmpeg.AutoGen/blob/v9.0.1.1/FFmpeg.AutoGen/generated/ffmpeg.libraries.g.cs
- OpenH264 build：https://github.com/BtbN/FFmpeg-Builds/blob/6c9aec5fc9a72ec3abedd1fa84db141fa18cf52b/scripts.d/50-openh264.sh

## Self-review 結果

- Spec 的 DLL 呼叫、PNG/SHA-256、30 fps、倍數、音訊、錯誤／取消、release/notices 與 Phase B 邊界，皆對應至 Task 1–9。
- 五個 Review Focus 均有 owning task 測試；negative stride 在 managed adapter 正規化，native ABI 僅接受合法正 stride。
- 已補時間戳欄位 SourceStartTime、FFmpeg 候選 hash/ABI 與既有輸出檔失敗時不得改動的驗收。
- 本輪僅規劃與文字查核。4.7.2 新目標、新 native 組合與各步驟測試尚未執行；前輪的 4.6.2 build PASS 不移作新目標通過證據。
