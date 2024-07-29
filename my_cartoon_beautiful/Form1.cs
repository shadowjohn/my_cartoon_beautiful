using System;
using System.IO;
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
        static string PROGRAM_VERSION = "0.01";
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

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            ShowInTaskbar = false;
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
            PWD = my.pwd();
            TMP_PATH = PWD + "\\tmp";
            if (!my.is_dir(TMP_PATH)) { my.mkdir(TMP_PATH); }

            // preset x 2
            comboBox_ImageScale.SelectedIndex = 0;
        }

        private void exit()
        {
            ShowInTaskbar = false;
            notifyIcon1.Visible = false;
            notifyIcon1.Dispose();
            Environment.Exit(1);
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
                    }));
                    break;
            }
        }
        private async void btnRun_Click(object sender, EventArgs e)
        {
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
                    DialogResult result = MessageBox.Show("檔案已存在，要覆蓋嗎？", "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (result == DialogResult.No)
                    {
                        return;
                    }
                }
                string sn = my.subname(targetFile);
                {
                    if (sn.ToLower() != "mp4")
                    {
                        MessageBox.Show("輸出檔案，必須為 mp4...");
                        return;
                    }
                }
                if (!my.isFileReadableWritable(targetFile))
                {
                    MessageBox.Show("輸出檔案無法寫入...", "囧rz");
                    return;
                }
                //可以開始轉檔了!!?
                uiRunOrStop("RUN");
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
                    if (!App.step1_checkWorkPath(workPath))
                    {
                        uiRunOrStop("STOP");
                        try { my.deltree(workPath); } catch { }
                        cts = null;
                        return;
                    }
                    //將 影片轉 png 
                    Task.Delay(1000).Wait();
                    cts = new CancellationTokenSource();
                    bool success = await App.step2_sourceFile_to_png(workPath, sourceFile, cts.Token);
                    if (!success)
                    {
                        uiRunOrStop("STOP");
                        try { await Task.Run(() => my.deltree(workPath)); } catch { }
                        cts = null;
                        return;
                    }
                    //將 影片轉 wav
                    cts = new CancellationTokenSource();
                    Task.Delay(1000).Wait();
                    success = await App.step3_sourceFile_to_wav(workPath, sourceFile, targetFile, cts.Token);
                    if (!success)
                    {
                        uiRunOrStop("STOP");
                        try { await Task.Run(() => my.deltree(workPath)); } catch { }
                        cts = null;
                        return;
                    }
                    //將 原影像 png 用 ai 轉成高解析度
                    cts = new CancellationTokenSource();
                    Task.Delay(1000).Wait();
                    success = await App.step4_sourcePng_to_aiPng(workPath, cts.Token);
                    if (!success)
                    {
                        uiRunOrStop("STOP");
                        try { await Task.Run(() => my.deltree(workPath)); } catch { }
                        cts = null;
                        return;
                    }

                    //將 ai 轉的高解析度影像 png 與 wav 合併輸出成 mp4 
                    cts = new CancellationTokenSource();
                    Task.Delay(1000).Wait();
                    success = await App.step5_aiPng_to_mp4(workPath, targetFile, cts.Token);
                    if (!success)
                    {
                        uiRunOrStop("STOP");
                        try { await Task.Run(() => my.deltree(workPath)); } catch { }
                        cts = null;
                        return;
                    }

                    cts = new CancellationTokenSource();
                    Task.Delay(1000).Wait();
                    success = await App.step6_remove_workPath(workPath, cts.Token);
                    if (!success)
                    {
                        uiRunOrStop("STOP");
                        cts = null;
                        return;
                    }
                    MessageBox.Show("工作完成");
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
    }
}
