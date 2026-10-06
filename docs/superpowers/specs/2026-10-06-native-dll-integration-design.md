# Native DLL integration — Phase A 設計草案

日期：2026-10-06。狀態：使用者已確認整合設計，並允許評估升級 4.7.2；本輪據相容性查核採 4.7.2 作實作目標，尚未修改產品程式或完成 native 驗證。

## 已確認的目標

- Phase A：取消執行時對 ffmpeg.exe、realesrgan-ncnn-vulkan.exe 的外部呼叫，保留 PNG 暫存與現有轉檔流程。
- Phase B：Phase A 驗收後，再檢視以前寫的程式，提出有證據的改良項目。
- 保留 WinForms、六個步驟、30 fps、x1/x2/x3/x4、PNG 檔案 SHA-256 去重與補回、保留暫存選項。
- 「整合」定義為主程式直接載入 managed/native DLL；模型與 DLL 仍隨程式交付，不以單一 exe 為目標。

## 可行性結論與方案比較

| 方案 | 是否符合取消外部 exe | 評估 |
| --- | --- | --- |
| 封裝 FFmpeg 命令列的 NuGet wrapper | 否 | 不能只因為改用 NuGet 就視為完成；必須確認實際呼叫 DLL |
| FFmpeg.AutoGen + FFmpeg shared DLL；Real-ESRGAN C ABI DLL + P/Invoke | 是 | 建議方案；可保留現有 UI 與 PNG 工作流程 |
| 同時改成解碼、推論、編碼的記憶體流水線 | 是 | 改動時間戳、佇列、取消與記憶體管理；不納入本輪 |

FFmpeg.AutoGen 是低階 native binding，需自行用 libavformat/libavcodec/libavfilter/libswscale/libswresample 組出原來的操作；不是把原命令字串傳入 DLL 就能完成。

Real-ESRGAN 現有 RealESRGAN::load/process 已與 main.cpp 分離，可另建 SHARED target。保留 C++ 推論核心，不改寫成 C#。

## 現有程式與替換位置

路徑相對 repository root D:/mytools/my_cartoon_beautiful。

| 位置 | 現況 | Phase A 替換 |
| --- | --- | --- |
| my_cartoon_beautiful/App_Code/include.cs:520 | FFmpeg report 的 Duration × 30 | libavformat 讀 duration/time base，總數僅作估計 |
| my_cartoon_beautiful/App_Code/app.cs:50 | FFmpeg fps=30 拆 PNG | 解碼、fps filter、色彩轉換、PNG 序列寫入 |
| my_cartoon_beautiful/App_Code/app.cs:178 | 分離／轉碼 AAC、MP3、Vorbis、WAV | native 音訊解碼／resample／編碼與封裝 |
| my_cartoon_beautiful/App_Code/app.cs:299 | 目錄交給 Real-ESRGAN exe | C# 逐張讀 PNG，重用 native session 推論，再寫 PNG |
| my_cartoon_beautiful/App_Code/include.cs:497 | 列 encoder，搜尋 h264_nvenc 字串 | native 查詢並實際初始化 encoder |
| my_cartoon_beautiful/App_Code/app.cs:568 | PNG + audio 合成 MP4 | native 視訊編碼、音訊 packet remux、MP4 mux |

app.cs 保留流程協調；新增專注於上述操作的 managed adapter。進度改由實際 frame/tile/packet 處理回報，不再解析子程序 stdout 或掃輸出檔猜完成。

## FFmpeg 整合設計

- 採用 .NET Framework 4.7.2（使用者於 2026-10-06 允許考慮升級）。Microsoft 對使用 .NET Standard 2.0 程式庫的 .NET Framework 專案建議至少 4.7.2，本機已有 v4.7.2 targeting pack。維持既有 WinForms 架構，實際 DLL 載入仍須驗證。
- 主程式、native libraries、建置與打包統一為 Windows x64，取消 AnyCPU 的不確定位元數。
- 選定一組 FFmpeg.AutoGen 與匹配的 shared libraries 後鎖版，記錄來源、版本、checksum、configure 與授權；禁止獨立升級其中一端。盤點時 NuGet 公開版本為 9.0.1.1，此資訊不代表已測試該套 DLL。
- 保留 fps=30 的抽／補幀語意、%08d.png 連續檔名、最終 yuv420p；不能把每次 decode 出來的 frame 直接寫出便宣稱等價。
- 優先初始化 NVENC；不可用時使用經實測可用的軟體 H.264 encoder。現有 binary 具有 libopenh264 而沒有 libx264，選 shared build 時需保留實際可用的軟體路徑。
- 音訊選項保留，逐一驗證輸出容器相容性。舊流程本來失敗的組合需明確回報錯誤，不靜默更換選項或產生假成功；無音軌行為先建立基準，再明確記錄處理方式。
- frame/packet/codec/filter/context 全部有明確釋放與 flush/trailer 流程。取消透過 token 與 FFmpeg interrupt callback、安全檢查點傳遞，不能強制終止執行緒。

## Real-ESRGAN 整合設計

- 將固定 upstream revision 的 source/dependency 納入可重現建置；不可讓正式建置依賴開發者 D:/mytools/Real-ESRGAN-ncnn-vulkan 的絕對路徑。
- 新增 native/realesrgan_bridge 專案，編譯 realesrgan.cpp、bridge 與 shader；不編入 main.cpp 的 CLI。
- 使用 C ABI + opaque handle：create、process、request_cancel、destroy、取得錯誤資訊；不得跨 ABI 傳 ncnn::Mat、std::string、C++ exception。
- C# 用 SafeHandle 管理 session；一個 job 載入模型一次，逐張重用。第一階段單一 job/GPU，不新增並行架構。
- 仍以 PNG 作為各步驟間的磁碟介面。讀 PNG 後交付 BGR/BGRA buffer，明確處理 stride、大小溢位、輸出容量與 alpha；核心目前假設 packed stride=width*channels，不能直接傳入任意 Bitmap stride。
- 模型沿用既有 realesr-animevideov3 x2/x3/x4 .param/.bin，與舊 exe 比對推論；x1 不初始化 ESRGAN/GPU。
- 新 DLL target 可省略 libwebp，因 PNG I/O 放在 managed/FFmpeg adapter；若仍建置上游 CLI 作比對，該 CLI 的 libwebp 相依仍需處理。
- 移入 default GPU、auto tile、prepadding 與全域 ncnn GPU instance 生命週期，保持上游預設語意。
- 必要防護屬 Phase A：模型開啟失敗立即回傳、檢查 load/input/extract 結果、初始化失敗可安全釋放。現有 load() 失敗後仍使用 FILE*，destructor 直接解參考 bicubic 指標，不能原樣暴露給 WinForms。
- 在 tile 與 GPU submission 完成的安全位置檢查取消；等目前 GPU 工作安全結束後釋放。已提交的 GPU 工作不保證立即取消；native crash 也可能使整個應用程式退出，不能宣稱等同原本子程序隔離。

## 發佈與授權

- 發佈腳本目前只複製 exe、exe.config、binary/；Phase A 必須納入 managed DLL、native DLL、模型與實際需要的 runtime。
- 發佈驗收使用乾淨資料夾，移除 ffmpeg.exe/ESRGAN exe 後仍可完成轉檔；不要求終端使用者安装 CMake 或 Vulkan SDK，但 GPU driver/runtime 仍需具備。
- Real-ESRGAN-ncnn-vulkan 的 MIT 著作權與授權文字保留；ncnn 是 BSD-3-Clause，按實際依賴一併整理 notices。
- FFmpeg 自身依 build 為 LGPL/GPL，不能因 wrapper 或主程式是 MIT 就將全部相依標成 MIT。優先評估 LGPL shared build；保留匹配來源與建置資訊。
- DLL 整合不保證影片處理明顯加速；本階段仍有 PNG I/O，主要收益是直接呼叫、錯誤處理與生命週期控制。

## 實作與驗收順序

1. 固定相依與工具鏈，完成 FFmpeg DLL 載入及 metadata 探測、ESRGAN DLL 單張 x2/x3/x4 推論的小型驗證。
2. 接回 FFmpeg 路徑，先完成 x1 + AAC 短片端到端，再接 x2/x3/x4，保留 SHA-256 去重與補幀。
3. 驗證錯誤與取消：缺 DLL、ABI 不匹配、缺模型、損壞媒體、無音軌、NVENC 不可用、取消後再跑、關窗時仍有 native 工作。
4. 短片對照：30 fps、frame 數、解析度、時長差在一 frame 的目標容差內、音畫同步、色彩／方向、音訊選項；同模型的 PNG 像素差異須量測，不能只看檔案存在。
5. 乾淨資料夾驗證發佈包與無子程序；較長影片檢查 GPU/native memory 無持續增長，驗證完成才移除執行時 exe 相依。

以上是設計順序，尚非已完成的實作或測試結果。短片 fixture 與原版本實際輸出需先建立，不能拿原來只有 unit tests 通過當作媒體驗收。

## 本次已取得的證據

- 主 repository HEAD：a3f152853b82c82402eec0739a58540ea283a371；盤點開始工作樹乾淨。
- ESRGAN source HEAD：37026f49824c5cf84062e7c6a5dd71445dcf610f；工作樹乾淨。
- dotnet build my_cartoon_beautiful.sln --configuration Release：成功，0 warnings / 0 errors。第一次 --no-restore 因缺 System.Resources.Extensions 失敗；還原既有相依後重試成功，未改產品程式。
- ProcessRunnerTests：PASS；UtilityTests：PASS（測試建置既有 CS8981 命名警告）。
- 現有 FFmpeg 為 N-116451-ge7d3ff8dcd-20240729；實測 -version 與 encoder 列表，並未以列出 NVENC 視為 GPU 編碼成功。
- 本機找到 MSVC x64、VS bundled CMake 3.31.6、Vulkan runtime；尚未定位 Vulkan SDK/glslangValidator。ncnn/libwebp 子模組未初始化。
- 未下載新 native 相依、安裝 SDK、編譯新 DLL、執行新 GPU 推論、進行 WinForms 人工驗收、完整影片對照或發佈。

## Phase B 待檢視，Phase A 不擴充

- Form1 與轉檔服務耦合、UI 更新與關閉時的生命週期。
- 阻塞等待、catch 吞錯、散落的錯誤判斷與 log。
- 暫存路徑碰撞、部分輸出與清理規則。
- 音訊選項與 MP4 的實際相容性、來源 fps/色彩/方向的處理策略。
- 量測 PNG I/O、去重與推論耗時後，才決定是否需要記憶體串流。
- 先列出具體問題與證據，按影響排序，再決定修改範圍。

## 查核來源

- FFmpeg.AutoGen：https://github.com/Ruslan-B/FFmpeg.AutoGen
- NuGet targets：https://www.nuget.org/packages/FFmpeg.AutoGen/9.0.1.1
- FFmpeg licensing：https://ffmpeg.org/legal.html
- Real-ESRGAN MIT：https://github.com/xinntao/Real-ESRGAN-ncnn-vulkan/blob/master/LICENSE
- ncnn pinned license：https://github.com/Tencent/ncnn/blob/6125c9f47cd14b589de0521350668cf9d3d37e3c/LICENSE.txt
- .NET Standard 相容性建議：https://learn.microsoft.com/en-us/dotnet/standard/net-standard

## 實作驗證後的生命週期補充（2026-10-06）

- 實測 raw Vulkan VkDevice create/destroy 在本機 loader/ICD/layer 每次保留5個 kernel handles，因此 ncnn Vulkan instance/device 採程序級重用；模型/session、job資料與閒置allocator pools仍在每次工作結束後釋放。x1依然完全不啟動GPU runtime。
- C ABI1追加 rs_shutdown()：所有session與worker結束後才可執行，活躍時回-5。產品Main finally／native test／隔離AppDomain probe必須在exit或unload前呼叫，避免上游global destructor ordering。native五次推論後handle405→405。
- glslang固定版的InitGlobalLock曾每次建立未關閉mutex，採可重用程序級recursive lock並以source SHA patch記錄；小型lock維持至程序結束，避免跨translation-unit解構順序。
- FFmpeg YUV420P要求偶數寬高；小於11x11的AI輸入明確拒絕。BGRA alpha採ncnn CPU bicubic，避免已在舊CLI重現的GPU alpha device-lost；RGB模型與shader算術維持上游。
- 未修改上游core配相同工具鏈與DLL逐像素相等；舊v0.2binary最大差4/255，保留有界比較與差異圖，不能描述為舊binary逐像素一致。
- 本機可執行測試包與公開散布分開：包內提供授權全文／版本hash／來源與建置資訊；公開發佈前還需要組裝FFmpeg及其靜態相依完整對應source bundle。
