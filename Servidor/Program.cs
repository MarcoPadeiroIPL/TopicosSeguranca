using System;
using EI.SI;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Servidor // Note: actual namespace depends on the project name.
{
    static class Globals
    {
        public static List<User> users = new List<User>();
    }
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
            int clientCounter = 0;
            Console.WriteLine("A iniciar o servidor " + IPAddress.Loopback + "...");
            Console.WriteLine("A definir a porta do servidor...");
            Console.WriteLine("PORTA: " + PORT);
            Console.WriteLine("Servidor pronto!\n");
            Console.WriteLine("A aguardar clientes...\n");

            
            while(true)
            {
                TcpClient client = listener.AcceptTcpClient();
                clientCounter++;

                User currUtilizador = new User(client, false);  
                Globals.users.Add(currUtilizador);

                Console.WriteLine("Alguém está a tentar entrar...");

                ClientHandler handler = new ClientHandler(currUtilizador);
                handler.Handle();
            }
        } 
        public static void SendToEveryone(byte[] package)
        {
            foreach(User user in Globals.users)
            {
                NetworkStream networkStream = user.GetStream();
                networkStream.Write(package, 0, package.Length);
                networkStream.Flush();
            }
        }
    }
}