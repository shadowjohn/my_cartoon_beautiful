# my_cartoon_beautiful
我的影片清晰機，可以把老影片變高解析度的好工具

## 開發動機
前陣子在網路上發現 [Real-ESRGAN-ncnn-vulkan](https://github.com/xinntao/Real-ESRGAN-ncnn-vulkan) 這個可以針對卡通圖片變成高解析度圖片的 project，產出的品質相當好，覺得可以整合加以利用。

轉檔速度不是很快，需要有點耐心^^... 有較好的顯卡GPU才能加快速度。

想法如下：
1. 用 ffmpeg 把影片 → 單張圖片 (一秒30幀)
2. 用 ffmpeg 把影片輸出聲音 (wav)
3. 用 Real-ESRGAN-ncnn-vulkan 把第 1 步驟作出的圖片，轉成高解析度圖片
4. 用 ffmpeg 把高解析度圖片、wav檔合併為新影片
5. 刪除處理過程的暫存檔
6. 將以上步驟作成 C# 小程式

最近在練習 C# 單機程式開發，當作練習作業。

## 作者
羽山秋人 ( [https://3wa.tw/](https://3wa.tw/) )

## 聯絡方式
信箱：<a href="mailto:linainverseshadow@gmail.com">linainverseshadow@gmail.com</a>

## 版權
完全免費的 MIT-License

## 版本資訊
- 最初開發日期：2024-07-28
- 最後更新日期：2024-07-28
- 版本：V0.01

## 下載位置
- 主程式(V0.02 beta版)：[下載連結](https://raw.githubusercontent.com/shadowjohn/my_cartoon_beautiful/master/release/V0.02/my_cartoon_beautiful.zip)
- 主程式(V0.01 穩定版)：[下載連結](https://raw.githubusercontent.com/shadowjohn/my_cartoon_beautiful/master/release/V0.01/my_cartoon_beautiful.zip)

## 執行畫面
![執行畫面](snapshot/s1.png)

## 影片產出範例
![影片產出範例](snapshot/s2.png)
[範例瀏覽](https://github.com/shadowjohn/my_cartoon_beautiful/tree/main/example)

## 使用方法
1. 選擇來源影像
2. 選擇要存在哪
3. 選擇影像放大倍數 x2 x3 x4 (越大越久...)
4. 按下開始轉檔，等待成果

## 程式相依套件
1. ffmpeg windows binary static (ffmpeg version N-116451-ge7d3ff8dcd-20240729)
2. realesrgan-ncnn-vulkan (v0.2.0-windows)
3. windows .net framework 4.6.2

## 版本說明
V0.01 版 (2024-07-28)：初版簽入
通過微軟掃毒：[掃毒結果](https://www.microsoft.com/en-us/wdsi/submission/7690a33f-7234-4f4d-87cf-a86ae0e42491)

V0.02 版 (2024-08-04)：更新內容
 1. (2024-08-04 Done) 【V0.02】轉檔時，每個步驟有開始、結束時間
 2. (2024-08-03 Done) 【V0.02】自動切換 h264 或 h264_nvenc
 3. (2024-08-03 Done) 【V0.02】轉檔時，可以選擇是否要保留暫存檔
 4. (2024-08-03 Done) 【V0.02】在處理 step4_sourcePng_to_aiPng ，先判斷是否有重複檔案，有的話就不處理，重複檔案用 md5_file 檢查
 5. (2024-08-04 Done) 【V0.02】轉檔過程時，直接按 X 離開，還在轉檔的 ffmpeg 或 real-esrgan-ncnn 並沒有結束
 6. (2024-08-04 Done) 【V0.02】文字調整，影片檔分離 wav 改成 影片分離聲音
 7. (2024-08-04 Done) 【V0.02】轉檔時，忽然按下 X 結束程式會先詢問使用者是否要強制結束，再結束程式
 8. (2024-08-04 Done) 【V0.02】最後加上 總時間 結算時間
 9. (2024-08-04 Done) 【V0.02】讓使用者自行指定原音，或是轉 mp3 可減少檔案大小
10. (2024-08-04 Done) 【V0.02】結束時的 MessageBox 置頂
11. (2024-08-04 Done) 【V0.02】滑鼠經過 ㊉ ㊀ 會變成手指指標


## 參考資料
1. [ffmpeg](https://www.ffmpeg.org/download.html)
2. [realesrgan-ncnn-vulkan](https://github.com/xinntao/Real-ESRGAN-ncnn-vulkan)

## 待處理
 1. (2024-08-04 Done) 【V0.02】轉檔時，每個步驟有開始、結束時間
 2. (2024-08-03 Done) 【V0.02】自動切換 h264 或 h264_nvenc
 3. (2024-08-03 Done) 【V0.02】轉檔時，可以選擇是否要保留暫存檔
 4. (2024-08-03 Done) 【V0.02】在處理 step4_sourcePng_to_aiPng ，先判斷是否有重複檔案，有的話就不處理，重複檔案用 md5_file 檢查
 5. (2024-08-04 Done) 【V0.02】轉檔過程時，直接按 X 離開，還在轉檔的 ffmpeg 或 real-esrgan-ncnn 並沒有結束
 6. (2024-08-04 Done) 【V0.02】文字調整，影片檔分離 wav 改成 影片分離聲音
 7. (2024-08-04 Done) 【V0.02】轉檔時，忽然按下 X 結束程式會先詢問使用者是否要強制結束，再結束程式
 8. (2024-08-04 Done) 【V0.02】最後加上 總時間 結算時間
 9. (2024-08-04 Done) 【V0.02】讓使用者自行指定原音，或是轉 mp3 可減少檔案大小
10. (2024-08-04 Done) 【V0.02】結束時的 MessageBox 置頂
11. (2024-08-04 Done) 【V0.02】滑鼠經過 ㊉ ㊀ 會變成手指指標

