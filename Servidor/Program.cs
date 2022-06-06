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
        //public static string currentPath = @"C:\temp\servidor\logs\log" + DateTime.Now.ToString("yy-MM-dd_HH:mm:ss") + ".txt";
        public static string currentPath = Directory.GetCurrentDirectory() + "\\logs\\log" + DateTime.Now.ToString("yy/MM/dd__HH.mm.ss") + ".txt";
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
            WriteToLog("A iniciar o servidor " + IPAddress.Loopback + "...");
            WriteToLog("A definir a porta do servidor");
            WriteToLog("PORTA: " + PORT);
            WriteToLog("Servidor pronto!\n");
            WriteToLog("A aguardar clientes...\n");

            // vai à procura de clientes    
            while(true)
            {
                // quando acha um cliente
                TcpClient client = listener.AcceptTcpClient();

                clientCounter++;
                User currUtilizador = new User(client, false); // criação de nova instancia de um user 
                Globals.users.Add(currUtilizador); // adiciona à lista global de todos os users


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
        public static void WriteToLog(string msg)
        {
            File.WriteAllText("log" + DateTime.Now.ToString("yy_MM_dd__HH.mm.ss") + ".txt", msg);
            Console.WriteLine(msg);
            
        }
    }
}