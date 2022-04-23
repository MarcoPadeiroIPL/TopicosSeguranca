using EI.SI;
using System;
using System.Net.Sockets;
using System.Threading;

namespace Servidor
{
    internal class ClientHandler
    {
        // informacao do cliente
        private List<TcpClient> tcpClients; 
        private int clientID;
        private TcpClient currClient;
        internal ClientHandler(List<TcpClient> clients, int clientID)
        {
            this.tcpClients = clients;
            this.clientID = clientID;
            this.currClient = tcpClients.ElementAt(clientID);
        }

        internal void Handle()
        {
            // Criação de Thread
            Thread thread = new Thread(threadHandler);
            thread.Start();
        }

        private void threadHandler()
        {
            // Armazena a conexão do cliente
            NetworkStream networkStream = this.currClient.GetStream();
            ProtocolSI protocolSI = new ProtocolSI();

            string mensagem = "Cliente " + clientID + " conectou-se";
            Console.WriteLine(mensagem);
            //byte[] pct = System.Text.Encoding.ASCII.GetBytes(mensagem);
            //foreach(TcpClient a in tcpClients)
            //{
              //  a.GetStream().Write(pct, 0, pct.Length);
            //}

            // Enquanto a transmissão com o cliente não acabar
            while(protocolSI.GetCmdType() != ProtocolSICmdType.EOT)
            {
                // lê a informação da possivel mensagem enviada pelo cliente
                int bytesRead = networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);

                // cria um ACK - um sinal que o recetor (servidor) envia para confirmar a receção da mensagem
                byte[] ack;

                string msg = string.Empty;

                if(protocolSI.GetCmdType() == ProtocolSICmdType.DATA)
                {
                    msg = "Cliente " + clientID + ": " + protocolSI.GetStringFromData();
                } else
                {
                    msg = "Cliente " + clientID + " desconectou-se";
                }
                Console.WriteLine(msg);
                byte [] pacote = System.Text.Encoding.ASCII.GetBytes(msg);

                foreach(TcpClient c in tcpClients)
                {
                    NetworkStream stream = c.GetStream();
                    stream.Write(pacote, 0, pacote.Length);
                }
                /*switch(protocolSI.GetCmdType())
                {
                    // caso a informação recebida seja uma mensagem
                    case ProtocolSICmdType.DATA:
                        string msg = "Cliente " + clientID + ": " + protocolSI.GetStringFromData();
                        Console.WriteLine(msg);
                        // confirma que recebeu a mensagem pelo envio de um ack
                        ack = protocolSI.Make(ProtocolSICmdType.ACK);
                        networkStream.Write(ack, 0, ack.Length);
                        break;
                    // caso a informação recebida tenha sido de fim de transmissão
                    case ProtocolSICmdType.EOT:
                        Console.WriteLine("Cliente {0} desligado.", clientID);
                        // confirma que recebeu a mensagem pelo envio de um ack
                        ack = protocolSI.Make(ProtocolSICmdType.ACK);
                        networkStream.Write(ack, 0, ack.Length);
                        break;
                }*/

            }
            // encerramento de ligação com o cliente
            networkStream.Close();
            currClient.Close();
        }
    }
}