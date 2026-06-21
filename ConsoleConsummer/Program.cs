using System;
using Communicator;

namespace ConsoleConsumer
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Consumer pornit. Citeste date din WebAPI la fiecare 2.5 secunde...");

            while (true)
            {
                var data = HttpHelper.GetDataFromWebAPI();
                Console.Clear();
                Console.WriteLine($"Total evenimente primite: {data.Count}");

                int start = Math.Max(0, data.Count - 10);
                for (int i = start; i < data.Count; i++)
                {
                    Console.WriteLine($"  [{data[i].StateChangedDate:HH:mm:ss}] Stare: {data[i].State}");
                }

                System.Threading.Thread.Sleep(2500);
            }
        }
    }
}