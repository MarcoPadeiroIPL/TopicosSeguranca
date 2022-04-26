using System;
using EI.SI;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Servidor
{
    static class Globais
    {
        public static List<string[]> contas = new List<string[]>();
    }
    internal class ClientHandler
    {
        private User currUser;
        

        // para conveniencia
        private TcpClient currClient;
        private NetworkStream currStream;
        private ProtocolSI protocolSI;
        private bool isLogged;
        private string currUsername;
        internal ClientHandler(User currUser)
        {
            this.currUser = currUser;
            this.currClient = currUser.GetClient();
            this.currStream = currUser.GetStream();
            this.protocolSI = new ProtocolSI();
            this.isLogged = currUser.GetLogin();
            this.currUsername = currUser.GetUsername();
        }

        internal void Handle()
        {
            // Criação de Thread
            Thread thread = new Thread(threadHandler);
            thread.Start();
        }

        private void threadHandler()
        {
            // Enquanto a transmissão com o cliente não acabar
            while(protocolSI.GetCmdType() != ProtocolSICmdType.EOT)
            {
                // lê a informação da possivel mensagem enviada pelo cliente
                int bytesRead = currStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);

                // cria um pacote responsavel por armazenar a mensagem a ser enviada pelo servidor 
                byte[] package;


                switch(protocolSI.GetCmdType())
                {
                    // caso esteja a tentar fazer o login
                    case ProtocolSICmdType.USER_OPTION_1:
                        if (!currUser.GetLogin()) // se não estiver logado
                        {
                            // obtem o username e password
                            string userPass = protocolSI.GetStringFromData();
                            currUsername = userPass.Substring(0, userPass.IndexOf('/'));
                            currUser.ChangeUsername(currUsername);
                            string password = userPass.Substring(userPass.IndexOf('/') + 1, userPass.Length - currUser.GetUsername().Length - 1);

                            if (VerifyLogin(currUsername, password)) {
                                currUser.ChangeLogin(true);
                                isLogged = true;
                                package = protocolSI.Make(ProtocolSICmdType.ACK);
                                currStream.Write(package, 0, package.Length);
                                string mensagem = currUsername + " entrou no chat!";
                                Console.WriteLine(mensagem);
                                package = protocolSI.Make(ProtocolSICmdType.DATA, mensagem);
                                Program.SendToEveryone(package);
                            } 
                            else { package = protocolSI.Make(ProtocolSICmdType.NACK); }

                            currStream.Write(package, 0, package.Length);
                        }
                        else
                        {
                            // erro, já está logado 
                        }
                        break;

                    case ProtocolSICmdType.USER_OPTION_2:
                        string userPas = protocolSI.GetStringFromData();
                        string username = userPas.Substring(0, userPas.IndexOf('/'));
                        string pass = userPas.Substring(userPas.IndexOf('/') + 1, userPas.Length - username.Length - 1);
                        bool res = Register(username, pass);
                        if (res)
                        {
                            package = protocolSI.Make(ProtocolSICmdType.ACK);
                        } else
                        {
                            package = protocolSI.Make(ProtocolSICmdType.NACK);
                        }
                        currStream.Write(package, 0, package.Length); 
                        break;
                    // caso a informação recebida seja uma mensagem
                    case ProtocolSICmdType.DATA:
                        if (currUser.GetLogin())
                        {
                            string msg = currUsername + ": " + protocolSI.GetStringFromData();
                            Console.WriteLine(msg);

                            package = protocolSI.Make(ProtocolSICmdType.DATA, msg);

                            Program.SendToEveryone(package);
                        } else
                        {
                            // erro, não está logado
                        }
                        break;
                        
                    // caso a informação recebida tenha sido de fim de transmissão
                    case ProtocolSICmdType.EOT:
                        string mesg = currUsername + " saiu do chat.";
                        Console.WriteLine(mesg);
                        // confirma que recebeu a mensagem pelo envio de um ack
                        package = protocolSI.Make(ProtocolSICmdType.DATA, mesg);


                        Program.SendToEveryone(package);
                        break;
                }
            }
            // encerramento de ligação com o cliente
            currStream.Close();
            currClient.Close();
        }
        private bool VerifyLogin(string username, string password)
        {
            string[] array1 = { "marco", "123" };
            string[] array3 = { "tomas", "321" };

            Globais.contas.Add(array1);
            Globais.contas.Add(array3);
            // temporario até criar ligação com base de dados
            foreach(string[] conta in Globais.contas)
            {
                if (username == conta[0] && password == conta[1]) return true;
            }
            return false;
        }
        private bool Register(string username, string password)
        {
            string[] array = {username, password};
            foreach (string[] conta in Globais.contas)
            {
                if(username == conta[0]) return false;
            }
            Globais.contas.Add(array);
            return true;
        }
    }
}