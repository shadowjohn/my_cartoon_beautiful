using System;
using System.IO;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using utility;
using utility_app;
namespace my_cartoon_beautiful
{
    public partial class Form1 : Form
    {
        public myinclude my = new myinclude();
        myApp App = null;
        static string PROGRAM_NAME = "影片高解析度小程式";
        static string PROGRAM_VERSION = "0.04";
        public string PWD = "";
        static string TMP_PATH = "";
        //用來取消工作
        private CancellationTokenSource cts;
        private Task activeJob;
        internal bool ClosingAfterJob { get; private set; }
        public Form1()
        {
            InitializeComponent();
        }

        private void notifyIcon1_MouseClick(object sender, MouseEventArgs e)
        {
            ShowInTaskbar = true;
            notifyIcon1.Visible = false;
            WindowState = FormWindowState.Normal;
        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            ShowInTaskbar = true;
            notifyIcon1.Visible = false;
            WindowState = FormWindowState.Normal;
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                ShowInTaskbar = false;
                notifyIcon1.Visible = true;
                notifyIcon1.BalloonTipText = this.Text + " 已縮小...";
                notifyIcon1.ShowBalloonTip(1000);
            }
        }
        private async void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (activeJob == null || activeJob.IsCompleted) return;
            e.Cancel = true;
            if (ClosingAfterJob) return;
            if (!cts.IsCancellationRequested && MessageBox.Show(this, "正在轉檔，取消並等候收尾後關閉？", "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            ClosingAfterJob = true;
            cts.Cancel();
            btnRun.Enabled = false;
            setProgressTitle("正在停止，等候目前工作收尾...");
            try { await activeJob; }
            catch (Exception ex) { Console.Error.WriteLine(ex); }
            if (!IsDisposed) Close();
        }
        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            // 隱藏在任務欄中的圖示
            ShowInTaskbar = false;

            // 隱藏並釋放通知圖示
            notifyIcon1.Visible = false;
            notifyIcon1.Dispose();

        }

        private void button1_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                string lastUsedFolder = Properties.Settings.Default.LastOpenFolder;
                if (!string.IsNullOrEmpty(lastUsedFolder) && System.IO.Directory.Exists(lastUsedFolder))
                {
                    openFileDialog.InitialDirectory = lastUsedFolder;
                }
                else
                {
                    openFileDialog.InitialDirectory = PWD;
                }
                string filters = @"
影片檔案 (*.mp4,*.rm,*.rmvb,*.avi,*.mkv,*.mov,*.3gp,*.webm,*.mpeg,*.mpe,*.ts,*.dat,*.wmv)|*.mp4;*.rm;*.rmvb;*.avi;*.mkv;*.mov;*.3gp;*.webm;*.mpeg;*.mpe;*.ts;*.dat;*.wmv|
所有檔案 (*.*)|*.*
";
                filters = filters.Replace("\n", "").Replace("\r", "");
                Console.WriteLine(filters);
                openFileDialog.Filter = filters;
                openFileDialog.FilterIndex = 1;
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    // 獲取選擇的檔案路徑
                    string filePath = openFileDialog.FileName;
                    //MessageBox.Show("選擇的檔案是: " + filePath);
                    txtSource.Text = filePath;
                    // 保存最後使用的目錄
                    string folderPath = System.IO.Path.GetDirectoryName(filePath);
                    Properties.Settings.Default.LastOpenFolder = folderPath;
                    Properties.Settings.Default.Save();
                }
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            App = new myApp(this);

            this.FormBorderStyle = FormBorderStyle.FixedSingle; // 或者 FormBorderStyle.FixedDialog
            this.MaximizeBox = false; // 隱藏最大化按鈕
            notifyIcon1.Text = PROGRAM_NAME + " - " + PROGRAM_VERSION;
            this.Text = PROGRAM_NAME + " - " + PROGRAM_VERSION;
            PWD = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            TMP_PATH = PWD + "\\tmp";
            if (!my.is_dir(TMP_PATH)) { my.mkdir(TMP_PATH); }

            // preset x 2
            comboBox_ImageScale.SelectedIndex = 1;

            // default aac
            comboBox_soundKind.SelectedIndex = 0;

            // 設定邊框樣式
            labelShowLog.AutoSize = true;
            labelShowLog.BorderStyle = BorderStyle.FixedSingle;
            labelShowLog.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;

            // 使用 Padding 調整內邊距
            labelShowLog.Padding = new Padding(3, 3, 1, 3); // 上下左右各 10 像素的內邊距
        }

        private void button2_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                string lastUsedFolder = Properties.Settings.Default.LastSaveFolder;
                if (!string.IsNullOrEmpty(lastUsedFolder) && System.IO.Directory.Exists(lastUsedFolder))
                {
                    saveFileDialog.InitialDirectory = lastUsedFolder;
                }
                else
                {
                    saveFileDialog.InitialDirectory = PWD;
                }
                saveFileDialog.Filter = @"MP4 檔案 (*.mp4)|*.mp4";
                saveFileDialog.FilterIndex = 1;
                saveFileDialog.RestoreDirectory = true;
                saveFileDialog.Title = "指定檔案名稱";

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    // 獲取指定的檔案路徑
                    string filePath = saveFileDialog.FileName;
                    //MessageBox.Show("指定的檔案是: " + filePath);
                    txtOutput.Text = filePath;

                    // 保存最後使用的目錄
                    string folderPath = System.IO.Path.GetDirectoryName(filePath);
                    Properties.Settings.Default.LastSaveFolder = folderPath;
                    Properties.Settings.Default.Save();
                }
            }
        }
        public void setProgressTitle(string title)
        {
            txtProgressLabel.Text = title;
        }
        public void setProgress(double value)
        {
            //顯示百分比
            txtProgress.Text = string.Format("{0:0.00} %", value);
            int v = (int)Math.Round(value);
            v = (v >= 100) ? 100 : v;
            setProgress(v);
        }
        private void setProgress(int value)
        {
            progressBar1.Value = value;
        }
        private void uiRunOrStop(string RunOrStop)
        {
            switch (RunOrStop)
            {
                case "RUN":
                    this.Invoke((MethodInvoker)(() =>
                    {
                        btnRun.Text = "轉檔中...";
                        btnRun.ForeColor = System.Drawing.Color.Red;
                        progressBar1.Visible = true;
                        txtProgressLabel.Visible = true;
                        txtProgress.Visible = true;
                        button1.Enabled = false;
                        txtSource.Enabled = false;
                        button2.Enabled = false;
                        txtOutput.Enabled = false;
                        comboBox_ImageScale.Enabled = false;
                        comboBox_soundKind.Enabled = false;
                        checkBox_keepTemp.Enabled = false;
                        labelShowLog.Visible = true;
                        switch (labelShowLog.Text)
                        {

                            case "㊉":
                                logDataGridView.Visible = false;
                                break;
                            case "㊀":
                                logDataGridView.Visible = true;
                                break;
                        }
                    }));
                    break;
                case "STOP":
                    this.Invoke((MethodInvoker)(() =>
                    {
                        btnRun.Text = "開始轉檔";
                        btnRun.ForeColor = System.Drawing.Color.Black;
                        progressBar1.Visible = false;
                        txtProgressLabel.Visible = false;
                        txtProgress.Visible = false;
                        button1.Enabled = true;
                        txtSource.Enabled = true;
                        button2.Enabled = true;
                        txtOutput.Enabled = true;
                        comboBox_ImageScale.Enabled = true;
                        comboBox_soundKind.Enabled = true;
                        checkBox_keepTemp.Enabled = true;
                        labelShowLog.Visible = false;
                        //logDataGridView.Rows.Clear();
                        //logDataGridView.Visible = false;
                    }));
                    break;
            }
        }
        private async void btnRun_Click(object sender, EventArgs e)
        {
            if (activeJob != null) {
                if (cts != null && !cts.IsCancellationRequested && MessageBox.Show(this, "停止轉檔嗎？", "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) {
                    cts.Cancel(); btnRun.Enabled = false; setProgressTitle("正在停止，等候目前工作收尾...");
                }
                return;
            }
            cts = new CancellationTokenSource();
            try { activeJob = RunJobAsync(cts.Token); await activeJob; }
            catch (Exception ex) { Console.Error.WriteLine(ex); if (!ClosingAfterJob) MessageBox.Show(this, ex.Message, "轉檔失敗"); }
            finally {
                cts.Dispose(); cts = null; activeJob = null;
                if (!IsDisposed && !ClosingAfterJob) { uiRunOrStop("STOP"); btnRun.Enabled = true; }
            }
        }
        private async Task RunJobAsync(CancellationToken token)
        {
            bool keepTemp = checkBox_keepTemp.Checked;
            string sourceFile = txtSource.Text.Trim(), targetFile = txtOutput.Text.Trim();
            if (!File.Exists(sourceFile)) { MessageBox.Show(this, "來源檔案不存在..."); return; }
            if (string.IsNullOrWhiteSpace(targetFile)) { MessageBox.Show(this, "未指定輸出檔案..."); return; }
            sourceFile = Path.GetFullPath(sourceFile); targetFile = Path.GetFullPath(targetFile);
            if (string.Equals(sourceFile, targetFile, StringComparison.OrdinalIgnoreCase)) { MessageBox.Show(this, "來源與輸出檔案不可相同..."); return; }
            if (!string.Equals(Path.GetExtension(targetFile), ".mp4", StringComparison.OrdinalIgnoreCase)) { MessageBox.Show(this, "輸出必須為 MP4..."); return; }
            if (File.Exists(targetFile) && MessageBox.Show(this, "檔案已存在，要覆蓋嗎？", "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            uiRunOrStop("RUN");
            // 清除所有行
            logDataGridView.Rows.Clear();

            // 清除所有列
            logDataGridView.Columns.Clear();

            // 清除所有選擇
            logDataGridView.ClearSelection();


            string json_columns = @"
                [
                    {""id"":""步驟"",""name"":""步驟"",""width"":""80"",""display"":""true"",""headerAlign"":""center"",""cellAlign"":""center"",""columnKind"":""text""},
                    {""id"":""工作名稱"",""name"":""工作名稱"",""width"":""300"",""display"":""true"",""headerAlign"":""center"",""cellAlign"":""left"",""columnKind"":""text""},
                    {""id"":""開始時間"",""name"":""開始時間"",""width"":""180"",""display"":""true"",""headerAlign"":""center"",""cellAlign"":""center"",""columnKind"":""text""},
                    {""id"":""經過時間"",""name"":""經過時間"",""width"":""120"",""display"":""true"",""headerAlign"":""center"",""cellAlign"":""right"",""columnKind"":""text""},
                    {""id"":""結束時間"",""name"":""結束時間"",""width"":""180"",""display"":""true"",""headerAlign"":""center"",""cellAlign"":""center"",""columnKind"":""text""},
                    {""id"":""狀態"",""name"":""狀態"",""width"":""90"",""display"":""true"",""headerAlign"":""center"",""cellAlign"":""center"",""columnKind"":""text""}                    
                ]
            ";
            //var ra = my.datatable_init(json_columns);
            my.grid_init(logDataGridView, json_columns);

            // 清空 DataTable 的內容
            //ra.Clear();

            // 設置 DataGridView 的一些屬性
            // 要自動展開

            logDataGridView.AllowUserToAddRows = false;
            logDataGridView.AllowUserToDeleteRows = false;
            logDataGridView.ReadOnly = true;
            logDataGridView.RowHeadersVisible = false;
            logDataGridView.ColumnHeadersVisible = true;
            //logDataGridView.RowHeadersDefaultCellStyle.BackColor = System.Drawing.Color.Orange;
            logDataGridView.ColumnHeadersDefaultCellStyle.BackColor = System.Drawing.Color.Orange;
            logDataGridView.ColumnHeadersDefaultCellStyle.ForeColor = System.Drawing.Color.Black;
            logDataGridView.ColumnHeadersDefaultCellStyle.Font = new System.Drawing.Font("微軟正黑體", 13, System.Drawing.FontStyle.Bold);
            logDataGridView.EnableHeadersVisualStyles = false;
            logDataGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            logDataGridView.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            logDataGridView.AllowUserToResizeColumns = true;
            logDataGridView.AllowUserToResizeRows = false;
            foreach (DataGridViewColumn column in logDataGridView.Columns)
            {
                column.SortMode = DataGridViewColumnSortMode.NotSortable;
                column.DefaultCellStyle.BackColor = System.Drawing.Color.White;
                column.DefaultCellStyle.Font = new System.Drawing.Font("微軟正黑體", 12, System.Drawing.FontStyle.Bold);
            }

            logDataGridView.Refresh();

            setProgressTitle("轉檔開始..."); setProgress(0.0);
            string workPath = Path.Combine(TMP_PATH, DateTime.Now.ToString("yyyyMMddHHmmss") + "-" + Guid.NewGuid().ToString("N"));
            string currentStage = null;
            var total = System.Diagnostics.Stopwatch.StartNew();
            try {
                string[] names = { "檢查與建立工作目錄", "將 影片轉 png", "將 影片分離聲音", "將 原影像 png 用 ai 轉成高解析度", "將 ai 轉的高解析度影像 與 聲音檔 合併輸出成 mp4" };
                for (int i = 0; i < names.Length; i++) {
                    token.ThrowIfCancellationRequested(); currentStage = names[i];
                    my.grid_addRow(logDataGridView, new string[] { "步驟" + (i + 1), currentStage, my.date(), "", "", "" });
                    var timer = System.Diagnostics.Stopwatch.StartNew();
                    bool success;
                    switch (i) {
                        case 0: success = App.step1_checkWorkPath(workPath); break;
                        case 1: success = await App.step2_sourceFile_to_png(workPath, sourceFile, token); break;
                        case 2: success = await App.step3_sourceFile_to_wav(workPath, sourceFile, targetFile, token); break;
                        case 3: success = await App.step4_sourcePng_to_aiPng(workPath, token); break;
                        default: success = await App.step5_aiPng_to_mp4(workPath, targetFile, token); break;
                    }
                    my.grid_updateRow(logDataGridView, currentStage, "經過時間", timer.Elapsed.TotalSeconds.ToString("0.0") + " 秒");
                    my.grid_updateRow(logDataGridView, currentStage, "結束時間", my.date());
                    my.grid_updateRow(logDataGridView, currentStage, "狀態", success ? "是" : "失敗");
                    if (!success) { setProgressTitle("轉檔失敗"); return; }
                }
                currentStage = "清理工作目錄";
                my.grid_addRow(logDataGridView, new string[] { "步驟6", currentStage, my.date(), "", "", "" });
                if (!keepTemp && !await App.step6_remove_workPath(workPath, CancellationToken.None)) {
                    my.grid_updateRow(logDataGridView, currentStage, "狀態", "失敗"); return;
                }
                my.grid_updateRow(logDataGridView, currentStage, "狀態", keepTemp ? "保留" : "是");
                my.grid_updateRow(logDataGridView, currentStage, "結束時間", my.date());
                setProgress(100.0); setProgressTitle(keepTemp ? "暫存檔保留於: " + workPath : "工作完成");
                my.grid_addRow(logDataGridView, new string[] { "結算", "總時間", "", total.Elapsed.TotalSeconds.ToString("0.0") + " 秒", my.date(), "完成" });
                if (!ClosingAfterJob) MessageBox.Show(this, keepTemp ? "工作完成\r\n暫存檔保留於：" + workPath : "工作完成", "通知", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (OperationCanceledException) {
                if (currentStage != null) my.grid_updateRow(logDataGridView, currentStage, "狀態", "已取消");
                setProgressTitle("已取消");
            }
            finally {
                // Every awaited worker has released its native handles before temp cleanup or UI reuse.
                if (!keepTemp && Directory.Exists(workPath)) {
                    try { await Task.Run(() => Directory.Delete(workPath, true)); }
                    catch (Exception ex) { Console.Error.WriteLine(ex); if (!ClosingAfterJob) MessageBox.Show(this, "暫存清理失敗：" + workPath + "\r\n" + ex.Message); }
                }
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            string message = @"
" + PROGRAM_NAME + @"

版本：" + PROGRAM_VERSION + @"
作者：羽山 (https://3wa.tw)

FFmpeg shared libraries：LGPLv3
Real-ESRGAN：MIT；詳見 THIRD-PARTY-NOTICES.md
";
            MessageBox.Show(message, "說明", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void labelShowLog_Click(object sender, EventArgs e)
        {
            if (labelShowLog.Text == "㊉")
            {
                labelShowLog.Text = "㊀";
                labelShowLog.ForeColor = System.Drawing.Color.Red;
                this.Size = new System.Drawing.Size(this.Size.Width, 672);
                logDataGridView.Visible = true;
            }
            else
            {
                labelShowLog.Text = "㊉";
                labelShowLog.ForeColor = System.Drawing.Color.DarkGreen;
                this.Size = new System.Drawing.Size(this.Size.Width, 460);
                logDataGridView.Visible = false;
            }
        }

        private void labelShowLog_MouseEnter(object sender, EventArgs e)
        {
            this.Cursor = Cursors.Hand;
        }

        private void labelShowLog_MouseLeave(object sender, EventArgs e)
        {
            this.Cursor = Cursors.Default;
        }
    }
}
