using System;
using EI.SI;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Servidor // Note: actual namespace depends on the project name.
{
    internal class Program
    {
        // porto do servidor
        private const int PORT = 5000;

        static void Main(string[] args)
        {
            // criação de um servidor na porta 5000
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Loopback, PORT);
            // procura por clientes
            TcpListener listener = new TcpListener(endpoint);
            listener.Start();
            ProtocolSI protocolSI = new ProtocolSI();
            int clientConter = 0;
            Console.WriteLine("A iniciar o servidor " + IPAddress.Loopback + "...");
            Console.WriteLine("A definir a porta do servidor...");
            Console.WriteLine("PORTA: " + PORT);
            Console.WriteLine("Servidor pronto!\n");
            Console.WriteLine("A aguardar clientes...\n");

            
            while(true)
            {
                TcpClient client = listener.AcceptTcpClient();
                clientConter++;
                Console.WriteLine("Cliente {0} ligado", clientConter);

                ClientHandler handler = new ClientHandler(client, clientConter);
                handler.Handle();

            }

        } 
    }
}