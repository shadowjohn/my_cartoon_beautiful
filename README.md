# my_cartoon_beautiful
我的影片清晰機，可以把老影片變高清的好工具

<h3>開發動機：</h3>
　　前陣子在網路上發現 https://github.com/xinntao/Real-ESRGAN-ncnn-vulkan
這個可以針對卡通圖片變成高清圖片的 project，產出的品質相當好，覺得可以整合加以利用。<br>
　　想法如下：<br>
	<ul>
		<li>1. 用 ffmpeg 把影片 → 單張圖片 (一秒30幀)</li>
		<li>2. 用 ffmpeg 把影片輸出聲音 (mp3)</li>
		<li>3. 用 Real-ESRGAN-ncnn-vulkan 把第 1 步驟作出的圖片，轉成高清圖片</li>
		<li>4. 用 ffmpeg 把高清圖片、mp3檔合併為新影片</li>
		<li>5. 刪除處理過程的暫存檔</li>
		<li>6. 將以上步驟作成 C# 小程式</li>
	</ul>
	<br>
　　最近在練習 C# 單機程式開發，當作練習作業
<h3>版本：V0.01</h3>
<h3>作者：羽山秋人 ( https://3wa.tw/ )</h3>
