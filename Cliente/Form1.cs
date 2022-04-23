using System;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using EI.SI;

namespace ProjetoTopicosSegurança
{
    public partial class Form1 : Form
    {

        private const int PORT = 5000;
        NetworkStream networkStream; 
        ProtocolSI protocolSI;
        TcpClient tcpClient;
        public Form1()
        {
            InitializeComponent();
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Loopback, PORT);
            tcpClient = new TcpClient();
            tcpClient.Connect(endpoint);
            networkStream = tcpClient.GetStream();
            protocolSI = new ProtocolSI();

                      

        }

        private void buttonClose_Click(object sender, EventArgs e)
        {
            // escreve a mensagem no network
            byte[] pacote = protocolSI.Make(ProtocolSICmdType.EOT);
            try
            {
                networkStream.Write(pacote, 0, pacote.Length);
                Application.Exit();
            }
            catch
            {
                Application.Exit();
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }
        private void ReadMessage()
        {
            // Buffer to store the response bytes.
            byte[] data = new Byte[256];

            // String to store the response ASCII representation.
            String responseData = String.Empty;
            // Read the first batch of the TcpServer response bytes.
            Int32 bytes = networkStream.Read(data, 0, data.Length); //(**This receives the data using the byte method**)
            responseData = System.Text.Encoding.ASCII.GetString(data, 0, bytes); //(**This converts it to string**)
            textBoxChat.AppendText(responseData + Environment.NewLine);
            // vai por um ciclo enquanto não receber a confirmação que o servidor recebeu a mensagem 
            // while (protocolSI.GetCmdType() != ProtocolSICmdType.ACK)
            //{
            //  networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);
            // }


            
           
        }
        private void SendMessage(string msg)
        {
            // escreve a mensagem no network
            byte[] pacote = protocolSI.Make(ProtocolSICmdType.DATA, msg);
            try
            {
                if (!String.IsNullOrEmpty(msg))
                {
                    networkStream.Write(pacote, 0, pacote.Length);
                    ReadMessage();
                }
            }
            catch
            {
                MessageBox.Show("A conexão com o servidor foi interrompida");
                Application.Exit();
            }
        }
        private void buttonEnviar_Click(object sender, EventArgs e)
        {
            SendMessage(textBoxMensagem.Text);
            textBoxMensagem.Clear();
            
        }

        private void buttonMinimize_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
        }

        private void textBoxMensagem_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13)
            {
                SendMessage(textBoxMensagem.Text);
                textBoxMensagem.Clear();
            }
        }

        private void buttonRead_Click(object sender, EventArgs e)
        {
            ReadMessage();
        }
    }
}