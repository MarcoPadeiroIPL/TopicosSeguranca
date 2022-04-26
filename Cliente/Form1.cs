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

            DesligarLigarChat(false);
            DesligarLigarLogin(true);
        }
                
        
        private void Form1_Load(object sender, EventArgs e)
        {

        }
        private void SendMessage(string msg)
        {
            // escreve a mensagem no network
            byte[] pacote = protocolSI.Make(ProtocolSICmdType.DATA, msg);
            networkStream.Write(pacote, 0, pacote.Length);
            networkStream.Flush();
        }
        private void ReadMessage()
        {
            while (true)
            {
                    networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);
                    if (protocolSI.GetCmdType() == ProtocolSICmdType.DATA) { AddText(protocolSI.GetStringFromData()); }
                   
               
            }
        }
        private void AddText(string text)
        {
            
            if (InvokeRequired)
            {
                this.Invoke(new Action<string>(AddText), new object[] { text });
                return;
            }
            textBoxChat.Text += text + Environment.NewLine;
        }




        private void buttonEnviar_Click(object sender, EventArgs e)
        {
            SendMessage(textBoxMensagem.Text.Trim());
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
        private void buttonClose_Click(object sender, EventArgs e)
        {
            // escreve a mensagem no network
            byte[] pacote = protocolSI.Make(ProtocolSICmdType.EOT);
            networkStream.Write(pacote, 0, pacote.Length);
            Application.Exit();
        }

        private void DesligarLigarChat(bool currState)
        {
            textBoxChat.Enabled = currState;
            textBoxMensagem.Enabled = currState;
            buttonEnviar.Enabled = currState;
        }
        private void DesligarLigarLogin(bool currState)
        {
            label1.Enabled = currState;
            label2.Enabled = currState;
            textBoxUsername.Enabled = currState;
            textBoxPassword.Enabled = currState;
            buttonLogin.Enabled = currState;
        }

        private void buttonLogin_Click(object sender, EventArgs e)
        {
            if (String.IsNullOrEmpty(textBoxUsername.Text) || String.IsNullOrEmpty(textBoxPassword.Text))
            {
                MessageBox.Show("Tem de preencher todos os campos!", "Erro de login");
            }
            else
            {
                string userPass = textBoxUsername.Text.Trim() + '/' + textBoxPassword.Text.Trim();

                // cria um pacote para enviar os dados de autenticação ao servidor
                byte[] package = protocolSI.Make(ProtocolSICmdType.USER_OPTION_1, userPass);

                // envia os dados para o servidor
                networkStream.Write(package, 0, package.Length);

                while(protocolSI.GetCmdType() != ProtocolSICmdType.ACK || protocolSI.GetCmdType() != ProtocolSICmdType.NACK)
                {
                    networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);
                    if (protocolSI.GetCmdType() == ProtocolSICmdType.ACK)
                    {
                        DesligarLigarChat(true);
                        DesligarLigarLogin(false);
                        textBoxChat.Text += "Bem-vindo ao chat!" + Environment.NewLine;
                        Thread ctThread = new Thread(ReadMessage);
                        ctThread.Start();
                        return;
                    } 
                    if(protocolSI.GetCmdType() == ProtocolSICmdType.NACK)
                    {
                        MessageBox.Show("Credenciais invalidas!");
                        return;
                    }
                }
            }
        }

        private void textBoxUsername_KeyPress(object sender, KeyPressEventArgs e)
        { 
            if (e.KeyChar == 13)
            {
                buttonLogin.PerformClick();
                textBoxMensagem.Clear();
            }
        }

        private void buttonRegister_Click(object sender, EventArgs e)
        {
            if (String.IsNullOrEmpty(textBoxUsername.Text) || String.IsNullOrEmpty(textBoxPassword.Text))
            {
                MessageBox.Show("Tem de preencher todos os campos!", "Erro de registo");
            }
            else
            {
                string userPass = textBoxUsername.Text.Trim() + '/' + textBoxPassword.Text.Trim();

                // cria um pacote para enviar os dados de autenticação ao servidor
                byte[] package = protocolSI.Make(ProtocolSICmdType.USER_OPTION_2, userPass);

                // envia os dados para o servidor
                networkStream.Write(package, 0, package.Length);

                while(protocolSI.GetCmdType() != ProtocolSICmdType.ACK || protocolSI.GetCmdType() != ProtocolSICmdType.NACK)
                {
                    networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);
                    if (protocolSI.GetCmdType() == ProtocolSICmdType.ACK)
                    {
                        MessageBox.Show("Registado com sucesso!");
                        return;
                    } 
                    if(protocolSI.GetCmdType() == ProtocolSICmdType.NACK)
                    {
                        MessageBox.Show("Conta já existe");
                        return;
                    }
                }
            }

        }
    }
}