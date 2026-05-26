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
