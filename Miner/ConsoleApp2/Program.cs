using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading;
using System.Diagnostics;
using Newtonsoft.Json;
using System.IO;
using System.Text;
using System.Security.Cryptography;

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
            StreamReader sr = null;
            MD5 md5 = MD5.Create();
            while (true)
            {
                if (workQueue.Count < 20)
                {
                    string content = "";
                    try
                    {
                        var data2 = await c.GetAsync("https://piebot.xyz/ctf/pixels/getwork");
                        content = await data2.Content.ReadAsStringAsync();
                        List<WorkData> retData = JsonConvert.DeserializeObject<List<WorkData>>(content);

                        if (File.Exists("input.txt"))
                        {
                            File.Delete("input.txt");
                        }

                        if (File.Exists("outfile.txt"))
                        {
                            bool Finished = false;
                            while (!Finished)
                            {
                                try
                                {
                                    File.Delete("outfile.txt");
                                    Finished = true;
                                }
                                catch
                                {
                                    Thread.Sleep(100);
                                }
                            }
                        }

                        StreamWriter sw = new StreamWriter("input.txt");
                        Dictionary<string, string> hashToSession = new Dictionary<string, string>();
                        Dictionary<string, string> hashToSalt = new Dictionary<string, string>();
                        foreach (WorkData workData in retData)
                        {
                            sw.WriteLine(workData.salt + " " + workData.target);
                            hashToSession[workData.target] = workData.session;
                            hashToSalt[workData.target] = workData.salt;
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

                        sr = new StreamReader("outfile.txt");
                        while (!sr.EndOfStream)
                        {
                            string lineIn = sr.ReadLine();
                            string[] parts = lineIn.Split(" ");
                            if (hashToSession.ContainsKey(parts[0]))
                            {
                                string comp = BitConverter.ToString(md5.ComputeHash(Encoding.ASCII.GetBytes(hashToSalt[parts[0]] + parts[1]))).Replace("-", "").ToLower();
                                if (comp.StartsWith(parts[0]))
                                {
                                    sendData.Add(new FinishData()
                                    {
                                        session = hashToSession[parts[0]],
                                        pow = parts[1]
                                    });
                                }
                                else
                                {
                                    Console.WriteLine($"Mismatch: {hashToSalt[parts[0]]} + {parts[1]} = {comp.Substring(0, 6)}, not {parts[0]}");
                                }
                            }
                        }
                        sr.Close();

                        string Data = JsonConvert.SerializeObject(sendData);
                        var cont = new StringContent(Data, Encoding.UTF8, "application/json");
                        await c.PostAsync("https://piebot.xyz/ctf/pixels/bulkdata", cont);
                        Thread.Sleep(20);
                    }
                    catch
                    {
                        Console.WriteLine(content);
                        sr?.Close();
                        Thread.Sleep(1000);
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
