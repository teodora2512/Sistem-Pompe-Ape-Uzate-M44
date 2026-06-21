using System;
using System.Net.Sockets;

namespace Communicator
{
    public class Sender
    {
        private TcpClient _sender;

        public Sender(string ipAddress, int portNumber)
        {
            _sender = new TcpClient(ipAddress, portNumber);
        }

        public void Send(byte valueToSend)
        {
            try
            {
                NetworkStream nwStream = _sender.GetStream();
                byte[] bytesToSend = new byte[4];
                bytesToSend[0] = valueToSend;
                nwStream.Write(bytesToSend, 0, bytesToSend.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }
}
