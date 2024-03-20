using System.Diagnostics;
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
            DateTime localTimeBeforeRequest = DateTime.Now;

            // NTPのタイムスタンプは1900年1月1日からの秒数です
            DateTime baseTime = new(1900, 1, 1);

            // NTPパケットの送信バイト配列を作成します
            byte[] ntpData = new byte[48];

            // NTPリクエストを示すバイト
            ntpData[0] = 0x1B;

            // NTPサーバーにUDPソケットを作成し、リクエストを送信します
            using UdpClient udpClient = new(hostname, 123);
            _ = udpClient.Send(ntpData, ntpData.Length);
            IPEndPoint ipendpoint = new(IPAddress.Any, 0);
            byte[] receivedData = udpClient.Receive(ref ipendpoint);

            DateTime localTimeAfterResponse = DateTime.Now;

            // 32ビットの秒数としてNTP応答から取得
            ulong intPart = ((ulong)receivedData[40] << 24) | ((ulong)receivedData[41] << 16) | ((ulong)receivedData[42] << 8) | receivedData[43];
            ulong fracPart = ((ulong)receivedData[44] << 24) | ((ulong)receivedData[45] << 16) | ((ulong)receivedData[46] << 8) | receivedData[47];

            // 受信時刻と送信時刻の間の時間差を計算し、遅延として考慮する
            TimeSpan roundTripTime = localTimeAfterResponse - localTimeBeforeRequest;
            TimeSpan oneWayDelay = roundTripTime / 2;

            // 取得した秒数からDateTimeオブジェクトを作成
            ulong milliseconds = (intPart * 1000) + (fracPart * 1000 / 0x100000000L);
            DateTime networkDateTime = baseTime.AddMilliseconds((long)milliseconds);

            // 補正を行い、遅延を考慮してNTPサーバーから取得した時刻に補正を加える
            networkDateTime += oneWayDelay;

            return new TimeInfo(localTimeBeforeRequest, networkDateTime.AddHours(9));
        }

        /// <summary> Defines the entry point of the application. </summary>
        private static void Main()
        {
            string hostname = "time.windows.com";

            Console.WriteLine("press any key to exit . . .");

            _ = Task.Run(() =>
            {
                while (true)
                {
                    TimeInfo timeInfo = GetNetworkTime(hostname);
                    double diff = Math.Abs((timeInfo.Local - timeInfo.NTP).TotalSeconds);
                    if (diff > 1)
                    {
                        Console.WriteLine($"{timeInfo.Local:yyyy/MM/dd HH:mm:ss.fff} Bad Time. [{diff}]");
                        Run("sc", "start w32time task_started");
                        Run("w32tm", "/resync");
                        TimeInfo fixedTime = GetNetworkTime(hostname);
                        Console.WriteLine($"{timeInfo.Local:yyyy/MM/dd HH:mm:ss.fff} Fixed Timee. [{Math.Abs((fixedTime.Local - fixedTime.NTP).TotalSeconds)}]");
                    }

                    Thread.Sleep(100);
                }
            });

            _ = Console.ReadKey(true);
            Environment.Exit(0);
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
