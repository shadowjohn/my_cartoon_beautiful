using System;
using System.Windows.Forms;

namespace my_cartoon_beautiful
{
    internal static class Program
    {
        /// <summary>
        /// 應用程式的主要進入點。
        /// </summary>
       
        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            // 處理非 UI 線程的未處理例外
            Exception ex = (Exception)e.ExceptionObject;
            MessageBox.Show("發生未處理的例外: \r\n" + ex.Message + "\r\n" + ex.StackTrace, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            //exit();
        }
        [STAThread]
        static void Main()
        {
            //Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            //AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }
}
