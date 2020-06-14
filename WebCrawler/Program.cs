using log4net;
using log4net.Config;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WebCrawle
{
    class Program
    {
        private const string FilePath = @"./DownFile/";


        private static Stopwatch sw;
        private static char[] cbuffer;
        private static List<string> loaded;
        private static List<string> unLoaded;
        private static object threadLockObject;
        private static object taskLockObject;
        private static Dictionary<Task, string> taskUriDic;

        static void Main(string[] args)
        {
            Log4net.Init();

            if (!System.IO.Directory.Exists(FilePath))
            {
                System.IO.Directory.CreateDirectory(FilePath);
            }
            sw = new Stopwatch();
            cbuffer = new char[512];
            loaded = new List<string>();
            unLoaded = new List<string>();
            threadLockObject = new object();
            taskLockObject = new object();
            taskUriDic = new Dictionary<Task, string>();

            sw.Restart();

            unLoaded.Add("http://news.baidu.com/");
            MultiThreadWork();

            sw.Stop();
            Console.WriteLine(string.Format("消耗时间:{0}ms.", sw.ElapsedMilliseconds));
            Console.Read();

            Log4net.ShutDown();
        }

        private static void MultiThreadWork()
        {
            string uri = null;
        Start:
            while (unLoaded.Count > 0)
            {
                uri = unLoaded[0];
                unLoaded.RemoveAt(0);

                Task task = Task.Run(() =>
                {
                    WebCrawlerThread(uri);
                });

                if (!taskUriDic.Keys.Contains(task))
                {
                    taskUriDic.Add(task, uri);
                }

                try
                {
                    while (unLoaded.Count == 0)
                    {
                        Task[] taskArray;
                        lock (taskLockObject)
                        {
                            Log4net.gLogger.Debug("TaskUriDic.Where");
                            taskArray = taskUriDic.Keys.Where(r => r.Status == TaskStatus.Running).ToArray();
                        }
                        if (taskArray != null && taskArray.Length > 0)
                        {
                            Task.WaitAny(taskArray);
                        }
                    }
                }
                catch (Exception err)
                {
                    Log4net.gLogger.Fatal("<MultiThreadWork>", err);
                    Log4net.gLogger.Fatal("<MultiThreadWork>" + Environment.StackTrace);
                }
            }

            {
                Task[] taskArray;
                lock (threadLockObject)
                {
                    taskArray = taskUriDic.Keys.Where(r => r.Status == TaskStatus.Running).ToArray();
                }
                if (taskArray != null && taskArray.Length > 0)
                {
                    Task.WaitAny(taskArray);
                    goto Start;
                }
            }
        }

        private static void WebCrawlerThread(string uriString)
        {
            if (loaded.Contains(uriString))
            {
                return;
            }

            loaded.Add(uriString);
            Console.WriteLine("Now loading " + uriString);
            Log4net.gLogger.Debug("Now loading " + uriString);

            try
            {
                //HttpWebRequest类继承于WebRequest，并没有自己的构造函数，需通过WebRequest的Creat方法 建立，并进行强制的类型转换 
                HttpWebRequest httpReq = (HttpWebRequest)WebRequest.Create(uriString);

                //通过HttpWebRequest的GetResponse()方法建立HttpWebResponse,强制类型转换
                HttpWebResponse httpResp = (HttpWebResponse)httpReq.GetResponse();

                /*
                 * GetResponseStream()方法获取HTTP响应的数据流,并尝试取得URL中所指定的网页内容
                 * 若成功取得网页的内容，则以System.IO.Stream形式返回，若失败则产生ProtoclViolationException错误。
                 * 在此正确的做法应将以下的代码放到一个try块中处理。这里简单处理 
                 */
                using (var respStream = httpResp.GetResponseStream())
                {
                    if (respStream != null)
                    {
                        //返回的内容是Stream形式的，所以可以利用StreamReader类获取GetResponseStream的内容，
                        //并以StreamReader类的Read方法依次读取网页源程序代码每一行的内容，直至行尾（读取的编码格式：UTF8）
                        var enc = Encoding.UTF8;
                        CheckEncoding(respStream, ref enc);

                        using (var respStreamReader = new StreamReader(respStream, enc))
                        {
                            string strBuff = "";
                            var byteRead = respStreamReader.Read(cbuffer, 0, cbuffer.Length);

                            while (byteRead != 0)
                            {
                                string strResp = new string(cbuffer, 0, byteRead);
                                strBuff = strBuff + strResp;
                                byteRead = respStreamReader.Read(cbuffer, 0, cbuffer.Length);
                            }

                            lock (threadLockObject)
                            {
                                using (var fs = new FileStream(string.Format("{0}WebCrawler_{1}.txt", FilePath, DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss-fff")), FileMode.Create, FileAccess.Write))
                                {
                                    fs.SetLength(0);
                                    byte[] byData = System.Text.Encoding.Default.GetBytes(strBuff);
                                    fs.Write(byData, 0, byData.Length);
                                }

                                string[] uriStrings = GetLinks(strBuff);
                                foreach (string item in uriStrings)
                                {
                                    if (!loaded.Contains(item))
                                    {
                                        unLoaded.Add(item);
                                    }
                                }
                                Console.WriteLine("Download OK!");
                                Log4net.gLogger.Debug("Download OK!");
                            }
                        }
                    }
                }
            }
            catch (Exception err)
            {
                Log4net.gLogger.Error("<WebCrawlerThread>", err);
                Log4net.gLogger.Error("<WebCrawlerThread>" + Environment.StackTrace);
            }
            finally
            {
                lock (taskLockObject)
                {
                    if (taskUriDic.Values.Contains(uriString))
                    {
                        var firstKey = taskUriDic.FirstOrDefault(q => q.Value == uriString).Key;
                        taskUriDic.Remove(firstKey);
                        Log4net.gLogger.Debug("TaskUriDic.Remove");
                    }
                }
            }
        }

        private static string[] GetLinks(string html)
        {
            const string pattern = @"http://([\w-]+\.)+[\w-]+(/[\w- ./?%&=]*)?";
            Regex r = new Regex(pattern, RegexOptions.IgnoreCase); //新建正则模式
            MatchCollection m = r.Matches(html); //获得匹配结果
            string[] links = new string[m.Count];

            for (int i = 0; i < m.Count; i++)
            {
                links[i] = m[i].ToString(); //提取出结果
            }
            return links;
        }

        #region  c# 网络爬虫乱码问题解决方法[http://www.cnphp.info/howto-solve-encoding-problem-in-csharp.html]

        /// <summary>
        /// Check Html Page Encoding
        /// site: http://www.cnphp.info/howto-solve-encoding-problem-in-csharp.html
        /// author: <a href="http://www.cnphp.info">freemouse</a>
        /// </summary>
        /// <param name="s">NetWork Stream</param>
        /// <param name="enc">Ecnoding in</param>
        /// <returns>return read content from stream s</returns>
        private static string CheckEncoding(Stream s, ref Encoding enc)
        {
            string pattern = "<meta.+?charset\\s*=\\s*(?<charset>[-\\w]+)";
            Regex charSetPattern = new Regex(pattern, RegexOptions.IgnoreCase);
            StringBuilder strBuilder = new StringBuilder();
            StringBuilder retBuilder = new StringBuilder();
            string line = "";
            while (ReadLine(s, strBuilder))
            {
                line = strBuilder.ToString();
                if (line.Trim().StartsWith("<body")) break;
                strBuilder.Remove(0, strBuilder.Length);
                retBuilder.AppendLine(line);
                Match m = charSetPattern.Match(line);
                if (m.Success)
                {
                    string strEnc = m.Groups["charset"].Value;
                    try
                    {
                        enc = Encoding.GetEncoding(strEnc);
                        break;
                    }
                    catch (Exception)
                    {
                        //throw new Exception(err.Message);
                        return retBuilder.ToString();
                    }
                }
            }
            if (enc != null)
            {
                return enc.GetString(Encoding.GetEncoding("ISO-8859-1").GetBytes(retBuilder.ToString()));
            }

            return retBuilder.ToString();
        }

        /// <summary>
        /// Read a line from NetwrokStream
        /// site: http://www.cnphp.info/howto-solve-encoding-problem-in-csharp.html
        /// author: <a href="http://www.cnphp.info">freemouse</a>
        /// </summary>
        /// <param name="s">Stream to Read</param>
        /// <param name="strBuilder">Line storage</param>
        /// <returns>if is end of Stream return false</returns>
        private static bool ReadLine(Stream s, StringBuilder strBuilder)
        {
            int iChar = -1;
            do
            {
                if (iChar != -1)
                {
                    if (iChar <= 65 && iChar >= 90)
                    {
                        strBuilder.Append(iChar + 32);
                    }
                    else
                        strBuilder.Append((char)iChar);
                }

            } while ((iChar = s.ReadByte()) != (int)'\n' && iChar != -1);

            if (iChar != -1) return true;

            return false;
        }

        #endregion

    }

    class Log4net
    {
        private static string strLogPath = Environment.CurrentDirectory + @"\Config\log4net.config";

        public static ILog gLogger;//日志记录

        public static void Init()
        {
            if (System.IO.File.Exists(strLogPath))
            {
                XmlConfigurator.ConfigureAndWatch(new FileInfo(strLogPath));
            }
            if (null == Log4net.gLogger)
            {
                Log4net.gLogger = LogManager.GetLogger(typeof(Program));
            }
        }

        public static void ShutDown()
        {
            LogManager.Shutdown();
        }
    }

}
