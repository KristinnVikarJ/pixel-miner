using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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
            public byte[] target;
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

        static async Task<SuperData> GetWork()
        {
            HttpClient c = new HttpClient();
            var parameters = new Dictionary<string, string> { { "action", "get_work" } };
            var data = new FormUrlEncodedContent(parameters);
            var data2 = await c.PostAsync("http://challs.xmas.htsp.ro:3002/api", data);
            string content = await data2.Content.ReadAsStringAsync();

            string[] temp = content.Split(' ');

            string session = ((string[])data2.Headers.GetValues("Set-Cookie"))[0];
            string target = temp[temp.Length-1];
            byte[] bytes = new byte[target.Length / 2];

            for (int i = 0; i < target.Length; i += 2)
                bytes[i / 2] = Convert.ToByte(target.Substring(i, 2), 16);

            return new SuperData()
            {
                session = session,
                salt = temp[3].Substring(0, 64),
                target = bytes
            };
        }

        public static string GetRandomHexColor()
        {
            var result = Guid.NewGuid().ToString().Substring(0, 6);
            return result;
        }

        static ConcurrentQueue<DrawData> PostQueue = new ConcurrentQueue<DrawData>();

        public static async void PosterThread()
        {
            var httpClientHandler = new HttpClientHandler
            {
                UseCookies = false,
                AllowAutoRedirect = false,
            };
            HttpClient c = new HttpClient(httpClientHandler);
            DrawData drawData = new DrawData();
            bool Finished = true;
            while (true)
            {
                if (!Finished || (!PostQueue.IsEmpty && PostQueue.TryDequeue(out drawData)))
                {
                    Stopwatch stopwatch = new Stopwatch();
                    stopwatch.Start();
                    Console.WriteLine($"drawing at x:{drawData.pos.Y.ToString()} y:{drawData.pos.X.ToString()} color:{drawData.color}");
                    var parameters = new Dictionary<string, string> { { "action", "paint" },
                    { "team", "42" },
                    { "x", drawData.pos.Y.ToString() },
                    { "y", drawData.pos.X.ToString() },
                    { "color", drawData.color },
                    { "work", drawData.pow } 
                    };
                    var data = new FormUrlEncodedContent(parameters);
                    data.Headers.Add("Cookie", drawData.session);
                    var data2 = await c.PostAsync("http://challs.xmas.htsp.ro:3002/api", data);
                    stopwatch.Stop();
                    string p = await data2.Content.ReadAsStringAsync();
                    if(p == "You haven't POSTed with action=get_work first!")
                    {
                        Console.WriteLine("session got invalidated, refreshing");
                        Finished = true;
                        Thread.Sleep(50);
                        continue;
                    }
                    if (data2.StatusCode != HttpStatusCode.OK)
                    {
                        Finished = false;
                        Thread.Sleep(50);
                        continue;
                    }
                    Thread.Sleep(220);
                }
            }
        }

        public static async void GetterThread()
        {
            HttpClient c = new HttpClient();
            while (true)
            {
                Thread.Sleep(100);
                if (PostQueue.Count < 30)
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
            }
        }

        static void Main(string[] args)
        {
            queue = new ConcurrentQueue<SuperData>();
            //PostQueue = new ConcurrentQueue<DrawData>();
            Thread t2 = new Thread(GetterThread);
            t2.Start();
            Thread t3 = new Thread(PosterThread);
            t3.Start();
            while (true)
            {
                Thread.Sleep(200);
            }
        }
    }
}
