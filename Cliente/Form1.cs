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
        private void SendMessage(byte[] package)
        {
            // escreve a mensagem no network
            networkStream.Write(package, 0, package.Length);
            networkStream.Flush();
        }
        private void ReadMessage()
        {
            while (networkStream.CanRead)
            {
                networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);
                if (protocolSI.GetCmdType() == ProtocolSICmdType.DATA) { AddText(protocolSI.GetStringFromData()); }
            }
        }
        private void AddText(string text) // Adiciona texto à textbox chat
        {
            // fix de problemas com manipulação de threads diferentes          #StackOverFlowFTW
            if (InvokeRequired)
            {
                this.Invoke(new Action<string>(AddText), new object[] { text });
                return;
            }
            textBoxChat.Text += text + Environment.NewLine;
        }




        private void buttonEnviar_Click(object sender, EventArgs e)
        {
            byte[] package = protocolSI.Make(ProtocolSICmdType.DATA, textBoxMensagem.Text.Trim());
            SendMessage(package);
            textBoxMensagem.Clear();
        }

        private void buttonMinimize_Click(object sender, EventArgs e) // UI
        {
            this.WindowState = FormWindowState.Minimized;
        }

        private void textBoxMensagem_KeyPress(object sender, KeyPressEventArgs e) // UI
        {
            if (e.KeyChar == 13)
            {
                byte[] package = protocolSI.Make(ProtocolSICmdType.DATA, textBoxMensagem.Text.Trim());
                SendMessage(package);
                textBoxMensagem.Clear();
            }
        }
        private void buttonClose_Click(object sender, EventArgs e) // UI
        {
            // escreve a mensagem para o servidor a avisar que o cliente vai encerrar a conexão
            byte[] package = protocolSI.Make(ProtocolSICmdType.EOT);
            SendMessage(package); 
            networkStream.Close();
            tcpClient.Close();
            Application.Exit();
        }

        private void DesligarLigarChat(bool currState) // UI    
        {
            textBoxChat.Enabled = currState;
            textBoxMensagem.Enabled = currState;
            buttonEnviar.Enabled = currState;
        }
        private void DesligarLigarLogin(bool currState) // UI
        {
            label1.Enabled = currState;
            label2.Enabled = currState;
            textBoxUsername.Enabled = currState;
            textBoxPassword.Enabled = currState;
            buttonLogin.Enabled = currState;
            buttonRegister.Enabled = currState;
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
                byte[] package = protocolSI.Make(ProtocolSICmdType.USER_OPTION_1, userPass); // USER_OPTION_1 == Tentativa de login

                // envia os dados para o servidor
                SendMessage(package);
                
                // vai por um loop e aguarda até a mensagem do servidor seja o pretendido     ACK == valid       NACK == invalid
                while(protocolSI.GetCmdType() != ProtocolSICmdType.ACK || protocolSI.GetCmdType() != ProtocolSICmdType.NACK)
                {
                    networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length); // le a mensagem do servidor
                    if (protocolSI.GetCmdType() == ProtocolSICmdType.ACK) // caso seja valido
                    {
                        // UI
                        DesligarLigarChat(true);     
                        DesligarLigarLogin(false);
                        textBoxChat.Text += "Bem-vindo ao chat!" + Environment.NewLine;

                        // Inicia uma nova thread dedicada a este cliente para estar sempre à procura de mensagens do servidor
                            Thread clientRead = new Thread(ReadMessage);
                            clientRead.Start();

                        return;
                    } 
                    if(protocolSI.GetCmdType() == ProtocolSICmdType.NACK) // caso as credencias sejam invalidas
                    {
                        MessageBox.Show("Credenciais invalidas!");
                        return;
                    }
                }
            }
        }

        private void textBoxUsername_KeyPress(object sender, KeyPressEventArgs e) // UI
        { 
            if (e.KeyChar == 13)
            {
                buttonLogin.PerformClick();
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
                string userPass = textBoxUsername.Text.Trim() + '/' + textBoxPassword.Text.Trim(); // cria uma nova string para armazenar o username e password
                // uso da '/' serve para facilitar o envio ao servidor, exemplo   username:teste | password:1234  | resultado:teste/1234

                // cria um pacote para enviar os dados de autenticação ao servidor
                // USER_OPTION_2 avisa que está a tentar fazer um registo
                byte[] package = protocolSI.Make(ProtocolSICmdType.USER_OPTION_2, userPass);

                // envia os dados para o servidor
                SendMessage(package);

                // aguarda por uma mensagem de validação do servidor
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