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
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {

            // 檢查是否有正在進行的轉檔過程
            if (cts != null && !cts.Token.IsCancellationRequested)
            {
                // 提示使用者是否要強制結束
                var result = MessageBox.Show("目前正在進行轉檔，是否確定要強制結束？", "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                if (result == DialogResult.No)
                {
                    // 如果使用者選擇不結束，取消關閉操作
                    e.Cancel = true;
                    return;
                }
                else
                {
                    // 如果使用者選擇結束，取消轉檔過程
                    cts.Cancel();
                }
            }
        }
        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            // 隱藏在任務欄中的圖示
            ShowInTaskbar = false;

            // 隱藏並釋放通知圖示
            notifyIcon1.Visible = false;
            notifyIcon1.Dispose();

            // 執行退出邏輯
            exit();
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
            PWD = my.pwd();
            TMP_PATH = PWD + "\\tmp";
            if (!my.is_dir(TMP_PATH)) { my.mkdir(TMP_PATH); }

            // preset x 2
            comboBox_ImageScale.SelectedIndex = 0;

            // default aac
            comboBox_soundKind.SelectedIndex = 0;

            // 設定邊框樣式
            labelShowLog.AutoSize = true;
            labelShowLog.BorderStyle = BorderStyle.FixedSingle;
            labelShowLog.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;

            // 使用 Padding 調整內邊距
            labelShowLog.Padding = new Padding(3, 3, 1, 3); // 上下左右各 10 像素的內邊距
        }

        private void exit()
        {
            ShowInTaskbar = false;
            notifyIcon1.Visible = false;
            notifyIcon1.Dispose();
            try
            {
                Environment.Exit(1);
            }
            catch { }
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
                        labelShowLog.Visible = false;
                        //logDataGridView.Rows.Clear();
                        //logDataGridView.Visible = false;
                    }));
                    break;
            }
        }
        private async void btnRun_Click(object sender, EventArgs e)
        {
            //當 isDebug true 時，不刪資料
            bool isDebug = false;
            if (btnRun.Text == "開始轉檔")
            {
                string sourceFile = txtSource.Text.Trim();
                string targetFile = txtOutput.Text.Trim();
                if (!my.is_file(sourceFile))
                {
                    MessageBox.Show("來源檔案，檔案不存在...");
                    return;
                }
                if (targetFile == "")
                {
                    MessageBox.Show("輸出檔案，未指定...");
                    return;
                }
                if (sourceFile == targetFile)
                {
                    MessageBox.Show("來源檔案，不可與輸出檔案相同...");
                    return;
                }
                if (my.is_file(targetFile))
                {
                    //檔案已存在
                    DialogResult result = MessageBox.Show("檔案已存在，要覆蓋嗎？", "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (result == DialogResult.No)
                    {
                        return;
                    }
                }
                string sn = my.subname(targetFile);
                if (sn.ToLower() != "mp4")
                {
                    MessageBox.Show("輸出檔案，必須為 mp4...");
                    return;
                }
                if (!my.isFileReadableWritable(targetFile))
                {
                    MessageBox.Show("輸出檔案無法寫入...", "囧rz");
                    return;
                }
                //可以開始轉檔了!!?
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

                this.Invoke((MethodInvoker)(() =>
                {
                    setProgressTitle("轉檔開始...");
                    setProgress(0.00f);
                }));
                string dt = my.date("YmdHis");
                //dt = "20240729005107";
                string workPath = Path.Combine(TMP_PATH, dt);

                try
                {
                    //檢查與建立工作目錄...
                    //把步驟一寫到 logDataGridView
                    long st = Convert.ToInt64(my.strtotime(my.date("Y-m-d H:i:s")));
                    long et = Convert.ToInt64(my.strtotime(my.date("Y-m-d H:i:s")));
                    Int64 duration = 0;
                    bool success = true;
                    {
                        my.grid_addRow(logDataGridView, new string[] { "步驟1", "檢查與建立工作目錄", my.date("Y-m-d H:i:s", st.ToString()), "", "", "" });

                        if (!App.step1_checkWorkPath(workPath))
                        {
                            uiRunOrStop("STOP");
                            try { if (!isDebug) { my.deltree(workPath); } } catch { }
                            cts = null;
                            et = Convert.ToInt64(my.strtotime(my.date("Y-m-d H:i:s")));
                            duration = et - st;
                            my.grid_updateRow(logDataGridView, 0, new string[] { "步驟1", "檢查與建立工作目錄", my.date("Y-m-d H:i:s", st.ToString()), duration.ToString() + " 秒", my.date("Y-m-d H:i:s", et.ToString()), "失敗" });
                            return;
                        }
                        //將 影片轉 png 
                        Task.Delay(1000).Wait();
                        cts = new CancellationTokenSource();

                        et = Convert.ToInt64(my.strtotime(my.date("Y-m-d H:i:s")));
                        duration = et - st;
                        my.grid_updateRow(logDataGridView, 0, new string[] { "步驟1", "檢查與建立工作目錄", my.date("Y-m-d H:i:s", st.ToString()), duration.ToString() + " 秒", my.date("Y-m-d H:i:s", et.ToString()), "是" });
                    }  // step 1 步驟1 檢查與建立工作目錄

                    {
                        st = Convert.ToInt64(my.strtotime(my.date("Y-m-d H:i:s")));
                        my.grid_addRow(logDataGridView, new string[] { "步驟2", "將 影片轉 png", my.date("Y-m-d H:i:s", st.ToString()), "", "", "" });
                        success = await App.step2_sourceFile_to_png(workPath, sourceFile, cts.Token);
                        if (!success)
                        {
                            uiRunOrStop("STOP");
                            try { await Task.Run(() => { if (!isDebug) { my.deltree(workPath); } }); } catch { }
                            cts = null;
                            et = Convert.ToInt64(my.strtotime(my.date("Y-m-d H:i:s")));
                            duration = et - st;
                            //my.grid_updateRow(logDataGridView, 1, new string[] { "步驟2", "將 影片轉 png", my.date("Y-m-d H:i:s", st.ToString()), duration.ToString() + " 秒", my.date("Y-m-d H:i:s", et.ToString()), "失敗" });
                            my.grid_updateRow(logDataGridView, "將 影片轉 png", "經過時間", duration.ToString() + " 秒");
                            my.grid_updateRow(logDataGridView, "將 影片轉 png", "結束時間", my.date());
                            my.grid_updateRow(logDataGridView, "將 影片轉 png", "狀態", "失敗");
                            return;
                        }
                        et = Convert.ToInt64(my.strtotime(my.date("Y-m-d H:i:s")));
                        duration = et - st;
                        my.grid_updateRow(logDataGridView, 1, new string[] { "步驟2", "將 影片轉 png", my.date("Y-m-d H:i:s", st.ToString()), duration.ToString() + " 秒", my.date("Y-m-d H:i:s", et.ToString()), "是" });
                        st = Convert.ToInt64(my.strtotime(my.date("Y-m-d H:i:s")));
                    } // step 2 步驟2 將 影片轉 png                    

                    {

                        st = Convert.ToInt64(my.strtotime(my.date("Y-m-d H:i:s")));
                        my.grid_addRow(logDataGridView, new string[] { "步驟3", "將 影片分離聲音", my.date("Y-m-d H:i:s", st.ToString()), "", "", "" });

                        cts = new CancellationTokenSource();
                        Task.Delay(1000).Wait();
                        success = await App.step3_sourceFile_to_wav(workPath, sourceFile, targetFile, cts.Token);
                        if (!success)
                        {
                            uiRunOrStop("STOP");
                            try { await Task.Run(() => { if (!isDebug) { my.deltree(workPath); } }); } catch { }
                            cts = null;
                            et = Convert.ToInt64(my.strtotime(my.date("Y-m-d H:i:s")));
                            duration = et - st;
                            //my.grid_updateRow(logDataGridView, 2, new string[] { "步驟3", "將 影片分離聲音", my.date("Y-m-d H:i:s", st.ToString()), duration.ToString() + " 秒", my.date("Y-m-d H:i:s", et.ToString()), "失敗" });
                            my.grid_updateRow(logDataGridView, "將 影片分離聲音", "經過時間", duration.ToString() + " 秒");
                            my.grid_updateRow(logDataGridView, "將 影片分離聲音", "結束時間", my.date());
                            my.grid_updateRow(logDataGridView, "將 影片分離聲音", "狀態", "失敗");
                            return;
                        }
                        //將 原影像 png 用 ai 轉成高解析度

                        et = Convert.ToInt64(my.strtotime(my.date("Y-m-d H:i:s")));
                        duration = et - st;
                        my.grid_updateRow(logDataGridView, 2, new string[] { "步驟3", "將 影片分離聲音", my.date("Y-m-d H:i:s", st.ToString()), duration.ToString() + " 秒", my.date("Y-m-d H:i:s", et.ToString()), "是" });
                    } // step 3 步驟3 將 影片分離聲音

                    {
                        st = Convert.ToInt64(my.strtotime(my.date("Y-m-d H:i:s")));
                        my.grid_addRow(logDataGridView, new string[] { "步驟4", "將 原影像 png 用 ai 轉成高解析度", my.date("Y-m-d H:i:s", st.ToString()), "", "", "" });
                        cts = new CancellationTokenSource();
                        Task.Delay(1000).Wait();
                        success = await App.step4_sourcePng_to_aiPng(workPath, cts.Token);
                        if (!success)
                        {
                            uiRunOrStop("STOP");
                            try { await Task.Run(() => { if (!isDebug) { my.deltree(workPath); } }); } catch { }
                            cts = null;
                            et = Convert.ToInt64(my.strtotime(my.date("Y-m-d H:i:s")));
                            duration = et - st;
                            my.grid_updateRow(logDataGridView, 3, new string[] { "步驟4", "將 原影像 png 用 ai 轉成高解析度", my.date("Y-m-d H:i:s", st.ToString()), duration.ToString() + " 秒", my.date("Y-m-d H:i:s", et.ToString()), "失敗" });
                            return;
                        }

                        //將 ai 轉的高解析度影像 與 聲音檔 合併輸出成 mp4 
                        et = Convert.ToInt64(my.strtotime(my.date("Y-m-d H:i:s")));
                        duration = et - st;
                        my.grid_updateRow(logDataGridView, 3, new string[] { "步驟4", "將 原影像 png 用 ai 轉成高解析度", my.date("Y-m-d H:i:s", st.ToString()), duration.ToString() + " 秒", my.date("Y-m-d H:i:s", et.ToString()), "是" });

                    } // step 4 步驟4 將 原影像 png 用 ai 轉成高解析度

                    {
                        st = Convert.ToInt64(my.strtotime(my.date("Y-m-d H:i:s")));
                        my.grid_addRow(logDataGridView, new string[] { "步驟5", "將 ai 轉的高解析度影像 與 聲音檔 合併輸出成 mp4", my.date("Y-m-d H:i:s", st.ToString()), "", "", "" });

                        cts = new CancellationTokenSource();
                        Task.Delay(1000).Wait();
                        success = await App.step5_aiPng_to_mp4(workPath, targetFile, cts.Token);
                        if (!success)
                        {
                            uiRunOrStop("STOP");
                            try { await Task.Run(() => { if (!isDebug) { my.deltree(workPath); } }); } catch { }
                            cts = null;
                            et = Convert.ToInt64(my.strtotime(my.date("Y-m-d H:i:s")));
                            duration = et - st;
                            my.grid_updateRow(logDataGridView, 4, new string[] { "步驟5", "將 ai 轉的高解析度影像 與 聲音檔 合併輸出成 mp4", my.date("Y-m-d H:i:s", st.ToString()), duration.ToString(), my.date("Y-m-d H:i:s", et.ToString()), "失敗" });
                            return;
                        }

                        cts = new CancellationTokenSource();
                        et = Convert.ToInt64(my.strtotime(my.date("Y-m-d H:i:s")));
                        duration = et - st;
                        my.grid_updateRow(logDataGridView, 4, new string[] { "步驟5", "將 ai 轉的高解析度影像 與 聲音檔 合併輸出成 mp4", my.date("Y-m-d H:i:s", st.ToString()), duration.ToString() + " 秒", my.date("Y-m-d H:i:s", et.ToString()), "是" });
                    } // step 5 步驟5 將 ai 轉的高解析度影像 與 聲音檔 合併輸出成 mp4

                    {
                        st = Convert.ToInt64(my.strtotime(my.date("Y-m-d H:i:s")));
                        my.grid_addRow(logDataGridView, new string[] { "步驟6", "清理工作目錄", my.date("Y-m-d H:i:s", st.ToString()), "", "", "" });


                        Task.Delay(1000).Wait();
                        if (!isDebug)
                        {
                            success = await App.step6_remove_workPath(workPath, cts.Token);
                            if (!success)
                            {
                                uiRunOrStop("STOP");
                                cts = null;
                                et = Convert.ToInt64(my.strtotime(my.date("Y-m-d H:i:s")));
                                duration = et - st;
                                my.grid_updateRow(logDataGridView, 5, new string[] { "步驟6", "清理工作目錄", my.date("Y-m-d H:i:s", st.ToString()), duration.ToString() + " 秒", my.date("Y-m-d H:i:s", et.ToString()), "否" });
                                return;
                            }
                        }
                        et = Convert.ToInt64(my.strtotime(my.date("Y-m-d H:i:s")));
                        duration = et - st;
                        my.grid_updateRow(logDataGridView, 5, new string[] { "步驟6", "清理工作目錄", my.date("Y-m-d H:i:s", st.ToString()), duration.ToString() + " 秒", my.date("Y-m-d H:i:s", et.ToString()), "是" });
                    } // step 6 步驟6 清理工作目錄
                    //加上總時間
                    st = Convert.ToInt64(my.strtotime(my.grid_getRowValueFromNindNameAndCellName(logDataGridView, "檢查與建立工作目錄", "開始時間")));
                    et = Convert.ToInt64(my.strtotime(my.date("Y-m-d H:i:s")));
                    duration = et - Convert.ToInt64(my.strtotime(my.date("Y-m-d H:i:s", st.ToString())));
                    my.grid_addRow(logDataGridView, new string[] { "結算", "總時間", my.date("Y-m-d H:i:s", st.ToString()), duration.ToString() + " 秒", my.date("Y-m-d H:i:s", et.ToString()), "完成" });
                    this.TopMost = true;
                    MessageBox.Show(this, "工作完成", "通知", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    this.TopMost = false;
                    uiRunOrStop("STOP");
                }
                catch (OperationCanceledException)
                {
                    // 處理取消操作的後續行為
                    uiRunOrStop("STOP");
                    cts = null;
                }
                finally
                {
                    if (cts != null)
                    {
                        cts.Dispose();
                    }
                    cts = null;
                }

            }
            else
            {
                DialogResult result = MessageBox.Show("停止轉檔嗎？", "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {

                    // kill all ?
                    if (cts != null)
                    {
                        try
                        {
                            cts.Cancel();
                        }
                        catch { }
                    }
                    uiRunOrStop("STOP");
                    return;
                }
            }

        }

        private void button3_Click(object sender, EventArgs e)
        {
            string message = @"
" + PROGRAM_NAME + @"

版本：" + PROGRAM_VERSION + @"
作者：羽山 (https://3wa.tw)
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
