using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleApp3
{
    class Program
    {
        struct SuperData
        {
            public string salt;
            public string target;
            public string session;
        }

        struct Pos
        {
            public int X;
            public int Y;
        }

        struct DrawData
        {
            public Pos pos;
            public string color;
            public string session;
            public string pow;
        }

        static ConcurrentQueue<SuperData> queue;
        public static string URL;

        public static async void WorkerThread(string proxy)
        {
            WebProxy webProxy = new WebProxy(proxy);
            var httpClientHandler = new HttpClientHandler
            {
                UseCookies = false,
                AllowAutoRedirect = false,
                Proxy = webProxy
            };
            HttpClient c = new HttpClient(httpClientHandler);
            c.Timeout = TimeSpan.FromSeconds(5);
            HttpClient c1 = new HttpClient();
            int FailCount = 0;
            while (true)
            {
                Thread.Sleep(1);
                try
                {
                    var parameters = new Dictionary<string, string> { { "action", "get_work" } };
                    var data2 = await c.PostAsync("http://challs.xmas.htsp.ro:3002/api", new FormUrlEncodedContent(parameters));
                    string content = await data2.Content.ReadAsStringAsync();

                    string[] temp = content.Split(' ');

                    string session = ((string[])data2.Headers.GetValues("Set-Cookie"))[0];
                    string target = temp[^1];
                    queue.Enqueue(new SuperData()
                    {
                        session = session,
                        salt = temp[3].Substring(0, 64),
                        target = target
                    });
                    Thread.Sleep(150);
                    FailCount = 0;
                }
                catch
                {
                    FailCount += 1;
                    if (FailCount >= 5)
                    {
                        Console.WriteLine("Killed Thread (Sender): " + proxy);
                        Thread.Sleep(2000);
                    }
                }
            }
        }

        public static string GetRandomHexColor()
        {
            var result = Guid.NewGuid().ToString().Substring(0, 6);
            return result;
        }

        static ConcurrentQueue<DrawData> PostQueue = new ConcurrentQueue<DrawData>();

        public static async void PosterThread(string proxy)
        {
            WebProxy webProxy = new WebProxy(proxy);
            var httpClientHandler = new HttpClientHandler
            {
                UseCookies = false,
                AllowAutoRedirect = false,
                Proxy = webProxy
            };
            HttpClient c = new HttpClient(httpClientHandler);
            c.Timeout = TimeSpan.FromSeconds(5);
            DrawData drawData = new DrawData();
            bool Finished = true;
            int FailCount = 0;
            while (true)
            {
                if (!Finished || (!PostQueue.IsEmpty && PostQueue.TryDequeue(out drawData)))
                {
                    try
                    {
                        var parameters = new Dictionary<string, string> { { "action", "paint" },
                            { "team", "42" },
                            { "x", drawData.pos.X.ToString() },
                            { "y", drawData.pos.Y.ToString() },
                            { "color", drawData.color },
                            { "work", drawData.pow }
                            };
                        var data = new FormUrlEncodedContent(parameters);
                        data.Headers.Add("Cookie", drawData.session);
                        Console.WriteLine($"drawing at x:{drawData.pos.X.ToString()} y:{drawData.pos.Y.ToString()} color:{drawData.color}");
                        var data2 = await c.PostAsync("http://challs.xmas.htsp.ro:3002/api", data);
                        string p = await data2.Content.ReadAsStringAsync();
                        Console.WriteLine("done");
                        if (p == "You haven't POSTed with action=get_work first!")
                        {
                            Console.WriteLine("session got invalidated, refreshing");
                            Finished = true;
                            Thread.Sleep(50);
                            continue;
                        }
                        if (data2.StatusCode != HttpStatusCode.OK)
                        {
                            FailCount += 1;
                            Finished = false;
                            Thread.Sleep(50);
                            continue;
                        }
                        FailCount = 0;
                    }
                    catch (Exception e){
                        Finished = true;
                        FailCount += 1;
                        if(FailCount >= 5)
                        {
                            Console.WriteLine("Killed Thread (Painter): " + proxy);
                            Thread.Sleep(2000);
                        }
                    }
                }
                Thread.Sleep(100);
            }
        }

        public static async void GetterThread()
        {
            HttpClient c = new HttpClient();
            c.Timeout = TimeSpan.FromSeconds(1);
            while (true)
            {
                Thread.Sleep(50);
                if (PostQueue.Count < 30)
                {
                    Console.WriteLine("Getting Queue");
                    try
                    {
                        var data2 = await c.GetAsync("https://piebot.xyz/ctf/pixels/get");
                        string jsonData2 = await data2.Content.ReadAsStringAsync();
                        if (jsonData2 == "{}")
                            continue;
                        foreach (DrawData i in JsonConvert.DeserializeObject<DrawData[]>(jsonData2))
                        {
                            PostQueue.Enqueue(i);
                        }
                    }
                    catch
                    {
                        //do nothing lol
                    }
                }
            }
        }

        static async void SenderThread()
        {
            HttpClient c = new HttpClient();
            while (true)
            {
                Thread.Sleep(100);
                if(queue.Count > 100)
                {
                    Console.WriteLine("Sending data");
                    List<SuperData> array = new List<SuperData>();
                    for (int i = 0; i < 100; i++)
                    {
                        if(queue.TryDequeue(out SuperData data))
                        {
                            array.Add(data);
                        }
                    }
                    string Data = JsonConvert.SerializeObject(array);
                    var content = new StringContent(Data, Encoding.UTF8, "application/json");
                    await c.PostAsync("https://piebot.xyz/ctf/pixels/set", content);
                }
            }
        }

        static void Main(string[] args)
        {
            queue = new ConcurrentQueue<SuperData>();
            Thread t2 = new Thread(GetterThread);
            t2.Start();
            Thread t = new Thread(SenderThread);
            t.Start();

            StreamReader sr = new StreamReader(args[0]);
            int current = 0;
            while (!sr.EndOfStream) {
                string proxy = sr.ReadLine();
                if (current % 2 == 0)
                {
                    Thread t3 = new Thread(() => PosterThread(proxy));
                    t3.Start();
                }
                else
                {
                    Thread t3 = new Thread(() => PosterThread(proxy));
                    t3.Start();
                }
                current += 1;
            }

            while (true)
            {
                Thread.Sleep(200);
            }
        }
    }
}
