using System;
using System.Net;
using System.Net.Sockets;

namespace Communicator
{
    public class Receiver
    {
        private TcpListener _receiver;
        public EventHandler DataReceived;

        public Receiver(string ipAddress, int portNumber)
        {
            _receiver=new TcpListener(IPAddress.Parse(ipAddress), portNumber);
        }

        public void StartListen()
        {
            try
            {
                _receiver.Start();
                byte[] bytes = new byte[256];
                while (true)
                {
                    Console.WriteLine("Asteptare conexiune...");
                    TcpClient client = _receiver.AcceptTcpClient();
                    Console.WriteLine("Conectat!");

                    NetworkStream stream = client.GetStream();
                    int i;

                    while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
                    {
                        var importantValue = bytes[0];
                        DataReceived?.Invoke(importantValue, null);
                    }
                    client.Close();
                }
            }
            catch (Exception ex) {
                Console.WriteLine(ex.ToString());
            }
        }
    }
}
