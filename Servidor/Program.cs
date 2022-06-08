using System;
using EI.SI;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Security.Cryptography;

namespace Servidor // Note: actual namespace depends on the project name.
{
    static class Globals
    {
        public static List<User> users = new List<User>(); // variavel global que armazena todos os users
        public static string currentPath = Directory.GetCurrentDirectory() + "\\logs\\log" + DateTime.Now.ToString("yy_MM_dd__HH.mm.ss") + ".txt";
    }
    internal class Program
    {
        // porto do servidor
        private const int PORT = 5000;

        // criação da chave simetrica
        private static Aes aes;

        static void Main(string[] args)
        {
            // criação de um servidor na porta 5000
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Loopback, PORT);
            // procura por clientes
            TcpListener listener = new TcpListener(endpoint);
            listener.Start();
            ProtocolSI protocolSI = new ProtocolSI();
            int clientCounter = 0;

            // definição da chave simetrica (Criação da key e do IV)
            aes = Aes.Create();

            // definição da chave publica e privada
            RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
            // definição da chave privada
            string privKey = rsa.ToXmlString(true);
            string pubKey = rsa.ToXmlString(false);

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
                
                // adiciona à lista de utilizadores
                clientCounter++;
                User currUtilizador = new User(client, false, clientCounter); // criação de nova instancia de um user 
                Globals.users.Add(currUtilizador); // adiciona à lista global de todos os users

                // executa a função da class ClienteHandler que é responsavel por criar uma nova thread dedicada ao cliente
                ClientHandler handler = new ClientHandler(currUtilizador, aes.Key, aes.IV, privKey, pubKey);
                handler.Handle();
            }
        } 
        public static void SendToEveryone(byte[] package) // faz um broadcast de uma mensagem para todos os clientes
        {
            // vai por todos os utilizadores na lista
            foreach(User user in Globals.users)
            {
                // caso estejam logados
                if(user.GetLogin())
                {
                    // obtem a stream entre o servidor e o cliente
                    NetworkStream networkStream = user.GetStream();
                    // envia a mensagem ao cliente
                    networkStream.Write(package, 0, package.Length);
                    networkStream.Flush();
                }
               
            }
        }
        public static void WriteToLog(string msg) // função chamada quando se quer escrever no servidor
        {

            // escreve na consola os dados enviados por parametro
            Console.WriteLine(msg);

            FileStream fs = new FileStream(Globals.currentPath, FileMode.Append, FileAccess.Write);
            StreamWriter sw = new StreamWriter(fs);

            sw.WriteLine(msg);

            sw.Close();
            fs.Close();

        }
    }
}