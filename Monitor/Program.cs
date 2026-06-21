using System;
using Communicator;
using DataModel;

namespace Monitor
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Aplicatia de monitorizare a pompelor a pornit...");
            Console.WriteLine("Asteapta date de la Simulator pe portul 3000...");

            Receiver receiver = new Receiver("127.0.0.1", 3000);
            receiver.DataReceived += ReceivedSomeData;

            receiver.StartListen();
        }

        private static void ReceivedSomeData(object sender, EventArgs e)
        {
            var currentProcessState = (ProcessState)Enum.Parse(typeof(ProcessState), sender.ToString());

            Console.WriteLine($"Stare curenta pompe: {currentProcessState}");

            var postData = new ProcessStatusEvent(currentProcessState, DateTime.Now);

            HttpHelper.PostDataToWebAPI(postData);
        }
    }
}