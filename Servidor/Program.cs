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
        public static List<User> users = new List<User>(); // variavel global que armazena todos os users
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

            // vai à procura de clientes    
            while(true)
            {
                // quando acha um cliente
                TcpClient client = listener.AcceptTcpClient();

                clientCounter++;
                User currUtilizador = new User(client, false); // criação de nova instancia de um user 
                Globals.users.Add(currUtilizador); // adiciona à lista global de todos os users

                Console.WriteLine(DateTime.Now.ToString("[hh:mm]") + " Alguém está a tentar entrar..."); 

                ClientHandler handler = new ClientHandler(currUtilizador);
                handler.Handle();
            }
        } 
        public static void SendToEveryone(byte[] package) // faz um broadcast de uma mensagem para todos os clientes
        {
            foreach(User user in Globals.users)
            {
                if(user.GetLogin())
                {
                    NetworkStream networkStream = user.GetStream();
                    networkStream.Write(package, 0, package.Length);
                    networkStream.Flush();
                }
               
            }
        }
    }
}