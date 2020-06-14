﻿using System;
using System.IO;
using System.Net;
using System.Text;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace WebCrawle
{
    class Program
    {
        private const string FilePath = @"./WebCrawler";

        private static Stopwatch sw;


        static void Main(string[] args)
        {
            sw = new Stopwatch();
            sw.Restart();
            WebCrawler();
            sw.Stop();
            Console.WriteLine(string.Format("消耗时间:{0}ms.", sw.ElapsedMilliseconds));
            Console.Read();
        }


        private static void WebCrawler()
        {
            var httpURL = new Uri("https://www.python.org/");

            //HttpWebRequest类继承于WebRequest，并没有自己的构造函数，需通过WebRequest的Creat方法 建立，并进行强制的类型转换 
            HttpWebRequest httpReq = (HttpWebRequest)WebRequest.Create(httpURL);

            //通过HttpWebRequest的GetResponse()方法建立HttpWebResponse,强制类型转换
            HttpWebResponse httpResp = (HttpWebResponse)httpReq.GetResponse();

            //GetResponseStream()方法获取HTTP响应的数据流,并尝试取得URL中所指定的网页内容
            //若成功取得网页的内容，则以System.IO.Stream形式返回，若失败则产生ProtoclViolationException错误。在此正确的做法应将以下的代码放到一个try块中处理。这里简单处理 

            Stream respStream = null;
            try
            {
                respStream = httpResp.GetResponseStream();
            }
            catch (System.Net.ProtocolViolationException)
            {
                Console.WriteLine("没有响应流");
            }
            catch (System.ObjectDisposedException)
            {
                Console.WriteLine("当前实例已被释放");
            }

            if (respStream != null)
            {
                //返回的内容是Stream形式的，所以可以利用StreamReader类获取GetResponseStream的内容，
                //并以StreamReader类的Read方法依次读取网页源程序代码每一行的内容，直至行尾（读取的编码格式：UTF8） 

                var enc = Encoding.UTF8;
                CheckEncoding(respStream, ref enc);

                using (var respStreamReader = new StreamReader(respStream, enc))
                {
                    string strBuff = "";
                    char[] cbuffer = new char[256];
                    int byteRead = 0;
                    byteRead = respStreamReader.Read(cbuffer, 0, cbuffer.Length);

                    while (byteRead != 0)
                    {
                        string strResp = new string(cbuffer, 0, byteRead);
                        strBuff = strBuff + strResp;
                        byteRead = respStreamReader.Read(cbuffer, 0, cbuffer.Length);
                    }
                    respStream.Close();
                    Console.WriteLine(strBuff);

                    using (var fs = new FileStream(string.Format("{0}_{1}.txt", FilePath, DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss-fff")), FileMode.Create, FileAccess.Write))
                    {
                        fs.SetLength(0);
                        byte[] byData = System.Text.Encoding.Default.GetBytes(strBuff);
                        fs.Write(byData, 0, byData.Length);
                    }
                }
            }
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
}
