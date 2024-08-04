namespace my_cartoon_beautiful
{
    partial class Form1
    {
        /// <summary>
        /// 設計工具所需的變數。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清除任何使用中的資源。
        /// </summary>
        /// <param name="disposing">如果應該處置受控資源則為 true，否則為 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form 設計工具產生的程式碼

        /// <summary>
        /// 此為設計工具支援所需的方法 - 請勿使用程式碼編輯器修改
        /// 這個方法的內容。
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            this.notifyIcon1 = new System.Windows.Forms.NotifyIcon(this.components);
            this.label1 = new System.Windows.Forms.Label();
            this.openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            this.button1 = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.txtSource = new System.Windows.Forms.TextBox();
            this.txtOutput = new System.Windows.Forms.TextBox();
            this.saveFileDialog1 = new System.Windows.Forms.SaveFileDialog();
            this.button2 = new System.Windows.Forms.Button();
            this.btnRun = new System.Windows.Forms.Button();
            this.progressBar1 = new System.Windows.Forms.ProgressBar();
            this.txtProgress = new System.Windows.Forms.Label();
            this.txtProgressLabel = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.comboBox_ImageScale = new System.Windows.Forms.ComboBox();
            this.button3 = new System.Windows.Forms.Button();
            this.labelShowLog = new System.Windows.Forms.Label();
            this.logDataGridView = new System.Windows.Forms.DataGridView();
            this.label4 = new System.Windows.Forms.Label();
            this.comboBox_soundKind = new System.Windows.Forms.ComboBox();
            ((System.ComponentModel.ISupportInitialize)(this.logDataGridView)).BeginInit();
            this.SuspendLayout();
            // 
            // notifyIcon1
            // 
            this.notifyIcon1.BalloonTipText = "影片清晰機";
            this.notifyIcon1.Icon = ((System.Drawing.Icon)(resources.GetObject("notifyIcon1.Icon")));
            this.notifyIcon1.Text = "影片清晰機";
            this.notifyIcon1.Visible = true;
            this.notifyIcon1.MouseClick += new System.Windows.Forms.MouseEventHandler(this.notifyIcon1_MouseClick);
            this.notifyIcon1.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.notifyIcon1_MouseDoubleClick);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("微軟正黑體", 27.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.label1.Location = new System.Drawing.Point(31, 31);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(205, 47);
            this.label1.TabIndex = 0;
            this.label1.Text = "來源檔案：";
            // 
            // openFileDialog1
            // 
            this.openFileDialog1.FileName = "openFileDialog1";
            // 
            // button1
            // 
            this.button1.Font = new System.Drawing.Font("微軟正黑體", 27.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.button1.Location = new System.Drawing.Point(224, 31);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(186, 47);
            this.button1.TabIndex = 1;
            this.button1.Text = "選擇檔案";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("微軟正黑體", 27.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.label2.Location = new System.Drawing.Point(31, 130);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(205, 47);
            this.label2.TabIndex = 3;
            this.label2.Text = "輸出檔案：";
            // 
            // txtSource
            // 
            this.txtSource.Font = new System.Drawing.Font("微軟正黑體", 18F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.txtSource.Location = new System.Drawing.Point(39, 82);
            this.txtSource.Name = "txtSource";
            this.txtSource.Size = new System.Drawing.Size(939, 39);
            this.txtSource.TabIndex = 4;
            // 
            // txtOutput
            // 
            this.txtOutput.Font = new System.Drawing.Font("微軟正黑體", 18F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.txtOutput.Location = new System.Drawing.Point(39, 180);
            this.txtOutput.Name = "txtOutput";
            this.txtOutput.Size = new System.Drawing.Size(939, 39);
            this.txtOutput.TabIndex = 5;
            // 
            // button2
            // 
            this.button2.Font = new System.Drawing.Font("微軟正黑體", 27.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.button2.Location = new System.Drawing.Point(224, 130);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(186, 47);
            this.button2.TabIndex = 6;
            this.button2.Text = "選擇檔案";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // btnRun
            // 
            this.btnRun.Font = new System.Drawing.Font("微軟正黑體", 27.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.btnRun.Location = new System.Drawing.Point(39, 298);
            this.btnRun.Name = "btnRun";
            this.btnRun.Size = new System.Drawing.Size(186, 47);
            this.btnRun.TabIndex = 7;
            this.btnRun.Text = "開始轉檔";
            this.btnRun.UseVisualStyleBackColor = true;
            this.btnRun.Click += new System.EventHandler(this.btnRun_Click);
            // 
            // progressBar1
            // 
            this.progressBar1.Location = new System.Drawing.Point(15, 366);
            this.progressBar1.Name = "progressBar1";
            this.progressBar1.Size = new System.Drawing.Size(962, 47);
            this.progressBar1.TabIndex = 8;
            this.progressBar1.Visible = false;
            // 
            // txtProgress
            // 
            this.txtProgress.AutoSize = true;
            this.txtProgress.Font = new System.Drawing.Font("微軟正黑體", 27.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.txtProgress.Location = new System.Drawing.Point(231, 298);
            this.txtProgress.Name = "txtProgress";
            this.txtProgress.Size = new System.Drawing.Size(94, 47);
            this.txtProgress.TabIndex = 9;
            this.txtProgress.Text = "進度";
            this.txtProgress.Visible = false;
            // 
            // txtProgressLabel
            // 
            this.txtProgressLabel.AutoSize = true;
            this.txtProgressLabel.Font = new System.Drawing.Font("微軟正黑體", 27.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.txtProgressLabel.Location = new System.Drawing.Point(405, 298);
            this.txtProgressLabel.Name = "txtProgressLabel";
            this.txtProgressLabel.Size = new System.Drawing.Size(168, 47);
            this.txtProgressLabel.TabIndex = 10;
            this.txtProgressLabel.Text = "進度說明";
            this.txtProgressLabel.Visible = false;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("微軟正黑體", 27.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.label3.Location = new System.Drawing.Point(31, 238);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(205, 47);
            this.label3.TabIndex = 11;
            this.label3.Text = "影像放大：";
            // 
            // comboBox_ImageScale
            // 
            this.comboBox_ImageScale.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBox_ImageScale.Font = new System.Drawing.Font("微軟正黑體", 24F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.comboBox_ImageScale.FormattingEnabled = true;
            this.comboBox_ImageScale.Items.AddRange(new object[] {
            "x 2",
            "x 3",
            "x 4"});
            this.comboBox_ImageScale.Location = new System.Drawing.Point(224, 236);
            this.comboBox_ImageScale.Name = "comboBox_ImageScale";
            this.comboBox_ImageScale.Size = new System.Drawing.Size(90, 48);
            this.comboBox_ImageScale.TabIndex = 12;
            // 
            // button3
            // 
            this.button3.Font = new System.Drawing.Font("微軟正黑體", 21.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.button3.Location = new System.Drawing.Point(891, 12);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(98, 47);
            this.button3.TabIndex = 13;
            this.button3.Text = "說明";
            this.button3.UseVisualStyleBackColor = true;
            this.button3.Click += new System.EventHandler(this.button3_Click);
            // 
            // labelShowLog
            // 
            this.labelShowLog.AutoSize = true;
            this.labelShowLog.Font = new System.Drawing.Font("微軟正黑體", 26.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.labelShowLog.ForeColor = System.Drawing.Color.DarkGreen;
            this.labelShowLog.Location = new System.Drawing.Point(934, 240);
            this.labelShowLog.Name = "labelShowLog";
            this.labelShowLog.Size = new System.Drawing.Size(55, 45);
            this.labelShowLog.TabIndex = 14;
            this.labelShowLog.Text = "㊉";
            this.labelShowLog.Visible = false;
            this.labelShowLog.Click += new System.EventHandler(this.labelShowLog_Click);
            this.labelShowLog.MouseEnter += new System.EventHandler(this.labelShowLog_MouseEnter);
            this.labelShowLog.MouseLeave += new System.EventHandler(this.labelShowLog_MouseLeave);
            // 
            // logDataGridView
            // 
            this.logDataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.logDataGridView.Location = new System.Drawing.Point(15, 419);
            this.logDataGridView.Name = "logDataGridView";
            this.logDataGridView.RowTemplate.Height = 24;
            this.logDataGridView.Size = new System.Drawing.Size(962, 205);
            this.logDataGridView.TabIndex = 15;
            this.logDataGridView.Visible = false;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("微軟正黑體", 27.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.label4.Location = new System.Drawing.Point(340, 237);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(205, 47);
            this.label4.TabIndex = 16;
            this.label4.Text = "聲音格式：";
            // 
            // comboBox_soundKind
            // 
            this.comboBox_soundKind.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBox_soundKind.Font = new System.Drawing.Font("微軟正黑體", 24F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.comboBox_soundKind.FormattingEnabled = true;
            this.comboBox_soundKind.Items.AddRange(new object[] {
            "MP3",
            "原音"});
            this.comboBox_soundKind.Location = new System.Drawing.Point(530, 236);
            this.comboBox_soundKind.Name = "comboBox_soundKind";
            this.comboBox_soundKind.Size = new System.Drawing.Size(121, 48);
            this.comboBox_soundKind.TabIndex = 17;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1001, 421);
            this.Controls.Add(this.comboBox_soundKind);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.logDataGridView);
            this.Controls.Add(this.labelShowLog);
            this.Controls.Add(this.button3);
            this.Controls.Add(this.comboBox_ImageScale);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.txtProgressLabel);
            this.Controls.Add(this.txtProgress);
            this.Controls.Add(this.progressBar1);
            this.Controls.Add(this.btnRun);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.txtOutput);
            this.Controls.Add(this.txtSource);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "Form1";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "影片清晰機";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.Form1_FormClosed);
            this.Load += new System.EventHandler(this.Form1_Load);
            this.Resize += new System.EventHandler(this.Form1_Resize);
            ((System.ComponentModel.ISupportInitialize)(this.logDataGridView)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.NotifyIcon notifyIcon1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.OpenFileDialog openFileDialog1;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox txtSource;
        private System.Windows.Forms.TextBox txtOutput;
        private System.Windows.Forms.SaveFileDialog saveFileDialog1;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.Button btnRun;
        private System.Windows.Forms.ProgressBar progressBar1;
        private System.Windows.Forms.Label txtProgress;
        private System.Windows.Forms.Label txtProgressLabel;
        private System.Windows.Forms.Label label3;
        public System.Windows.Forms.ComboBox comboBox_ImageScale;
        private System.Windows.Forms.Button button3;
        private System.Windows.Forms.Label labelShowLog;
        public System.Windows.Forms.DataGridView logDataGridView;
        private System.Windows.Forms.Label label4;
        public System.Windows.Forms.ComboBox comboBox_soundKind;
    }
}

