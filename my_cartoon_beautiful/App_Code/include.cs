using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace utility
{
    public class myinclude
    {
        private Random rnd = new Random(DateTime.Now.Millisecond);

        public myinclude()
        {

        }
        public string pwd()
        {
            return System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
            //return dirname(System.Web.HttpContext.Current.Server.MapPath("~/"));
        }
        public bool is_dir(string path)
        {
            return Directory.Exists(path);
        }
        public bool is_file(string filepath)
        {
            return File.Exists(filepath);
        }
        public string[] glob(string path)
        {
            //string[] test = my.glob("c:\\tmp");
            //my.echo(my.pre_print_r(test));
            return Directory.GetFiles(path);
        }
        public string b2s(byte[] input)
        {
            return System.Text.Encoding.UTF8.GetString(input);
        }
        public string[] glob(string path, string patten)
        {
            //string[] test = my.glob("c:\\tmp");
            //my.echo(my.pre_print_r(test));
            return Directory.GetFiles(path, patten);
        }
        public void mkdir(string path)
        {
            Directory.CreateDirectory(path);
        }
        public void unlink(string filepath)
        {
            if (is_file(filepath))
            {
                File.Delete(filepath);
            }
        }
        public void copy(string sourceFile, string destFile)
        {
            System.IO.File.Copy(sourceFile, destFile, true);
        }
        public string dirname(string path)
        {
            return Directory.GetParent(path).FullName;
        }
        public string microtime()
        {
            System.DateTime dt = DateTime.Now;
            System.DateTime UnixEpoch = new System.DateTime(1970, 1, 1, 0, 0, 0, 0);
            TimeSpan span = dt - UnixEpoch;
            return string.Format("{0:0.0000}", Convert.ToDouble((span.Ticks / (TimeSpan.TicksPerMillisecond / 1000))) / 1000000.0);
        }
        public string time()
        {
            return strtotime(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        }
        public string date()
        {
            return date("Y-m-d H:i:s", strtotime(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.ffffff")));
        }
        //UnixTimeToDateTime
        public DateTime UnixTimeToDateTime(string text)
        {
            System.DateTime dateTime = new System.DateTime(1970, 1, 1, 0, 0, 0, 0);
            // Add the number of seconds in UNIX timestamp to be converted.            
            dateTime = dateTime.AddSeconds(Convert.ToDouble(text));
            return dateTime;
        }
        public string implode(string keyword, ConcurrentDictionary<int, string> arrays)
        {
            return string.Join(keyword, arrays.Values);
        }

        public string implode(string keyword, ConcurrentDictionary<string, string> arrays)
        {
            return string.Join(keyword, arrays.Values);
        }
        public string basename(string path)
        {
            return Path.GetFileName(path);
        }
        public string mainname(string path)
        {
            return Path.GetFileNameWithoutExtension(path);
        }
        public string subname(string path)
        {
            //return Path.GetExtension(path);
            return Path.GetExtension(path).TrimStart('.');
        }
        public long filesize(string path)
        {
            FileInfo f = new FileInfo(path);
            return f.Length;
        }
        public long filemtime(string filename)
        {
            if (!is_file(filename))
            {
                return -1;
            }
            DateTime dt = File.GetLastWriteTime(filename);
            return Convert.ToInt64(strtotime(dt.ToString("yyyy-MM-dd HH:mm:ss")));
        }
        public string size_hum_read(long bytes)
        {
            string[] Suffix = { "B", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
            int i = 0;
            double dblSByte = Convert.ToDouble(bytes);
            if (bytes > 1024)
                for (i = 0; (bytes / 1024) > 0; i++, bytes /= 1024)
                    dblSByte = bytes / 1024.0;
            return String.Format("{0:0.##} {1}", dblSByte, Suffix[i]);
        }
        public List<string> natsort(List<string> data)
        {
            // 自然排序法
            Func<string, object> convert = str =>
            {
                try { return int.Parse(str); }
                catch { return str; }
            };
            data.Sort((x, y) =>
            {
                var xParts = Regex.Split(x.Replace(" ", ""), "([0-9]+)").Select(convert);
                var yParts = Regex.Split(y.Replace(" ", ""), "([0-9]+)").Select(convert);
                int result = new EnumerableComparer<object>().Compare(xParts, yParts);
                return result != 0 ? result : x.Length.CompareTo(y.Length);
            });
            return data;
        }
        public string[] natsort(string[] data)
        {
            //自然排序法
            Func<string, object> convert = str =>
            {
                try { return int.Parse(str); }
                catch { return str; }
            };
            var sorted = data.OrderBy(
                str => Regex.Split(str.Replace(" ", ""), "([0-9]+)").Select(convert),
                new EnumerableComparer<object>()).OrderBy(
                   x => x.Length
                );
            return sorted.ToArray();
        }
        public string size_hum_read_v2(string _size)
        {
            return size_hum_read_v2(Convert.ToInt64(_size));
        }
        public string size_hum_read_v2(long _size)
        {
            if (_size != 0)
            {
                List<string> unit = new List<string>();
                unit.Add("B");
                unit.Add("KB");
                unit.Add("MB");
                unit.Add("GB");
                unit.Add("TB");
                unit.Add("PB");
                int i = Convert.ToInt32(Math.Floor(Math.Log(_size, 1024)));
                return string.Format("{0:0.00}", Math.Round(Convert.ToDouble(_size) / Convert.ToDouble(Math.Pow(1024, i)), 2)) + " " + unit[i];
            }
            else
            {
                return "0 B";
            }
        }
        public string md5_file(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("File not found", filePath);
            }

            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    var hash = md5.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }
        public string date(string format)
        {
            return date(format, strtotime(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.ffffff")));
        }
        public string date(string format, string unixtimestamp)
        {
            DateTime tmp = UnixTimeToDateTime(unixtimestamp);
            tmp = tmp.AddHours(+8);
            switch (format)
            {
                case "YmdHis":
                    return tmp.ToString("yyyyMMddHHmmss");
                case "Y-m-d H:i:s":
                    return tmp.ToString("yyyy-MM-dd HH:mm:ss");
                case "Y/m/d":
                    return tmp.ToString("yyyy/MM/dd");
                case "Y/m/d H:i:s":
                    return tmp.ToString("yyyy/MM/dd HH:mm:ss");
                case "Y/m/d H:i:s.fff":
                    return tmp.ToString("yyyy/MM/dd HH:mm:ss.fff");
                case "Y-m-d_H_i_s":
                    return tmp.ToString("yyyy-MM-dd_HH_mm_ss");
                case "Y-m-d":
                    return tmp.ToString("yyyy-MM-dd");
                case "H:i:s":
                    return tmp.ToString("HH:mm:ss");
                case "H:i":
                    return tmp.ToString("HH:mm");
                case "Y-m-d H:i":
                    return tmp.ToString("yyyy-MM-dd HH:mm");
                case "Y_m_d_H_i_s":
                    return tmp.ToString("yyyy_MM_dd_HH_mm_ss");
                case "Y_m_d_H_i_s_fff":
                    return tmp.ToString("yyyy_MM_dd_HH_mm_ss_fff");
                case "w":
                    //回傳week, sun =0 , sat = 6, mon=1.....
                    return Convert.ToInt16(tmp.DayOfWeek).ToString();
                case "Y":
                    return tmp.ToString("yyyy");
                case "m":
                    return tmp.ToString("MM");
                case "d":
                    return tmp.ToString("dd");
                case "H":
                    return tmp.ToString("HH");
                case "i":
                    return tmp.ToString("mm");
                case "s":
                    return tmp.ToString("ss");
                case "Y-m-d H:i:s.fff":
                    return tmp.ToString("yyyy-MM-dd HH:mm:ss.fff");
                case "Y-m-d H:i:s.ffffff":
                    return tmp.ToString("yyyy-MM-dd HH:mm:ss.ffffff");
                case "H:i:s.fff":
                    return tmp.ToString("HH:mm:ss.fff");
                case "H:i:s.ffffff":
                    return tmp.ToString("HH:mm:ss.ffffff");
                case "N":
                    //回傳星期1~星期日 (1~7)
                    Dictionary<string, string> w = new Dictionary<string, string>();
                    w["Monday"] = "1";
                    w["Tuesday"] = "2";
                    w["Wednesday"] = "3";
                    w["Thursday"] = "4";
                    w["Friday"] = "5";
                    w["Saturday"] = "6";
                    w["Sunday"] = "7";
                    return w[tmp.DayOfWeek.ToString()];
            }
            return "";
        }
        //strtotime 轉換成 Unix time
        public string strtotime(string value)
        {
            //create Timespan by subtracting the value provided from
            //the Unix Epoch
            TimeSpan span = (Convert.ToDateTime(value) - new DateTime(1970, 1, 1, 0, 0, 0, 0).ToLocalTime());

            //return the total seconds (which is a UNIX timestamp)
            if (is_string_like(value, "."))
            {
                //有小數點               
                double sec = span.Ticks / (TimeSpan.TicksPerMillisecond / 1000.0) / 1000000.0;
                return sec.ToString();
            }
            else
            {
                return span.TotalSeconds.ToString();
            }
        }
        public string[] explode(string keyword, string data)
        {
            return data.Split(new string[] { keyword }, StringSplitOptions.None);
        }
        public string[] explode(string keyword, object data)
        {
            return data.ToString().Split(new string[] { keyword }, StringSplitOptions.None);
        }
        public string[] explode(string[] keyword, string data)
        {
            return data.Split(keyword, StringSplitOptions.None);
        }
        public bool is_string_like(string data, string find_string)
        {
            return (data.IndexOf(find_string) == -1) ? false : true;
        }
        public bool is_string_like_new(string data, string find_string)
        {
            /*
              is_string_like($data,$fine_string)

              $mystring = "Hi, this is good!";
              $searchthis = "%thi% goo%";

              $resp = string_like($mystring,$searchthis);


              if ($resp){
                 echo "milike = VERDADERO";
              } else{
                 echo "milike = FALSO";
              }

              Will print:
              milike = VERDADERO

              and so on...

              this is the function:
            */
            bool tieneini = false;
            if (find_string == "") return true;
            var vi = explode("%", find_string);
            int offset = 0;
            for (int n = 0, max_n = vi.Count(); n < max_n; n++)
            {
                if (vi[n] == "")
                {
                    if (vi[0] == "")
                    {
                        tieneini = true;
                    }
                }
                else
                {
                    //newoff =  strpos(data,vi[$n],offset);
                    int newoff = data.IndexOf(vi[n], offset);
                    if (newoff != -1)
                    {
                        if (!tieneini)
                        {
                            if (offset != newoff)
                            {
                                return false;
                            }
                        }
                        if (n == max_n - 1)
                        {
                            if (vi[n] != data.Substring(data.Length - vi[n].Length, vi[n].Length))
                            {
                                return false;
                            }

                        }
                        else
                        {
                            offset = newoff + vi[n].Length;
                        }
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            return true;
        }
        public bool is_istring_like(string data, string find_string)
        {
            return (data.ToUpper().IndexOf(find_string.ToUpper()) == -1) ? false : true;
        }

        public string strtotime(DateTime value)
        {
            //create Timespan by subtracting the value provided from
            //the Unix Epoch
            TimeSpan span = (value - new DateTime(1970, 1, 1, 0, 0, 0, 0).ToLocalTime());

            //return the total seconds (which is a UNIX timestamp)
            return span.TotalSeconds.ToString();
        }
        public bool isFileReadableWritable(string filePath)
        {
            try
            {
                // 嘗試讀寫檔案
                using (FileStream fs = File.Open(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite))
                {
                    // 檔案可讀寫
                    return true;
                }
            }
            catch (IOException)
            {
                // 檔案不可讀寫
                return false;
            }
        }
        public bool deltree(string target_dir)
        {
            //From : https://dotblogs.com.tw/grepu9/2013/03/20/98267
            try
            {
                bool result = false;
                string[] files = Directory.GetFiles(target_dir);
                string[] dirs = Directory.GetDirectories(target_dir);
                foreach (string file in files)
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                    File.Delete(file);
                }
                foreach (string dir in dirs)
                {
                    deltree(dir);
                }
                Directory.Delete(target_dir, false);
                return result;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        public double ConvertTimeToSeconds(string time)
        {
            // 使用 TimeSpan.ParseExact 方法來解析時間字串
            TimeSpan timeSpan = TimeSpan.ParseExact(time, @"hh\:mm\:ss\.ff", CultureInfo.InvariantCulture);

            // 將 TimeSpan 轉換成總秒數並返回
            return timeSpan.TotalSeconds;
        }
        public string get_between(string data, string s_begin, string s_end)
        {
            //http://stackoverflow.com/questions/378415/how-do-i-extract-a-string-of-text-that-lies-between-two-parenthesis-using-net
            //string a = "abcdefg";
            //MessageBox.Show(my.get_between(a, "cde", "g"));
            //return f;
            string s = data;
            int start = s.IndexOf(s_begin);
            string new_s = data.Substring(start + s_begin.Length);
            int end = new_s.IndexOf(s_end);
            return s.Substring(start + s_begin.Length, end);
        }
        public bool checkNvenc(string ffmpegBin)
        {
            // 自動選擇 h264 或 h264_nvenc 
            try
            {
                Process process = new Process();
                process.StartInfo.FileName = ffmpegBin;
                process.StartInfo.Arguments = "-hide_banner -encoders";
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.Start();

                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                return output.Contains("h264_nvenc");
            }
            catch (Exception)
            {
                return false;
            }
        } // 自動選擇 h264 或 h264_nvenc 
        public long getMovieTotalFrames(string workPath, string ffmpegBin, string movieFile) // 取得影片有多少幀
        {
            if (string.IsNullOrEmpty(movieFile) || !System.IO.File.Exists(movieFile))
            {
                throw new ArgumentException("File does not exist.", nameof(movieFile));
            }

            string ffmpegPath = ffmpegBin; // 確保 ffmpeg 在系統 PATH 中或指定其完整路徑
            string arguments = $" -i \"{movieFile}\" -vf \"fps=30\" -report";

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                WorkingDirectory = workPath,
                FileName = ffmpegPath,
                Arguments = arguments,
                RedirectStandardOutput = false,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = Process.Start(startInfo))
            {
                string output = process.StandardError.ReadToEnd();
                process.WaitForExit();
                var fp = glob(workPath, "ffmpeg-20*.log");
                if (fp.Count() >= 1)
                {
                    string data = b2s(file_get_contents(fp[0]));
                    string dt = get_between(data, " Duration: ", ", ").Trim();
                    Console.WriteLine(data);
                    Console.WriteLine(dt);
                    double dt_sec = ConvertTimeToSeconds(dt);
                    long totalFrames = Convert.ToInt64(dt_sec * 30.0);
                    unlink(fp[0]);
                    return totalFrames;
                }
                return -1;
                /*
                string frameLine = Regex.Match(output, @"frame=\s*(\d+)").Groups[1].Value;
                if (long.TryParse(frameLine, out long totalFrames))
                {
                    return totalFrames;
                }
                else
                {
                    Console.WriteLine("Failed to parse total frames.");
                    return -1; // 解析錯誤
                }*/
            }
        }

        public static long AdjustFramesToNearestMultiple(long frames, int multiple)
        {
            if (multiple <= 0)
            {
                throw new ArgumentException("Multiple must be greater than 0.", nameof(multiple));
            }

            long remainder = frames % multiple;
            if (remainder == 0)
            {
                return frames; // Already a multiple of the desired value
            }

            // Calculate the number of frames needed to reach the next multiple
            long adjustment = multiple - remainder;
            return frames + adjustment;
        }
        public double arduino_map(double x, double inMin, double inMax, double outMin, double outMax)
        {
            return (x - inMin) * (outMax - outMin) / (inMax - inMin) + outMin;
        }
        public byte[] file_get_contents(string f)
        {
            byte[] data;
            using (var fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                data = ReadStream(fs, 8192);
                fs.Close();
            };
            return data;
        }
        public bool isFileLocked(string filename)
        {
            try
            {
                string fpath = filename;
                FileStream fs = File.OpenWrite(fpath);
                fs.Close();
                return false;
            }

            catch { return true; }
        }
        private byte[] ReadStream(Stream stream, int initialLength)
        {
            if (initialLength < 1)
            {
                initialLength = 32768;
            }
            byte[] buffer = new byte[initialLength];
            int read = 0;
            int chunk;
            while ((chunk = stream.Read(buffer, read, buffer.Length - read)) > 0)
            {
                read += chunk;
                if (read == buffer.Length)
                {
                    int nextByte = stream.ReadByte();
                    if (nextByte == -1)
                    {
                        return buffer;
                    }
                    byte[] newBuffer = new byte[buffer.Length * 2];
                    Array.Copy(buffer, newBuffer, buffer.Length);
                    newBuffer[read] = (byte)nextByte;
                    buffer = newBuffer;
                    read++;
                }
            }
            byte[] bytes = new byte[read];
            Array.Copy(buffer, bytes, read);
            return bytes;
        }
        public void grid_init(DataGridView g, string json_columns)
        {
            json_columns = json_columns.Trim();

            // 去掉 JSON 字串的頭尾方括號和大括號
            string trimmedJson = json_columns.Trim('[', ']');
            trimmedJson = trimmedJson.Replace("\r", "").Replace(" ", "").Trim();
            Console.WriteLine(trimmedJson);
            // 分割每一組
            string[] lines = trimmedJson.Trim().Split(new string[] { "\n" }, StringSplitOptions.None);

            foreach (string line in lines)
            {

                // 解析欄位屬性
                string[] attributePairs = line.Trim('{', '}', ',').Trim('"').Split(new string[] { "\",\"" }, StringSplitOptions.None);
                Dictionary<string, string> attributeDict = new Dictionary<string, string>();
                foreach (string pair in attributePairs)
                {
                    string[] attributeKeyValue = pair.Split(new string[] { "\":\"" }, StringSplitOptions.None);
                    attributeDict[attributeKeyValue[0].Trim()] = attributeKeyValue[1].Trim();
                    Console.WriteLine(attributeKeyValue[0].Trim() + ":" + attributeKeyValue[1].Trim());
                }

                // 使用欄位名稱和屬性來建立 DataGridViewColumn
                //p.Name;
                //Console.WriteLine(item.ToString());
                //Console.WriteLine(p.Name);
                //Console.WriteLine(p.Value);                
                string id = attributeDict["id"].ToString();
                string key = attributeDict["id"].ToString();
                switch (attributeDict["columnKind"].ToString())
                {
                    case "text":
                        Console.WriteLine(attributeDict["name"].ToString());
                        g.Columns.Add(new DataGridViewTextBoxColumn
                        {
                            DataPropertyName = id,
                            Name = id,
                            HeaderText = attributeDict["name"].ToString(),
                            Width = Convert.ToInt32(attributeDict["width"]),
                            Visible = Convert.ToBoolean(attributeDict["display"])
                        });
                        g.Columns[key].DefaultCellStyle.Font = new Font("@Fixedsys", 14); //標題字型大小
                        switch (attributeDict["cellAlign"].ToString())
                        {
                            case "left":
                                g.Columns[key].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
                                break;
                            case "center":
                                g.Columns[key].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                                break;
                            case "right":
                                g.Columns[key].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                                break;
                        }
                        g.Columns[key].SortMode = DataGridViewColumnSortMode.NotSortable;
                        g.Columns[key].HeaderCell.Style.Font = new Font("微軟正黑體", 12); //標題字型大小
                        switch (attributeDict["headerAlign"].ToString())
                        {
                            case "left":
                                g.Columns[key].HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleLeft;
                                break;
                            case "center":
                                g.Columns[key].HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                                break;
                            case "right":
                                g.Columns[key].HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
                                break;
                        }
                        break;
                    case "picture":
                        //From : https://www.codeproject.com/Questions/729186/how-to-insert-PictureBox-in-dataGridView-using-Csh
                        var d = new DataGridViewImageColumn();
                        d.Name = id;
                        d.DataPropertyName = id;
                        d.Width = Convert.ToInt32(attributeDict["width"]);
                        d.Visible = Convert.ToBoolean(attributeDict["display"]);
                        d.HeaderText = attributeDict["name"].ToString();
                        switch (attributeDict["headerAlign"].ToString())
                        {
                            case "left":
                                d.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleLeft;
                                break;
                            case "center":
                                d.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                                break;
                            case "right":
                                d.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
                                break;
                        }
                        d.HeaderCell.Style.Font = new Font("微軟正黑體", 12); //標題字型大小
                        d.SortMode = DataGridViewColumnSortMode.NotSortable;
                        g.Columns.Add(d);

                        break;
                }
                //設定標題
            }
        }

        public void autoResizeColumns(DataGridView dgv)
        {
            foreach (DataGridViewColumn column in dgv.Columns)
            {
                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            }

            for (int i = 0; i < dgv.Columns.Count; i++)
            {
                int colWidth = dgv.Columns[i].Width;
                dgv.Columns[i].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                dgv.Columns[i].Width = colWidth;
            }
        }
        public string readFileWithRetry(string filePath, int maxRetries = 50, int delayMilliseconds = 100)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    if (is_file(filePath))
                    {
                        return b2s(file_get_contents(filePath));
                    }
                }
                catch
                {
                    //Console.WriteLine($"IOException: {ex.Message}. Retrying in {delayMilliseconds}ms...");
                    //Thread.Sleep(delayMilliseconds);
                    Task.Delay(delayMilliseconds).Wait();
                }
            }

            Console.WriteLine("Failed to read the file after several retries.");
            return "";
        }

        public void grid_addRow(DataGridView logDataGridView, string[] strings)
        {
            if (logDataGridView == null)
            {
                throw new ArgumentNullException(nameof(logDataGridView));
            }

            if (strings == null)
            {
                throw new ArgumentNullException(nameof(strings));
            }

            if (logDataGridView.ColumnCount != strings.Length)
            {
                throw new ArgumentException("The number of elements in the strings array must match the number of columns in the DataGridView.");
            }

            if (logDataGridView.InvokeRequired)
            {
                logDataGridView.Invoke(new Action(() => logDataGridView.Rows.Add(strings)));
            }
            else
            {
                logDataGridView.Rows.Add(strings);
            }
        }
        public void grid_updateRow(DataGridView logDataGridView, string kindName, string fieldName, string value)
        {
            // Find the row with the specified kindName
            // Find the column with the specified fieldName
            // Update the cell value with the specified value
            if (logDataGridView == null)
            {
                throw new ArgumentNullException(nameof(logDataGridView));
            }
            int index = grid_getRowIndexFromKindName(logDataGridView, kindName);
            if (index == -1)
            {
                // 找不到指定的 工作名稱
                return;
            }
            if (logDataGridView.InvokeRequired)
            {
                logDataGridView.Invoke(new Action(() =>
                {
                    logDataGridView.Rows[index].Cells[fieldName].Value = value;
                }));
            }
        }
        public void grid_updateRow(DataGridView logDataGridView, int rowIndex, string[] strings)
        {
            if (logDataGridView == null)
            {
                throw new ArgumentNullException(nameof(logDataGridView));
            }

            if (strings == null)
            {
                throw new ArgumentNullException(nameof(strings));
            }

            if (rowIndex < 0 || rowIndex >= logDataGridView.Rows.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(rowIndex), "Row index is out of range.");
            }

            if (logDataGridView.ColumnCount != strings.Length)
            {
                throw new ArgumentException("The number of elements in the strings array must match the number of columns in the DataGridView.");
            }

            if (logDataGridView.InvokeRequired)
            {
                logDataGridView.Invoke(new Action(() =>
                {
                    for (int i = 0; i < strings.Length; i++)
                    {
                        logDataGridView.Rows[rowIndex].Cells[i].Value = strings[i];
                    }
                }));
            }
            else
            {
                for (int i = 0; i < strings.Length; i++)
                {
                    logDataGridView.Rows[rowIndex].Cells[i].Value = strings[i];
                }
            }
        }
        public int grid_getRowIndexFromKindName(DataGridView logDataGridView, string kindName)
        {
            if (logDataGridView.InvokeRequired)
            {
                return (int)logDataGridView.Invoke(new Func<int>(() => grid_getRowIndexFromKindName(logDataGridView, kindName)));
            }
            else
            {
                // 遍歷 DataGridView 的所有行
                foreach (DataGridViewRow row in logDataGridView.Rows)
                {
                    // 檢查行是否為新行（用於輸入新數據的行）
                    if (row.IsNewRow)
                    {
                        continue;
                    }

                    // 檢查行中的 "kindName" 列是否存在且值是否匹配
                    if (row.Cells["工作名稱"].Value != null && row.Cells["工作名稱"].Value.ToString() == kindName)
                    {
                        return row.Index;
                    }
                }

                // 如果沒有找到匹配的行，返回 -1
                return -1;
            }
        }
        public string grid_getRowValueFromNindNameAndCellName(DataGridView logDataGridView, string kindName, string cellName)
        {
            if (logDataGridView.InvokeRequired)
            {
                return (string)logDataGridView.Invoke(new Func<string>(() => grid_getRowValueFromNindNameAndCellName(logDataGridView, kindName, cellName)));
            }
            else
            {
                int rowIndex = grid_getRowIndexFromKindName(logDataGridView, kindName);
                if (rowIndex >= 0 && rowIndex < logDataGridView.Rows.Count)
                {
                    return logDataGridView.Rows[rowIndex].Cells[cellName].Value.ToString();
                }
                else
                {
                    throw new ArgumentException("指定的 kindName 找不到對應的行。");
                }
            }
        }
        public string[] grid_getRow(DataGridView logDataGridView, int rowIndex)
        {
            if (logDataGridView == null)
            {
                throw new ArgumentNullException(nameof(logDataGridView));
            }

            if (rowIndex < 0 || rowIndex >= logDataGridView.Rows.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(rowIndex), "Row index is out of range.");
            }

            string[] rowData = new string[logDataGridView.ColumnCount];

            if (logDataGridView.InvokeRequired)
            {
                logDataGridView.Invoke(new Action(() =>
                {
                    for (int i = 0; i < logDataGridView.ColumnCount; i++)
                    {
                        rowData[i] = logDataGridView.Rows[rowIndex].Cells[i].Value?.ToString();
                    }
                }));
            }
            else
            {
                for (int i = 0; i < logDataGridView.ColumnCount; i++)
                {
                    rowData[i] = logDataGridView.Rows[rowIndex].Cells[i].Value?.ToString();
                }
            }

            return rowData;
        }
    }
    /// <summary>
    /// Compares two sequences.
    /// </summary>
    /// <typeparam name="T">Type of item in the sequences.</typeparam>
    /// <remarks>
    /// Compares elements from the two input sequences in turn. If we
    /// run out of list before finding unequal elements, then the shorter
    /// list is deemed to be the lesser list.
    /// </remarks>
    public class EnumerableComparer<T> : IComparer<IEnumerable<T>>
    {
        /// <summary>
        /// Create a sequence comparer using the default comparer for T.
        /// </summary>
        public EnumerableComparer()
        {
            comp = Comparer<T>.Default;
        }

        /// <summary>
        /// Create a sequence comparer, using the specified item comparer
        /// for T.
        /// </summary>
        /// <param name="comparer">Comparer for comparing each pair of
        /// items from the sequences.</param>
        public EnumerableComparer(IComparer<T> comparer)
        {
            comp = comparer;
        }

        /// <summary>
        /// Object used for comparing each element.
        /// </summary>
        private IComparer<T> comp;


        /// <summary>
        /// Compare two sequences of T.
        /// </summary>
        /// <param name="x">First sequence.</param>
        /// <param name="y">Second sequence.</param>
        public int Compare(IEnumerable<T> x, IEnumerable<T> y)
        {
            using (IEnumerator<T> leftIt = x.GetEnumerator())
            using (IEnumerator<T> rightIt = y.GetEnumerator())
            {
                while (true)
                {
                    bool left = leftIt.MoveNext();
                    bool right = rightIt.MoveNext();

                    if (!(left || right)) return 0;

                    if (!left) return -1;
                    if (!right) return 1;

                    int itemResult = comp.Compare(leftIt.Current, rightIt.Current);
                    if (itemResult != 0) return itemResult;
                }
            }
        }
    }
}