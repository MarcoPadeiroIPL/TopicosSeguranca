using EI.SI;
using System;
using System.Net.Sockets;
using System.Threading;

namespace Servidor
{
    internal class ClientHandler
    {
        // informacao do cliente
        private List<TcpClient> _clients = new List<TcpClient>();
        private int clientID;
        private TcpClient currClient;
        internal ClientHandler(List<TcpClient> _clients, int clientID)
        {
            this._clients = _clients;
            this.clientID = clientID;
            this.currClient = _clients.ElementAt(clientID);
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

            // Enquanto a transmissão com o cliente não acabar
            while(protocolSI.GetCmdType() != ProtocolSICmdType.EOT)
            {
                // lê a informação da possivel mensagem enviada pelo cliente
                int bytesRead = networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);

                // cria um ACK - um sinal que o recetor (servidor) envia para confirmar a receção da mensagem
                byte[] ack;

                switch(protocolSI.GetCmdType())
                {
                    // caso a informação recebida seja uma mensagem
                    case ProtocolSICmdType.DATA:
                        string msg = "Cliente " + clientID + ": " + protocolSI.GetStringFromData();
                        Console.WriteLine(msg);

                        // envia a mensagem a todos os clientes
                        foreach(TcpClient client in _clients)
                        {
                            byte[] pacote = protocolSI.Make(ProtocolSICmdType.DATA, msg);
                            client.GetStream().Write(pacote, 0, pacote.Length);
                        }

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
                }

            }
            // encerramento de ligação com o cliente
            networkStream.Close();
            currClient.Close();
        }
    }
}