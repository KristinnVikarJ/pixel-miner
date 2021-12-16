using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading;
using System.Diagnostics;

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

        public static void WorkerThread()
        {
            while (true)
            {
                if(!workQueue.IsEmpty && workQueue.TryDequeue(out WorkData data))
                {
                    Console.WriteLine(workQueue.Count);
                    Process p = Process.Start("md5.exe", data.salt + " " + data.target);
                    p.StartInfo.CreateNoWindow = true;
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.RedirectStandardOutput = true;
                    p.Start();
                    string msg = p.StandardOutput.ReadToEnd();
                    finishQueue.Enqueue(new FinishData()
                    {
                        pow = msg,
                        session = data.session
                    });
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
                    var parameters = new Dictionary<string, string> { { "action", "get_work" } };
                    var data2 = await c.PostAsync("http://challs.xmas.htsp.ro:3002/api", new FormUrlEncodedContent(parameters));
                    string content = await data2.Content.ReadAsStringAsync();

                    string[] temp = content.Split(' ');

                    try
                    {
                        string session = ((string[])data2.Headers.GetValues("Set-Cookie"))[0];
                        string target = temp[^1];

                        workQueue.Enqueue(new WorkData()
                        {
                            session = session,
                            salt = temp[3].Substring(0, 64),
                            target = target
                        });
                        Thread.Sleep(150);
                    }
                    catch { }
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

            Thread workerThread = new Thread(WorkerThread);
            workerThread.Start();

            Thread fetcherThread = new Thread(FetcherThread);
            fetcherThread.Start();

            Thread senderThread = new Thread(SenderThread);
            senderThread.Start();

            while (true)
            {
                Thread.Sleep(1000);
            }
        }
    }
}
