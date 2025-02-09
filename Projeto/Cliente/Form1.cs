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
        private byte[] signature;
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


        private void ReadMessage() // função responsavel por receber mensagens do servidor
        {
            while (networkStream.CanRead)  // repete enquanto o network continuar disponivel
            {
                if (networkStream.DataAvailable)  // caso haja informação disponivel na stream
                {
                    try
                    {
                        networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length); // le a mensagem

                        switch (protocolSI.GetCmdType()) // verifica o objetivo da mensagem
                        {
                            case ProtocolSICmdType.PUBLIC_KEY: // armazenar a chave publica do servidor
                                serverPubKey = protocolSI.GetStringFromData();
                                break;
                            case ProtocolSICmdType.DIGITAL_SIGNATURE: // armazenar a assinatura digital da mensagem recebida
                                signature = protocolSI.GetData();
                                break;
                            case ProtocolSICmdType.IV:              // armazenar o vetor de inicialização 
                                aes.IV = protocolSI.GetData();
                                break;
                            case ProtocolSICmdType.SYM_CIPHER_DATA:     // receber uma mensagem cifrada por uma chave simetrica

                                // inicializa um RSA para verificar a assinatura da mensagem
                                RSACryptoServiceProvider temp = new RSACryptoServiceProvider();
                                temp.FromXmlString(serverPubKey);
                                byte[] encrypredData = protocolSI.GetData();

                                // verifica se a mensagem foi assinada pelo servidor
                                if (temp.VerifyData(Encoding.UTF8.GetBytes(DecryptSymm(encrypredData, aes.Key, aes.IV)), CryptoConfig.MapNameToOID("SHA1"), signature))
                                {
                                    // caso tenha sido, decripta com a chave secreta apresenta o texto no chat
                                    string msg = DecryptSymm(encrypredData, aes.Key, aes.IV);
                                    AddText(msg + Environment.NewLine);
                                }
                                break;
                            case ProtocolSICmdType.SECRET_KEY:                          // armazenar a chave secreta

                                // inicializa um RSA para verificar a assinatura da mensagem
                                RSACryptoServiceProvider temp1 = new RSACryptoServiceProvider();
                                temp1.FromXmlString(serverPubKey);
                                byte[] encryptedKey = protocolSI.GetData();

                                // verifica se a mensagem foi assinada pelo servidor
                                if (temp1.VerifyData(DecryptAssym(encryptedKey, clientPrivKey), CryptoConfig.MapNameToOID("SHA1"), signature))
                                {
                                    aes.Key = DecryptAssym(encryptedKey, clientPrivKey);
                                    ChangeUI(true);
                                    // UI
                                }
                                break;
                            case ProtocolSICmdType.NACK: // erro no login
                                ShowError("Credenciais invalidas!"); 
                                break;
                            case ProtocolSICmdType.USER_OPTION_1: // sucesso no registo
                                ShowError("Registo com sucesso!"); 
                                break;
                            case ProtocolSICmdType.USER_OPTION_2: // erro no registo
                                ShowError("Utilizador já existe!"); 
                                break;
                            case ProtocolSICmdType.DATA:            // para avisar de utilizadores que entraram e sairam
                                AddText(protocolSI.GetStringFromData() + Environment.NewLine);
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
                // envia as credenciais encriptadas para o servidor
                byte[] encryptedData = EncryptAssym(data, serverPubKey);
                byte[] package = protocolSI.Make(ProtocolSICmdType.USER_OPTION_1, encryptedData);
                networkStream.Write(package, 0, package.Length);
                networkStream.Flush();
            }
        }
        private void buttonEnviar_Click(object sender, EventArgs e)
        {

            if (!String.IsNullOrEmpty(textBoxMensagem.Text))
            {
                byte[] package;

                // gera um novo vetor de incialização, envia-o e encripta a mensagem com a chave secreta e o vetor de inicialização
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
               
        }
        private void textBoxMensagem_KeyPress(object sender, KeyPressEventArgs e) // UI
        {
            if (e.KeyChar == 13)
            {
                buttonEnviar.PerformClick();
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
                byte[] data = Encoding.UTF8.GetBytes(textBoxUsername.Text.Trim() + '/' + textBoxPassword.Text.Trim());
                // envia os dados para o servidor
                byte[] encryptedData = EncryptAssym(data, serverPubKey);
                byte[] package = protocolSI.Make(ProtocolSICmdType.USER_OPTION_2, encryptedData);
                networkStream.Write(package, 0, package.Length);
                networkStream.Flush();
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
        private void ShowError(string error) // Adiciona texto à textbox chat
        {
            // fix de problemas com manipulação de threads diferentes          #StackOverFlowFTW
            if (InvokeRequired)
            {
                this.Invoke(new Action<string>(ShowError), new object[] { error });
                return;
            }
            MessageBox.Show(error);
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
            buttonLogout.Visible = currState;
        }
        private void DesligarLigarLogin(bool currState) // UI
        {
            label1.Enabled = currState;
            label2.Enabled = currState;
            textBoxUsername.Enabled = currState;
            textBoxPassword.Enabled = currState;
            buttonLogin.Visible = currState;
            buttonRegister.Visible = currState;
            buttonLogout.Visible = !currState;
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
        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            // escreve a mensagem para o servidor a avisar que o cliente vai encerrar a conexão
            byte[] package = protocolSI.Make(ProtocolSICmdType.EOT);
            try
            {
                networkStream.Write(package, 0, package.Length);
                networkStream.Close();
                tcpClient.Close();
            }
            catch (Exception ex)
            {

            }
        }

        private void buttonLogout_Click(object sender, EventArgs e)
        {
            byte[] package = protocolSI.Make(ProtocolSICmdType.USER_OPTION_3);
            networkStream.Write(package, 0, package.Length);
            networkStream.Flush();
            DesligarLigarChat(false);
            DesligarLigarLogin(true);
            textBoxChat.Clear();
            aes.Clear();
            textBoxMensagem.Clear();
            textBoxUsername.Clear();
            textBoxPassword.Clear();
        }
    }
}