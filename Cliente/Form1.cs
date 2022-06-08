using System;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using EI.SI;
using System.Text;
using System.Security.Cryptography;

namespace ProjetoTopicosSegurança
{
  
    public partial class Form1 : Form
    {

        // Variaveis para comunicação com o servidor
        private const int PORT = 5000;
        NetworkStream networkStream; 
        ProtocolSI protocolSI;
        TcpClient tcpClient;

        // Criação de variaveis responsaveis por armazenar a chave publica do servidor (para encriptar as credenciais)
        // e para armazenar a chave simetrica (para troca de mensagens com outros cliente)
        private string serverPubKey;
        private Aes aes;

        // Criação de variaveis para armazena a chave publica e privada do servidor (para o servidor encriptar a chave simetrica)
        private string clientPubKey;
        private string clientPrivKey;

        // Digital Signature
        byte[] signature;
        public Form1()
        {
            InitializeComponent();

            // entrar em ligação com o servidor
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Loopback, PORT);
            tcpClient = new TcpClient();
            tcpClient.Connect(endpoint);
            networkStream = tcpClient.GetStream();
            protocolSI = new ProtocolSI();

            aes = Aes.Create();
            RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
            // definição da chave publica e privada do cliente
            clientPrivKey = rsa.ToXmlString(true);
            clientPubKey = rsa.ToXmlString(false);


            // enviar a chave publica ao servidor
            byte[] package = protocolSI.Make(ProtocolSICmdType.PUBLIC_KEY, clientPubKey);
            networkStream.Write(package, 0, package.Length);

            // Inicia uma nova thread dedicada a este cliente para estar sempre à procura de mensagens do servidor
            Thread clientRead = new Thread(ReadMessage);
            clientRead.Start();

            // UI
            DesligarLigarChat(false);
            DesligarLigarLogin(true);
        }
        
        
        private void ReadMessage()
        {
            while (networkStream.CanRead)
            {
                if (networkStream.DataAvailable)
                {
                    try
                    {
                        networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);

                        switch (protocolSI.GetCmdType())
                        {
                            case ProtocolSICmdType.PUBLIC_KEY: // armazenar a chave publica do servidor
                                serverPubKey = protocolSI.GetStringFromData();
                                break;
                            case ProtocolSICmdType.DIGITAL_SIGNATURE:
                                signature = protocolSI.GetData();
                                break;
                            case ProtocolSICmdType.IV:
                                aes.IV = protocolSI.GetData();
                                break;
                            case ProtocolSICmdType.SYM_CIPHER_DATA:
                                RSACryptoServiceProvider temp = new RSACryptoServiceProvider();
                                temp.FromXmlString(serverPubKey);
                                byte[] encrypredData = protocolSI.GetData();
                                if (temp.VerifyData(encrypredData, CryptoConfig.MapNameToOID("SHA1"), signature))
                                {
                                    string msg = Encoding.UTF8.GetString(DecryptSymm(encrypredData, aes.Key, aes.IV));
                                    AddText(msg);
                                }
                                break;
                            case ProtocolSICmdType.SECRET_KEY:
                                byte[] encryptedKey = protocolSI.GetData();
                                aes.Key = DecryptAssym(encryptedKey, clientPrivKey);
                                ChangeUI(true);
                                // UI
                                break;
                            case ProtocolSICmdType.NACK: // erro no login
                                MessageBox.Show("Erro no login");
                                break;
                            case ProtocolSICmdType.DATA:
                                AddText(Environment.NewLine + protocolSI.GetStringFromData());
                                break;
                        }
                    }
                catch (Exception ex) { }


                }
                                            }
        }
        private void buttonLogin_Click(object sender, EventArgs e)
        {
            if (String.IsNullOrEmpty(textBoxUsername.Text) || String.IsNullOrEmpty(textBoxPassword.Text))
            {
                MessageBox.Show("Tem de preencher todos os campos!", "Erro de login");
            }
            else
            {
                byte[] data = Encoding.UTF8.GetBytes(textBoxUsername.Text.Trim() + '/' + textBoxPassword.Text.Trim());
                // envia os dados para o servidor
                byte[] encryptedData = EncryptAssym(data, serverPubKey);
                byte[] package = protocolSI.Make(ProtocolSICmdType.USER_OPTION_1, encryptedData);
                networkStream.Write(package, 0, package.Length);
                networkStream.Flush();
            }
        }
        private void buttonEnviar_Click(object sender, EventArgs e)
        {
            byte[] package;
            // assinar o hash

            Aes temp = Aes.Create();
            temp.Key = aes.Key;

            package = protocolSI.Make(ProtocolSICmdType.IV, temp.IV);
            networkStream.Write(package, 0, package.Length);
            byte[] data = Encoding.UTF8.GetBytes(textBoxMensagem.Text);
            SendEncryptedSym(data, aes.Key, temp.IV);
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
                buttonEnviar.PerformClick();
            }
        }
        private void buttonClose_Click(object sender, EventArgs e) // UI
        {
            // escreve a mensagem para o servidor a avisar que o cliente vai encerrar a conexão
            byte[] package = protocolSI.Make(ProtocolSICmdType.EOT);
            networkStream.Write(package, 0, package.Length); 
            networkStream.Close();
            tcpClient.Close();
            Application.Exit();
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
                byte[] data = Encoding.UTF8.GetBytes(textBoxUsername.Text.Trim() + '/' + textBoxUsername.Text.Trim());
                // envia os dados para o servidor
                byte[] encryptedData = EncryptAssym(data, serverPubKey);
                byte[] package = protocolSI.Make(ProtocolSICmdType.USER_OPTION_1, encryptedData);
                networkStream.Write(package, 0, package.Length);
                networkStream.Flush();

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
        private void AddText(string text) // Adiciona texto à textbox chat
        {
            // fix de problemas com manipulação de threads diferentes          #StackOverFlowFTW
            if (InvokeRequired)
            {
                this.Invoke(new Action<string>(AddText), new object[] { text });
                return;
            }
            textBoxChat.Text += text;
        }
        private void ChangeUI(bool res)
        {
            // fix de problemas com manipulação de threads diferentes          #StackOverFlowFTW
            if (InvokeRequired)
            {
                this.Invoke(new Action<bool>(DesligarLigarChat), new object[] { res });
                this.Invoke(new Action<bool>(DesligarLigarLogin), new object[] { !res });
                return;
            }
            DesligarLigarChat(res);
            DesligarLigarLogin(!res);
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
        private void SendEncryptedAssym(byte[] data, string pubKey) // Função dedicada a enviar mensagens encriptadas com uma chave publica | Criptografia assimetrica
        {
            byte[] encryptedData = EncryptAssym(data, pubKey);
            byte[] package = protocolSI.Make(ProtocolSICmdType.ASSYM_CIPHER_DATA, encryptedData);
            networkStream.Write(package, 0, package.Length);
            networkStream.Flush();
        }
        private void SendEncryptedSym(byte[] data, byte[] symmKey, byte[] IV) // Função dedicada a enviar mensagens encriptadas com uma chave secreta | Criptografia simetrica
        {
            byte[] encryptedData = EncryptSymm(data, symmKey, IV);
            byte[] package = protocolSI.Make(ProtocolSICmdType.SYM_CIPHER_DATA, encryptedData);
            networkStream.Write(package, 0, package.Length);
            networkStream.Flush();
        }
        private byte[] EncryptAssym(byte[] data, string pubKey) // Função dedicada a encriptar com uma chave publica | Criptografia assimetrica
        {
            byte[] encryptedData;
            RSACryptoServiceProvider temp = new RSACryptoServiceProvider();
            temp.FromXmlString(pubKey);
            encryptedData = temp.Encrypt(data, false);

            return encryptedData;
        }
        private byte[] DecryptAssym(byte[] encryptedData, string privKey) // Função dedicada a decriptar com uma chave privada | Criptografia assimetrica
        {
            RSACryptoServiceProvider temp = new RSACryptoServiceProvider();
            temp.FromXmlString(privKey);
            byte[] data = temp.Decrypt(encryptedData, false);

            return data;
        }
        private byte[] EncryptSymm(byte[] data, byte[] symmKey, byte[] IV) // Função dedicada a encriptar com uma chave secreta | Criptografia simetrica
        {
            byte[] encryptedData;

            Aes temp;               // variavel temporaria para armazenar a chave simetrica com os valores passados por parametro
            
            // Inicialização da chave simetrica com os valores passados por parametro
            temp = Aes.Create();
            temp.Key = symmKey;
            temp.IV = IV;

            MemoryStream ms = new MemoryStream();
            CryptoStream cs = new CryptoStream(ms, temp.CreateEncryptor(), CryptoStreamMode.Write);

            cs.Write(data, 0, data.Length);
            cs.Close();

            encryptedData = ms.ToArray();

            return encryptedData;
        }
        private byte[] DecryptSymm(byte[] encryptedData, byte[] symmKey, byte[] IV) // Função dedicada a encriptar com uma chave secreta | Criptografia simetrica
        {
            string msg;             // variavel temporaria para armazenar a mensagem decriptada em formato string
            Aes temp;               // variavel temporaria para armazenar a chave simetrica com os valores passados por parametro
            
            // Inicialização da chave simetrica com os valores passados por parametro
            temp = Aes.Create();
            temp.Key = symmKey;
            temp.IV = IV;

            // decriptação da informação
            MemoryStream ms = new MemoryStream(encryptedData);
            CryptoStream cs = new CryptoStream(ms, temp.CreateDecryptor(), CryptoStreamMode.Read);
            byte[] data = new byte[ms.Length];            // variavel temporaria para armazenar a mensagem decriptada em formato byte
            int bytesLidos = cs.Read(data, 0, data.Length);
            cs.Close();

            return data;
        }
    }
}