﻿using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace TimeSyncApp
{
    /// <summary> Program class. </summary>
    internal static class Program
    {
        /// <summary> Gets the network time. </summary>
        /// <param name="hostname"> The hostname. </param>
        /// <returns> </returns>
        private static TimeInfo GetNetworkTime(string hostname)
        {
            DateTime localTimeBeforeRequest = DateTime.UtcNow;

            // NOTE:NTPのタイムスタンプは1900年1月1日からの秒数です
            DateTime baseTime = new(1900, 1, 1);

            // NOTE:NTPパケットの送信バイト配列を作成します
            byte[] ntpData = new byte[48];

            // NOTE:NTPリクエストを示すバイト
            ntpData[0] = 0x1B;

            // NOTE:NTPサーバーにUDPソケットを作成し、リクエストを送信します
            using UdpClient udpClient = new(hostname, 123);
            _ = udpClient.Send(ntpData, ntpData.Length);
            IPEndPoint ipendpoint = new(IPAddress.Any, 0);
            byte[] receivedData = udpClient.Receive(ref ipendpoint);

            DateTime localTimeAfterResponse = DateTime.UtcNow;

            // NOTE:32ビットの秒数としてNTP応答から取得
            ulong intPart = ((ulong)receivedData[40] << 24) | ((ulong)receivedData[41] << 16) | ((ulong)receivedData[42] << 8) | receivedData[43];
            ulong fracPart = ((ulong)receivedData[44] << 24) | ((ulong)receivedData[45] << 16) | ((ulong)receivedData[46] << 8) | receivedData[47];

            // NOTE:受信時刻と送信時刻の間の時間差を計算し、遅延として考慮する
            TimeSpan roundTripTime = localTimeAfterResponse - localTimeBeforeRequest;
            TimeSpan oneWayDelay = roundTripTime / 2;

            // NOTE:取得した秒数からDateTimeオブジェクトを作成
            ulong milliseconds = (intPart * 1000) + (fracPart * 1000 / 0x100000000L);
            DateTime networkDateTime = baseTime.AddMilliseconds((long)milliseconds);

            // NOTE:補正を行い、遅延を考慮してNTPサーバーから取得した時刻に補正を加える
            networkDateTime += oneWayDelay;
            return new TimeInfo(localTimeBeforeRequest, networkDateTime);
        }

        /// <summary> Defines the entry point of the application. </summary>
        private static void Main()
        {
            string hostname = "time.windows.com";

            while (true)
            {
                TimeInfo timeInfo = GetNetworkTime(hostname);
                Console.WriteLine($"{DateTime.Now:yyyy/MM/dd HH:mm:ss.fff} Running.");

                double diff = Math.Abs((timeInfo.Local - timeInfo.NTP).TotalSeconds);
                if (diff > 1)
                {
                    Console.WriteLine($"{DateTime.Now:yyyy/MM/dd HH:mm:ss.fff} Bad Time. [{diff}]");
                    Run("sc", "start w32time task_started");
                    Run("w32tm", "/resync");
                    TimeInfo fixedTime = GetNetworkTime(hostname);
                    Console.WriteLine($"{DateTime.Now:yyyy/MM/dd HH:mm:ss.fff} Fixed Time. [{Math.Abs((fixedTime.Local - fixedTime.NTP).TotalSeconds)}]");
                }

                DateTime tmp = DateTime.UtcNow.AddHours(1);
                DateTime ajust = new(tmp.Year, tmp.Month, tmp.Day, tmp.Hour, 0, 0);
                TimeSpan span = ajust - DateTime.UtcNow;
                Console.WriteLine($"{DateTime.Now:yyyy/MM/dd HH:mm:ss.fff} Sleep. [{span}]");
                Thread.Sleep(span);
            }
        }

        /// <summary> Runs the specified filename. </summary>
        /// <param name="filename"> The filename. </param>
        /// <param name="args"> The arguments. </param>
        private static void Run(string filename, string args)
        {
            Process p = Process.Start(filename, args);
            p.WaitForExit();
        }
    }
}
