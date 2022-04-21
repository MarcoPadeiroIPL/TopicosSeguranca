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
            networkStream.Write(pacote, 0, pacote.Length);
            Application.Exit();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }
        private void SendMessage(string msg)
        {
            // escreve a mensagem no network
            byte[] pacote = protocolSI.Make(ProtocolSICmdType.DATA, msg);
            networkStream.Write(pacote, 0, pacote.Length);

            // vai por um ciclo enquanto não receber a confirmação que o servidor recebeu a mensagem 
            while (protocolSI.GetCmdType() != ProtocolSICmdType.ACK)
            {
                networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);
            }
            textBoxChat.Text += protocolSI.GetStringFromData();
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
    }
}