# history.md

## 2026-05-27 專案初評估

### 專案定位

`my_cartoon_beautiful` 是一個 Windows WinForms / .NET Framework 4.6.2 單機工具，主要用途是把影片拆成 30fps PNG 序列，透過 Real-ESRGAN-ncnn-vulkan 放大卡通影像，再用 ffmpeg 合成回 MP4。專案偏「可攜帶工具包」設計，binary 已放進 repo，使用者不需要另外安裝 ffmpeg 或 Real-ESRGAN。

### 目前主要結構

- `README.md`：專案說明、版本記錄、下載連結、待辦事項。
- `my_cartoon_beautiful/my_cartoon_beautiful.sln`：Visual Studio solution。
- `my_cartoon_beautiful/my_cartoon_beautiful.csproj`：舊式 .NET Framework 專案，TargetFrameworkVersion 為 `v4.6.2`。
- `my_cartoon_beautiful/Form1.cs`：WinForms UI、輸入輸出檢查、轉檔總流程、log grid、取消/結束處理。
- `my_cartoon_beautiful/App_Code/app.cs`：實際轉檔步驟，包含 ffmpeg / Real-ESRGAN process 呼叫。
- `my_cartoon_beautiful/App_Code/include.cs`：共用工具集合，包含檔案操作、時間、hash、DataGridView、ffmpeg helper。
- `my_cartoon_beautiful/binary/`：內建 `ffmpeg.exe`、`realesrgan-ncnn-vulkan.exe`、模型檔與 DLL。
- `release/V0.01` ~ `release/V0.04`：歷史發佈 zip。
- `snapshot/`、`example/`：畫面截圖與轉檔範例。

### 轉檔流程摘要

1. `step1_checkWorkPath`：建立 `tmp/yyyyMMddHHmmss` 工作目錄。
2. `step2_sourceFile_to_png`：用 ffmpeg 將來源影片固定轉成 30fps PNG。
3. `step3_sourceFile_to_wav`：依 UI 選項輸出 AAC / MP3 / OGG / 原音 WAV。
4. `step4_sourcePng_to_aiPng`：先用 MD5 找出重複 frame，刪除重複圖後呼叫 Real-ESRGAN，完成後再把重複 frame 補回。
5. `step5_aiPng_to_mp4`：用 ffmpeg 將放大後 PNG 與音訊合成 MP4，若偵測到 `h264_nvenc` 則優先使用，否則使用 `h264`。
6. `step6_remove_workPath`：刪除暫存工作目錄。

### 目前優點

- 工具自包含程度高，binary 與模型都已隨專案保存。
- 主流程直接、容易追：UI 驅動 6 個明確步驟。
- 已有基礎 UX：來源/輸出記憶、進度條、步驟 log、最小化到通知區、取消/關閉確認。
- 有重複 frame 去重邏輯，對卡通或長時間靜止畫面有實際效能價值。
- 無 NuGet 套件依賴，維護與發佈門檻低。

### 已驗證狀態

- `git status --short`：初始檢查時工作區乾淨。
- `rg --files`：專案檔案量小，核心 C# 檔案集中。
- `dotnet --info`：本機目前只有 .NET SDK 10.0.300，含 MSBuild 18.6.3。
- `where.exe msbuild`：PATH 找不到獨立 `msbuild.exe`。
- `dotnet build my_cartoon_beautiful/my_cartoon_beautiful.sln --configuration Release`：目前失敗。

建置失敗原因：

```text
MSB3823: 非字串資源需要屬性 GenerateResourceUsePreserializedResources 設定為 true。
MSB3822: 非字串資源在執行階段需要 System.Resources.Extensions 組件，但在這個專案的參考中找不到它。
```

判斷：這比較像是用新版 dotnet/MSBuild 建舊式 .NET Framework WinForms resx 的工具鏈問題，不一定代表 Visual Studio 2022 / Developer Command Prompt 下不能建。下次若要正式修建置，優先用 VS 的 MSBuild 驗證；若要支援 `dotnet build`，再補 `GenerateResourceUsePreserializedResources` 與 `System.Resources.Extensions` 方向。

### 主要風險與技術債

1. Process 成功/失敗判斷偏弱  
   多數步驟主要靠檔案數、檔案大小變化或 process 是否結束判斷，缺少統一檢查 `ExitCode`、stderr 摘要與 timeout。遇到 ffmpeg / Real-ESRGAN 錯誤時，可能表面上繼續或只回傳失敗但沒有足夠診斷資訊。

2. `step2_sourceFile_to_png` 的 while 條件不是以 process 結束為主  
   目前用 `while (true)` 搭配 PNG 數量接近總幀數就跳出，可能在 ffmpeg 尚未完全收尾時提前離開。這是後續穩定性最值得優先補的點。

3. 幀數估算使用 `Duration * 30`  
   `getMovieTotalFrames` 透過 ffmpeg report 抓 Duration，再乘以 30。對 VFR、奇怪封裝、Duration 缺失或 ffmpeg log 格式變動會比較脆。長期建議改用 `ffprobe` 或 ffmpeg `-progress`。

4. UI async 內仍有多處 `Task.Delay(1000).Wait()`  
   有些在背景 task 內影響較小，但語意上仍是 blocking wait；在 UI event 流程中也有等待。後續可逐步改成 `await Task.Delay(..., token)`，取消反應會更乾淨。

5. 暫存保留功能疑似文件與程式不一致  
   README 記錄 V0.02 已完成「可選擇是否保留暫存檔」，但目前 `Form1.cs` 只有 `bool isDebug = false`，沒有看到 UI checkbox 或設定。若功能真的要存在，需補 UI/設定；若已取消，README 應更新。

6. MD5 待改 SHA-256  
   README 待辦已列「改用 sha256 取代 md5 hash」。這個風險不高，因用途是重複 frame 辨識，但改動範圍小、可優先處理。

7. `deltree` 回傳值語意錯誤  
   `include.cs` 的 `deltree` 成功時目前仍回傳 `false`，因 `result` 初始為 false 且未改 true。呼叫端大多沒有依賴回傳值，但這是明顯可修的小 bug。

8. 輸出檔可寫檢查會建立檔案  
   `isFileReadableWritable` 使用 `FileMode.OpenOrCreate`，在真正轉檔前可能先產生空的輸出檔。流程上 ffmpeg `-y` 會覆蓋，但 UX 上若後面失敗，使用者可能看到空檔。

9. 錯誤記錄不夠落地  
   目前大量 `catch {}` 或只 `Console.WriteLine`，對一般 WinForms 使用者不易回報問題。建議建立 per-job log 檔，至少記錄命令列、exit code、stderr 末段、步驟耗時。

### 建議優先順序

P0：
- 補穩 process runner：集中處理啟動、取消、timeout、kill、ExitCode、stderr/stdout capture、log 檔。
- 修 `step2` 不應只靠 PNG 數接近總幀數就提前視為成功。
- 找到正確建置工具鏈，確認 VS/MSBuild Release build 是否可過。

P1：
- 用 `ffprobe` 或 ffmpeg `-progress` 取代 Duration * 30 與掃檔案數的進度估算。
- 補暫存保留 UI/設定，或同步 README 移除該描述。
- MD5 改 SHA-256。
- 修 `deltree` 成功回傳 true，並保留失敗原因。

P2：
- 新增 x1 不放大倍率：可跳過 Real-ESRGAN 或只做轉封裝/重編碼流程。
- 將魔術數字進度區間 0-15 / 18 / 22-87 / 88-98 集中成常數。
- 將音訊檔名 `wav_file` 改成更通用的 `audioFile`，降低後續誤讀。
- 發佈流程自動化：Release build、copy binary、zip、checksum、掃毒記錄連結。

### 下次接手建議

若下一輪要直接修，建議先做「process runner + step2 穩定化」這一包，效益最大且不會大幅改 UI。修改前先跑一次目前範例影片或短測試影片，保留 baseline；修改後至少驗證：

- 正常 MP4 可完成輸出。
- 取消轉檔能終止 ffmpeg / Real-ESRGAN。
- 來源影片無音訊時能得到清楚錯誤或可跳過音訊。
- Real-ESRGAN 執行失敗時不會假成功。
- 暫存資料在成功/失敗/取消三種情境都符合預期。

## 2026-10-06 Native DLL 整合可行性與 Phase A/B 範圍

- 使用者確認 Phase A：取消 ffmpeg / Real-ESRGAN 外部 exe 呼叫，保留 PNG 暫存與現有轉檔流程；Phase B 再檢視舊程式改良空間。
- 可行方向：FFmpeg.AutoGen + 匹配的 FFmpeg shared DLL；Real-ESRGAN C++ 核心另建 C ABI DLL，C# P/Invoke 呼叫。DLL／模型仍需交付，並非單一 exe。
- 設計草案：docs/superpowers/specs/2026-10-06-native-dll-integration-design.md。尚未實作，也未凍結新套件／native binary 版本。
- 現有程式已含 2026-05-27 後續 P0/P1/P2：step2 ProcessRunner/-progress、SHA-256 去重、x1、保留暫存、build resource 相容設定。上方「初評估」不可當作今天尚未修復的清單。
- 本機驗證：Release build 成功（0 warnings/errors），ProcessRunnerTests PASS，UtilityTests PASS（既有 CS8981 警告）。初次 --no-restore 缺 System.Resources.Extensions；還原既有 NuGet 相依後重試通過，未修改產品程式。
- ESRGAN source 位於 D:/mytools/Real-ESRGAN-ncnn-vulkan，HEAD 37026f4；已有 load/process 核心與 MIT license。ncnn/libwebp 子模組未初始化，Vulkan SDK 尚未定位；MSVC/CMake/runtime 存在，不代表 native build 已通過。
- Phase A 必須處理 x64/ABI、native 錯誤回傳與安全取消，以及 release script 未複製 exe 旁 DLL 的缺口。Real-ESRGAN 模型讀取與不完整初始化清理的防護是 DLL 整合必要範圍。
- 未驗證邊界：新 DLL 編譯、真實 GPU 推論、短片音畫對照、native 取消／記憶體、WinForms 人工操作與完整發佈包；未安裝 SDK、未發布或改動正式環境。

## 2026-10-06 Phase A 設計確認與實作計畫

- 使用者確認 native DLL 整合設計，另允許考慮從 .NET Framework 4.6.2 升至 4.7.2。
- 據 Microsoft .NET Standard 2.0 相容性建議與本機 v4.7.2 targeting pack 盤點，將設計目標改為 .NET Framework 4.7.2 + x64；本輪僅改文件，產品 csproj 尚未切換。
- 已建立 docs/superpowers/plans/2026-10-06-native-dll-integration.md，包含 9 個 task、C#/native 介面、分段驗收與 Phase B 邊界，並完成 self-review。
- FFmpeg 候選已定位：AutoGen 9.0.1.1 + BtbN 2026-09-30 的 LGPL shared 9.0 build，plan 記錄 upstream SHA、asset digest 與 ABI majors。這是公開 metadata/source 查核，尚未下載或執行。
- 本輪未安裝相依、編譯 DLL、修改轉檔程式或執行新 runtime 驗收。前輪 4.6.2 build/tests 通過不可視為 4.7.2/native 組合已通過。
- 目前等使用者檢視實作計畫與選擇執行方式；Phase B 仍在 Phase A 驗收後。

## 2026-10-06 Phase A Task 1：net472/x64 與 FFmpeg runtime

- 在 codex/native-media-dll managed worktree 實作，原 checkout 保留。目標 framework 已改 v4.7.2、platform x64、Prefer32Bit=false，CI/platform/runtime 設定同步。
- 鎖定 AutoGen 9.0.1.1 + FFmpeg LGPL shared 9.0 build；下載 archive 與 7 個 DLL SHA-256 已驗證，binary 保持 artifacts local-only。
- net472 x64 smoke 先以未實作 loader RED，再驗證實際載入、任意 cwd、缺 DLL、錯誤 ABI 拒絕、PNG/AAC/MP3/Vorbis/PCM encoders、libopenh264 單幀 encode/decode 回讀 PASS；Release build 0 warnings/errors。
- Legacy csproj 已 restore NuGet assets 但未自動提供編譯 reference，依既有 System.Resources.Extensions 作法追加 explicit HintPath/netstandard facade；未轉 SDK-style。
- 本階段只完成 runtime，影片各步驟仍是舊流程，不代表 DLL 端到端或 UI/GPU/發佈驗收。

## 2026-10-06 Phase A Task 2：DLL metadata / 30 fps PNG

- 以 libavformat/libavcodec/libavfilter 與 RGB 轉換實作 Probe/ExtractFrames，保留 fps=30 抽補幀；不啟動 ffmpeg.exe。
- 本機 net472 x64 smoke PASS：30fps/24fps 60張、VFR 59張、無音軌影片仍可拆幀、中文空白路徑、預先／進行中取消、損壞輸入、B-frames flush、非零起始 PTS、90度旋轉、4:3 SAR。新測試先在未實作 Probe 處 RED。
- FrameSequence 增加 SAR 以避免 Bitmap PNG metadata 丟失影響最終比例；尚待 Task7 編碼端保留。旋轉 fixture 起初以 rotate tag 製作實際無 display matrix，改用 -display_rotation 並查核 metadata 後重測通過。
- 舊 CLI baseline 已量測 AAC click 比來源延後21.333ms；其他音訊選項沒有該偏移；四種音訊抽取及 MP4 copy-mux 在本機皆成功。這些屬 baseline，不是新音訊 DLL 驗收。

## 2026-10-06 Phase A Task 3: native audio

- Implemented AAC192k, MP3q4, Vorbisq4 and PCM WAV via libavcodec/libavformat and filter-owned resampling/FIFO; no ffmpeg process.
- Native audio smoke PASS: 48kHz mono sample count, click onset within one video frame, no-audio rejection, pre/in-flight cancellation and retry. AAC onset 1.021375s matches legacy priming; other modes 1.000041667s.
- ADTS metadata duration is only an estimate; acceptance uses decoded PCM samples, not that estimate. MP4 mux/UI/package acceptance remains pending.

## 2026-10-06 Phase A Task 4：Real-ESRGAN C ABI DLL

- 固定 Real-ESRGAN/ncnn/glslang/Vulkan headers 與來源 SHA-256，使用 MSVC x64/static CRT 編譯 bridge；不安裝 Vulkan SDK、不替換系統 driver，build-only shader compiler 不隨產品執行。完整授權文字已收錄 native/licenses。
- tools/build_native.ps1 -Configuration Release -RequireGpu 成功，CTest ABI/GPU 2/2 PASS：缺檔／空模型／截斷 param、非法尺度／stride／容量、並行 busy、tile 取消、destroy 中生命週期、重建後推論、模型倍率不符。GPU 為 RTX 5060 Ti。
- 空模型負面測試抓到 pinned ncnn 在載入失敗後仍 create/upload；已用具原始／修補 SHA 的最小 patch 提前失敗並傳遞 GPU wait 錯誤，補 partial-param ownership/null cleanup 及未初始化 GPU flag。
- 編譯踩雷：長來源路徑超過 MSVC include 限制，縮短 cache 目錄；新增 shader 檢查曾造成 dangling-else，灰階對照測試抓到255而非舊版130，補 braces 後通過。
- 完整 script staging 成功；尚未代表 C# PNG 對照、UI、MP4 pipeline、長片或 release 包通過。Native upstream 有既有 CMake deprecation/字碼頁警告。

## 2026-10-06 Phase A Task 5：C# PNG adapter / GPU 對照

- Cdecl P/Invoke、SafeHandle、絕對路徑 DLL loader、BGR/BGRA row copy、negative stride、PNG 完成後發布、取消 delegate lifetime 已實作。Release build 0 warnings/errors；--upscale PASS。
- x2/x3/x4 尺寸、正負 stride／RGB 色序／alpha、模型缺失、Unicode 模型路径、session 重用、tile 取消與新 job 重試皆通過。
- 舊 v0.2 exe 比較：MAE 0.09487576/0.09206211/0.08833143，最大差值皆4/255；未宣稱逐像素一致。另以相同 pinned ncnn／shader／compiler 編譯未修改上游核心，三倍率均與 DLL MAE0/max0。tools/prepare_realesrgan_reference.ps1 可重現，diff32 圖留 artifacts。測試要求 rebuilt-reference 精確一致，legacy max4/MAE0.12 內。
- GPU alpha 路徑在新舊 core 都觸發 VK_ERROR_DEVICE_LOST；BGRA 改以 ncnn CPU bicubic 處理 alpha，GPU RGB 算法不變。小於11x11輸入明確拒絕，避免原核心單次鏡射 padding 越界。
- 本機 native/managed 圖片驗證通過；WinForms 還未改用這些元件，MP4 完整流程與發佈／長片仍 pending。

## 2026-10-06 Phase A Tasks 6–8：完整 DLL 流程與取消

- step2–step5 全部改為 managed/native DLL 呼叫；維持 PNG、30fps、SHA-256 去重、x1–x4 與四種音訊。x1 不載入 AI DLL；確定重複的測試為59次推論補回60張，來源PNG保留供診斷。
- native MP4 完整回讀影音後才原子替換目標；錯誤與取消保留既有目標 bytes。四音訊 click 偏移0.04ms／AAC21.375ms，均在1/30秒內。x1小尺寸 NVENC 真正初始化失敗後回退 OpenH264；x2–x4 真正以 NVENC 完成60張短片。奇數寬高 yuv420p 明確拒絕。
- WinForms 單一 CTS/activeJob、GUID 工作目录、停止後等待、關窗 await worker/native cleanup；移除 Environment.Exit 與每階段重設 CTS/阻塞延遲。失敗／取消的非保留暫存於 awaited worker 結束後清理。
- UI 啟動測試發現 Resources.Extensions 4.0.0.0/4.0.1.0 繫結不符，補正確 binding redirect；真實 WinForms message loop 關窗等待測試 PASS。native decode/audio/upscale 取消後重試與 Release build PASS，兩組既有測試 PASS。
- 以上為自動化本機證據；真人操作、完整乾淨 release package、長片與記憶體趨勢仍待 Task9。未發布、未push。

## 2026-10-06 Phase A Task 9：乾淨打包、原生生命週期

- 發佈包明列 managed DLL、FFmpeg七個 shared DLL、bridge、六個模型檔、config、授權／provenance／逐檔SHA；排除舊CLI與OpenMP runtime。版本字串限單一安全檔名、遞迴刪除只允許指定output root的嚴格子目錄。
- 真正隔離AppDomain／異地cwd測試找到 System.Memory 等間接相依未被舊式 csproj複製，已追加明確reference；缺managed/native檔案皆明確拒絕，x1包可啟動WinForms並跑產品step1–step6。
- FFmpeg loader改先用原生exports查ABI/config/license，失敗逆序卸載；一旦AutoGen binding開始則保持modules並鎖定失敗，避免快取函式指標指向已卸載DLL。錯誤ABI後同程序載入正確runtime PASS。
- 第一輪同程序五短片＋10分鐘片（18,000幀）全部成功，private peak458.96MiB，長片後264.07MiB；GPU使用回低值。但每短片+6handle，因此不以該輪宣稱無洩漏。
- 根因1：glslang InitGlobalLock覆寫CreateMutex未關閉，改單一程序生命週期recursive lock。根因2：不含AI的raw Vulkan device create/destroy仍每次+5（Event3/Mutant1/Section1），定位在本機loader/ICD/layer路徑，不能單獨指認某家driver。
- bridge保留ncnn程序級GPU runtime；每job模型/session釋放，最後worker結束後清空idle blob/staging pools。新增rs_shutdown於程式Main finally／測試host unload前呼叫，避開ncnn/glslang全域解構順序造成exit-time crash。shutdown遇活躍session回busy。CTest ABI/GPU2/2正常exit0，五次GPUjob handle405→405。
- GitHub Actions已更新native build／CPU smoke／乾淨package驗證；runner沒有系統Vulkan loader時ABI明列NOT-RUN。尚未push，未取得遠端CI執行證據。
- WMI process-start事件訂閱被本機拒絕存取；未提權，因此沒有事件級「零子程序」證據。已查核app/include六個原callsite消失，發佈包沒有兩支exe，並實際執行DLL流程。
- 真人按鈕操作／其他GPU與高解析度長片仍未驗證；本輪不發布。公開散布前須另外組裝固定FFmpeg與其外部相依完整對應source bundle；目前授權全文與固定來源／建置資訊已隨本機包提供。

### Task 9 最終本機驗收與獨立審查

- 最終runtime同程序重跑五次短片＋10分鐘片全部PASS／exit0，總234.345秒；長片205.412秒、18,000幀、去重2張（低解析度fixture，不能當4K長時間GPU負載驗收）。五短片idle handles均657，長片後648，沒有逐job增長。
- 最終private peak509.94MiB、五短片idle約401–411MiB，長片後394.39MiB；GPU dedicated peak225.53MiB、shared180.20MiB、committed405.79MiB。程序級runtime idle約106.03MiB dedicated，約208MiB committed保持平臺；這是刻意保留的device/cache，不宣稱每job GPU歸零。226次samples有224次GPU有效，缺值沒有當零。
- 六個成品另用舊CLI獨立CPU decode至EOF，影音皆可解碼，五短片各60幀／2秒，長片18,000幀／600秒，無decode錯誤。CLI僅屬測試工具，產品包仍不含CLI。
- 完整managed smoke（runtime/ABI rollback/decode/audio/upscale/pipeline/mux/lifecycle）exit0、native CTest2/2 exit0、乾淨包x1/x2（含真實產品step1–step6及WinForms進度）exit0；UtilityTests、ProcessRunnerTests PASS。Release managed build0warnings/errors；UtilityTests既有CS8981保留。
- 新鮮獨立reviewer只讀審查a3f1528..903c170，未發現Critical／Important問題；未再啟動review迴圈。實作位於codex/native-media-dll；原checkout的history/docs dirty狀態完整保留，未合併、push或發布。
- 完整raw證據位於ignored artifacts/acceptance/endurance-monitor-20261006-131620-abc7b189（含DLLhash、log、CSV、獨立decode結果）；本機測試包位於artifacts/release/my_cartoon_beautiful_native-dev。
- 已知驗證邊界：真人操作、不同GPU／高解析度長片、遠端CI、WMI事件級子程序追蹤，以及公開發布用完整對應source bundle仍未取得；Phase B舊程式改良留待Phase A實際試用後。

## 2026-10-06 切回原專案供人工試用

- 依使用者要求，原 checkout D:/mytools/my_cartoon_beautiful 已切至 codex/native-media-dll（c5fac23）；managed worktree 改為同一 commit 的 detached HEAD，保留原始建置與驗收資料。
- 原 main 未提交的 history.md 與 docs/ 已完整保存至 Git stash 156db88d1cd9a35f3adb97b1cc7ea40b96c9ef0c（preserve-main-design-docs-before-native-trial-20261006）；目前分支已有更新後文件，未套回舊版。
- 將已驗證 native runtime 複製到原 checkout 的 ignored artifacts/native-runtime；7 個 FFmpeg DLL manifest hash 與 bridge SHA256 複製比對通過。
- 原路徑 Release x64 build 成功，0 warnings/errors；artifacts/release/my_cartoon_beautiful_native-trial 乾淨試用包 layout/hash PASS，可直接執行其中 my_cartoon_beautiful.exe。
- 本輪僅驗證切換後編譯與試用包，不重複宣稱人工操作或其他 GPU 已驗收；未啟動 GUI、merge、push 或發布。

## 2026-10-06 使用者人工試跑回饋

- 切至 codex/native-media-dll 並提供本機試用包後，使用者回覆「可以耶，這樣安全多了」，確認本次實際試跑可用。
- 此為使用者回報的本機操作證據；未另提供測試影片、倍率、音訊選項、取消操作或輸出檢查細節，不擴大為全部情境與其他 GPU 已驗收，也不視為安全稽核結果。
- Phase B 舊程式檢視以目前 DLL 整合版本為基準；本輪僅補記回饋，未修改產品程式。
- 使用者後續明確回報「成功轉檔了」：本機人工驗收已確認完成一次轉檔；影片規格、倍率、音訊選項與播放品質細節未提供，其他情境仍維持上述驗證邊界。

## 2026-10-06 v0.05 版本升級

- 使用者確認 DLL 版成功轉檔後，核准升為 v0.05，並要求同步 README.md。視窗／通知圖示／關於版本升至 0.05；AssemblyVersion／FileVersion 採 Windows 四段格式 0.5.0.0，ProductVersion 為 0.05。
- README 更新日期、目前版本、DLL 相依說明、v0.05 更新紀錄與打包指令；舊公開下載連結保留為歷史版本，未虛構 v0.05 公開下載網址。
- Release x64 與 NativeMediaSmokeTests build 成功，0 warnings/errors；新包 isolated x1/x2（GPU required）完整轉檔 PASS／exit0，含真實產品 steps 1–6；證據位於 artifacts/acceptance/package-65e8f2e4331741d7821ca9c419b485df。
- 新 EXE 的 FileVersion 0.5.0.0、ProductVersion 0.05、AssemblyVersion 0.5.0.0 已實讀確認；package layout／逐檔 SHA 通過。既有 UTF-8 BOM 與 CRLF 格式維持，git diff --check 通過。
- 本機發佈產物位於 ignored artifacts/release/my_cartoon_beautiful_v0.05；維持 codex/native-media-dll 分支，不合併 main 或上傳 GitHub。公開 binary 發佈仍需完成先前記錄的 FFmpeg 完整對應 source bundle；其他 GPU／高解析度長片與遠端 CI 邊界不變。

## 2026-10-06 v0.05 合併 main 與推送範圍

- 使用者明確核准「併回 main 可以上 git push」。fetch 確認遠端 main 與原本機 main 同為 a3f1528，無遠端 v0.05 標籤；本機 main 已 fast-forward 至 v0.05 的 50e5d25，無衝突。
- 此次核准推送 main 與既有 v0.05 標籤，標籤保持指向已通過本機與使用者驗收的 50e5d25；不重寫歷史。83 個整合變更檔案皆為 source/docs/licenses/scripts，沒有新增追蹤 DLL、EXE、模型、ZIP 或驗收產物。
- 發現 main CI 會自動上傳整包 native binary；為符合前述尚待完整 FFmpeg source bundle 的發佈邊界，本輪移除 upload-artifact 步驟，保留 native/managed build、CPU tests 與隔離 package 驗證。README 同步說明。
- 沿用 v0.05 Release build 0 warnings/errors、isolated x1/x2 PASS 與使用者成功轉檔證據；本輪合併與文件／CI 上傳調整不改產品程式。遠端 CI 尚待本次推送後執行；GitHub Release／ZIP 公開下載不在本次操作內。
- 原設計文件 stash、managed worktree 與 ignored native runtime／本機 ZIP 全部保留。

## 2026-10-06 GitHub Actions 遠端編譯與手動入口

- 使用者要求由 GitHub Actions 自行編譯。已查核首次主線 run 37421190723（380ef9d）success，runner 實際從固定來源編出 ncnn／Real-ESRGAN bridge，再完成 managed Release build，非只使用本機成品。
- 遠端 native ABI 1/1 PASS；managed build 0 warnings/errors，四種音訊／decode／runtime／ProcessRunner／Utility PASS；乾淨 package layout/hash 與隔離 x1／產品 steps 1–6 PASS。原始 log 留存 ignored artifacts/acceptance/github-actions-37421190723/run.log。
- workflow 增加 workflow_dispatch，可在 Actions 的 Run workflow 選 main 執行；同一 ref 新 run 取消舊 run，避免 push 與手動重複建置。新增 always 執行的 Summary，列 native、managed/CPU、x1 package 結果及產品版本。README 同步使用入口。
- 本機 YAML 結構、inline PowerShell 語法與 Summary 執行檢查通過；接著推送並實際觸發手動 run 驗證。二進位 artifact upload 維持關閉。
- hosted runner 缺 nvcuda.dll 並成功回退軟體 H.264；GPU inference／NVENC／長時間 GPU 驗收仍屬本機邊界。Node20 actions 被 runner 切至 Node24 的警告未造成失敗，本輪不順帶升級 action majors。
- 手動入口實測完成：workflow_dispatch run 37423452118（https://github.com/shadowjohn/my_cartoon_beautiful/actions/runs/37423452118）在 785cc5b 上 success，build job 7m50s。native ABI 1/1、managed／CPU tests、isolated x1 產品流程及 Write build summary 全部 success。
- 同提交的 push run 37423441204 已由 concurrency 自動取消，確認避免重複建置生效。完整 run.json／run.log／watch.log 保存在 ignored artifacts/acceptance/github-actions-37423452118；遠端 runner 的 GPU inference／NVENC 仍明列 NOT-RUN。
- 本次收尾只追加驗收紀錄，以 [skip ci] 文件 commit 保存，避免重新建置相同產品與 workflow；實際通過遠端驗證的程式／workflow commit 為 785cc5b。

## 2026-10-06 v0.05 公開發布前的相依授權修正

- 使用者核准建立 v0.05 GitHub Release 並更新 README；已建立 draft，僅先放入原始碼 companion，尚未公開或上傳 FFmpeg runtime binary。
- 實際查核發現原 BtbN LGPL shared DLL 含 GPL 的 FFTW：固定 chromaprint recipe 以 FFT_LIB=fftw3 建置，avformat-63.dll（SHA256 7275053ebb1dfa708d45d8544ad94bf664e0cb534cd0f2ed979fdf024bc190b3）內有 FFTW 3.3.11／codelet 字串。不能只依 avutil_license 的 LGPL 字串判斷整包授權；先前 LGPL-only 敘述因此需修正。
- 證據位於 ignored artifacts/release-publication-v0.05/ffmpeg-license-audit；上游 Chromaprint README 明列 FFTW 會使其 binary 適用 GPL，FFTW 固定源碼標示 GPLv2-or-later。
- 使用者選擇「維持 LGPL，重編不含 GPL 相依的 FFmpeg」。重編將停用未使用的 Chromaprint／FFTW，並移除 LCMS 可選 GPL plugin 的靜態庫與 pkg-config 引用，保留 MIT LCMS core；最後需檢查 link map／DLL，不能只檢查 configure flags。
- 已向使用者說明 libx264／libx265 在 LGPL 組合停用；libfdk_aac 為獨立授權，並非 LGPL 必然排除，本案本來就未使用。現有 H.264 為 h264_nvenc／libopenh264，AAC 為內建 aac，並保留原生 H.264／HEVC／AAC 解碼；不因重編新增編碼器。
- 原建置 image 已被上游清除，將以固定的新 image／recipe 配同一 FFmpeg source 重建，對應 source pins 與 runtime SHA 需同步更新；舊 runtime 不公開。
- 來源快取下載／展開曾用盡 D: 可用空間；僅清理本輪重複下載分片後已恢復空間，後續大型來源與重編工作移至 C: managed worktree 的 ignored artifacts，保留所有專案與使用者資料。
- 已補 Microsoft packages 隨附的兩份額外 THIRD-PARTY-NOTICES；完整來源、重編後 tests、最終 public Release／README 連結仍待完成。

### 重編驗證與本機防毒提示

- 重編採 FFmpeg 2a571b606854520cf89804d8030c8b328e621689、BtbN recipes 9acad4a9ef1583096af7836cc1e9c8cbcb4d3950 與 image digest e0b0c4e3ff1dc7f5529b6174398c212dfa1358ec9f9a802e22e5cacba872032b；固定 image 的上游 Actions 來源已對應確認。建置腳本保存完整 configure／link trace／DLL hashes，並以 .gitattributes 保持 Docker Bash 腳本 LF。
- 使用者於重編驗證時收到 Trend Micro 對 ffmpeg.exe 的新程式／勒索行為攔截提示。已確認本輪曾從 managed worktree 的 artifacts/lgpl-ffmpeg-rebuild/bin 呼叫開發用 ffmpeg.exe -hide_banner -version 與 -encoders；隨後停止所有新 CLI 呼叫，沒有允許清單、繞過或調整防毒設定。此畫面不作為惡意或誤判定論。
- 產品與後續驗收使用 DLL；開發編譯產出的 ffmpeg／ffprobe／ffplay EXE 不納入 runtime／產品 ZIP。CLI SHA256 8737b4bd2c22c1d801786545d6b977eb313f8b0dd91fec48e927c2c7a2c8010f 僅供本機事件對照，不是發佈資產。

### LGPL 重編後的發佈驗證

- 新 runtime archive SHA256 為 10c74804c7dcb42cbc827685d9b0ef19cc5ebf7a5e7a92ab1ea908cbc6218223；七個 DLL hashes 已更新 lock，下載快取改以 archive SHA 命名。ZIP 讀回確認 7 DLL／0 EXE，GitHub draft asset 的 size／digest 相符。
- final link trace、DLL marker 與 libplacebo／libjxl／libjxl_cms 靜態庫符號查核均未見 FFTW／Chromaprint／LittleCMS GPL plugin；imports 僅 Windows 系統與七個 FFmpeg DLL。avutil／avcodec exports 回報 LGPLv3；不是只憑授權字串判斷相依。
- 靜態相依授權通知 1,516 個檔案、8,893,098 bytes，來源／逐檔 SHA 已驗證並納入兩種 binary ZIP；以 Git attributes 保留第三方通知原始 bytes。對應來源已組裝為 FFmpeg core/build-records ZIP 加 80 個相依 archive ZIP，另含三個更新來源與 Rust crates；大型 archive 僅放 Release，不進 Git。
- managed Release x64 與 NativeMediaSmokeTests build：0 warnings/errors。新 DLL smoke exit0：H.264／HEVC／AAC decoder presence、software H.264 roundtrip、decode、四種 audio、pipeline、mux x1–x4、NVENC fallback、取消／失敗保護及關窗等待均 PASS。
- 五次短片與十分鐘低解析度影片全部完成；長片 18,000 幀、去重 2 張。五次短片 idle handles 為 652/652/652/652/654，長片後 643，未呈現逐 job 持續增加；這不是高解析度 GPU 壓力或所有平台無洩漏保證。詳細 jobs.csv 與 full-native-smoke.log 位於 managed worktree ignored artifacts/acceptance。
- 新乾淨包 layout／逐檔 SHA、異地 cwd／隔離 AppDomain 的 x1/x2 真實產品 steps 1–6 PASS／exit0；證據 artifacts/acceptance/package-2cb00c89c0ed4ddb9d1416f872cb316f。獨立來源／linkage／腳本 review 未發現其他需修正問題。
- 本機真人成功轉檔是先前 runtime 的回饋；此次重編取得上述自動化證據，其他 GPU／高解析度長片／重編後真人操作未另驗證。接著需把 draft 的 v0.05 tag 對齊本次修正 commit、替換 source companion、發布並重跑遠端 Actions；原 tag commit 保留在主線歷史。

## 2026-10-06 v0.05 正式發布

- 已依使用者核准將 LGPL 修正提交 aeefc67125527ca4abf9f6dc4fe61bdc3e9e49fb 推至 main；v0.05 tag 從原先 50e5d25 對齊該提交。推送前比對遠端 main/tag，採 atomic push，僅 tag 使用精確舊 SHA 的 force-with-lease；main 為 fast-forward，原提交仍保留主線歷史。
- GitHub Release 已於 2026-10-06 07:50:35 UTC 公開並設為 latest：https://github.com/shadowjohn/my_cartoon_beautiful/releases/tag/v0.05 。共 10 個附件；主程式 ZIP 75,702,699 bytes，SHA256 cfb0ce4fc60baf4e75ed6f25686c3329243195006b16ac271373aec7d3991704。
- 最終主程式包與先前通過 isolated x1/x2 的 1,562 個 payload 檔案逐檔相同，只更新 generated package manifest 的 commit/time；layout/hash 再次 PASS。一般使用者僅需主程式 ZIP，產品與獨立 FFmpeg runtime ZIP 均無 FFmpeg CLI EXE。
- application/native source companion 共 1,636 檔案，包含該 commit 的 1,629 個 Git blobs（含所有通知與六個模型）、五個固定上游輸入及 README/manifest；Git blob／逐檔 SHA／ZIP CRC PASS。SHA256 3db33358b710e352ee27130687b08d5aad220a030a658575ba44520f34c4a36d；draft 舊來源附件已替換。
- FFmpeg source ZIP SHA256 01311161f0f4f36b3e1712663b8877a2dc30940de5f251445a80f5f61721f6a6；80 個相依 archive ZIP SHA256 9e96c78d72ca2c1c31d5727ae8bf6efd920a6cab051924781353b98de68b8072。SOURCES.md 說明對應／重建方式；SHA256SUMS.txt 的 9 筆內容與 GitHub 各 asset digest 全數相符。
- 公開主程式／runtime／checksum URL 均 HTTP 200；下載公開 checksum 檔與本機 bytes 相同，公開 README Git blob 與 aeefc67 完全一致，latest API 確認非 draft 的 v0.05。完整記錄在 managed worktree ignored artifacts/release-v005-lgpl。
- 新 public runtime 的 workflow_dispatch run 37432233617（head aeefc67）已 success，job 7m59s：https://github.com/shadowjohn/my_cartoon_beautiful/actions/runs/37432233617 。native ABI 1/1、app/smoke build 0 warnings/errors、audio/decode/runtime、ProcessRunner、Utility、package layout/hash 與 isolated x1 全數 PASS；log 實際回報 20261006-lgpl-no-gpl-deps 新 runtime。
- Hosted runner 缺 nvcuda.dll，成功回退 OpenH264；遠端 GPU inference／NVENC 為 NOT-RUN。本機 NVENC x2–x4 已通過；其他 GPU／高解析度長片／重編後真人操作邊界仍保留。非失敗警告包括上游 CMake deprecation、UtilityTests 既有 CS8981 與 action Node20→24／punycode；完整遠端 log/JSON 位於 artifacts/acceptance/github-actions-37432233617。
- 原 checkout 的 bin/Release 七個 DLL hashes 與新 lock 一致；artifacts/release/my_cartoon_beautiful_v0.05 及 ZIP 已同步公開包，layout/hash PASS。之前同名本機產物移至 artifacts/release/superseded-v0.05-before-lgpl-rebuild-20261006 保留，未刪除。
