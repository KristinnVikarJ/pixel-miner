using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading;
using System.Diagnostics;
using Newtonsoft.Json;
using System.IO;
using System.Text;

namespace ConsoleApp2
{
    class Program
    {
        struct WorkData
        {
            public string session;
            public string salt;
            public string target;
        }

        struct FinishData
        {
            public string session;
            public string pow;
        }

        static ConcurrentQueue<WorkData> workQueue;
        static ConcurrentQueue<FinishData> finishQueue;

        public static void SenderThread()
        {
            HttpClient c = new HttpClient();
            while (true)
            {
                if (!finishQueue.IsEmpty && finishQueue.TryDequeue(out FinishData finishData))
                {
                    var parameters = new Dictionary<string, string> { { "session", finishData.session }, { "pow", finishData.pow } };
                    var data = new FormUrlEncodedContent(parameters);
                    c.PostAsync("https://piebot.xyz/ctf/pixels/data", data);
                }
                else
                {
                    Thread.Sleep(5);
                }
            }
        }

        public static async void FetcherThread()
        {
            var httpClientHandler = new HttpClientHandler
            {
                UseCookies = false,
                AllowAutoRedirect = false,
            };
            HttpClient c = new HttpClient(httpClientHandler);
            while (true)
            {
                if (workQueue.Count < 20)
                {
                    try
                    {
                        var data2 = await c.GetAsync("https://piebot.xyz/ctf/pixels/getwork");
                        string content = await data2.Content.ReadAsStringAsync();
                        List<WorkData> retData = JsonConvert.DeserializeObject<List<WorkData>>(content);

                        if (File.Exists("input.txt"))
                        {
                            File.Delete("input.txt");
                        }

                        if (File.Exists("outfile.txt"))
                        {
                            File.Delete("outfile.txt");
                        }

                        StreamWriter sw = new StreamWriter("input.txt");
                        Dictionary<string, string> hashToSession = new Dictionary<string, string>();
                        foreach (WorkData workData in retData)
                        {
                            sw.WriteLine(workData.salt + " " + workData.target);
                            hashToSession[workData.target] = workData.session;
                        }
                        sw.Flush();
                        sw.Close();

                        Console.WriteLine(workQueue.Count);
                        Process p = Process.Start("md5.exe", "input.txt");
                        p.StartInfo.CreateNoWindow = true;
                        p.StartInfo.UseShellExecute = false;
                        p.StartInfo.RedirectStandardOutput = true;
                        p.Start();
                        p.WaitForExit();

                        List<FinishData> sendData = new List<FinishData>();

                        while (!File.Exists("outfile.txt"))
                            Thread.Sleep(5);

                        StreamReader sr = new StreamReader("outfile.txt");
                        while (!sr.EndOfStream)
                        {
                            string lineIn = sr.ReadLine();
                            string[] parts = lineIn.Split(" ");
                            sendData.Add(new FinishData()
                            {
                                session = hashToSession[parts[0]],
                                pow = parts[1]
                            });
                        }
                        sr.Close();

                        string Data = JsonConvert.SerializeObject(sendData);
                        var cont = new StringContent(Data, Encoding.UTF8, "application/json");
                        await c.PostAsync("https://piebot.xyz/ctf/pixels/bulkdata", cont);
                        Thread.Sleep(20);
                    }
                    catch
                    {
                        Thread.Sleep(100);
                    }
                }
                else
                {
                    Thread.Sleep(50);
                }
            }
        }

        static void Main(string[] args)
        {
            workQueue = new ConcurrentQueue<WorkData>();
            finishQueue = new ConcurrentQueue<FinishData>();

            Thread fetcherThread = new Thread(FetcherThread);
            fetcherThread.Start();

            while (true)
            {
                Thread.Sleep(1000);
            }
        }
    }
}
