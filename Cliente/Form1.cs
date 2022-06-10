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
                                if (temp.VerifyData(Encoding.UTF8.GetBytes(DecryptSymm(encrypredData, aes.Key, aes.IV)), CryptoConfig.MapNameToOID("SHA1"), signature))
                                {
                                    string msg = DecryptSymm(encrypredData, aes.Key, aes.IV);
                                    AddText(msg + Environment.NewLine);
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
            networkStream.Flush();

            package = protocolSI.Make(ProtocolSICmdType.SYM_CIPHER_DATA, EncryptSymm(textBoxMensagem.Text.Trim(), aes.Key, temp.IV));
            networkStream.Write(package, 0, package.Length);
            networkStream.Flush();
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
                while (protocolSI.GetCmdType() != ProtocolSICmdType.ACK || protocolSI.GetCmdType() != ProtocolSICmdType.NACK)
                {
                    networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);
                    if (protocolSI.GetCmdType() == ProtocolSICmdType.ACK)
                    {
                        MessageBox.Show("Registado com sucesso!");
                        return;
                    }
                    if (protocolSI.GetCmdType() == ProtocolSICmdType.NACK)
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
            textBoxChat.AppendText(text);
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
        private byte[] EncryptSymm(string data, byte[] symmKey, byte[] IV) // Função dedicada a encriptar com uma chave secreta | Criptografia simetrica
        {
            byte[] encryptedData;

            using (Aes temp = Aes.Create())
            {
                temp.Key = symmKey;
                temp.IV = IV;

                ICryptoTransform encryptor = temp.CreateEncryptor(temp.Key, temp.IV);

                using (MemoryStream memoryStream = new MemoryStream())
                {
                    using (CryptoStream cryptoStream = new CryptoStream((Stream)memoryStream, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter streamWriter = new StreamWriter((Stream)cryptoStream))
                        {
                            streamWriter.Write(data);
                        }

                        encryptedData = memoryStream.ToArray();
                    }
                }
            }
            return encryptedData;
        }
        private string DecryptSymm(byte[] encryptedData, byte[] symmKey, byte[] IV) // Função dedicada a encriptar com uma chave secreta | Criptografia simetrica
        {
            using (Aes temp = Aes.Create())
            {
                temp.Key = symmKey;
                temp.IV = IV;
                ICryptoTransform decryptor = temp.CreateDecryptor(temp.Key, temp.IV);

                using (MemoryStream memoryStream = new MemoryStream(encryptedData))
                {
                    using (CryptoStream cryptoStream = new CryptoStream((Stream)memoryStream, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader streamReader = new StreamReader((Stream)cryptoStream))
                        {
                            return streamReader.ReadToEnd();
                        }
                    }
                }
            }
        }
    }
}