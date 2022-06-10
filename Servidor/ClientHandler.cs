using System;
using EI.SI;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Data.SqlClient;
using System.Security.Cryptography;

namespace Servidor
{
    internal class ClientHandler
    {
        private ProtocolSI protocolSI;

        private User currUser;
        private const int SALTSIZE = 8;
        private const int NUMBER_OF_ITERATIONS = 50000;

        // armazenamento da chave simetrica, publica e privada do servidor
        private Aes aes;
        private string serverPrivKey;
        private string serverPubKey;

        // armazenamento da chave publica do cliente
        private string clientPubKey;

        // armazenamento da assinatura digital
        private byte[] signature;

        internal ClientHandler(User currUser, byte[] symmKey, byte[] IV, string serverPrivKey, string serverPubKey)
        {
            this.currUser = currUser;
            protocolSI = new ProtocolSI();
            this.aes = Aes.Create();
            this.aes.Key = symmKey;
            this.aes.IV = IV;
            this.serverPrivKey = serverPrivKey;
            this.serverPubKey = serverPubKey;
        }

        internal void Handle()
        {
            // Criação de Thread
            Thread thread = new Thread(threadHandler);
            thread.Start();
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

        private void threadHandler()
        {
            Program.WriteToLog(DateTime.Now.ToString("[HH:mm]") + " O cliente " + currUser.id + " está a tentar entrar...");

            // mal o cliente entra, envia a chave publica ao cliente
            byte[] package = protocolSI.Make(ProtocolSICmdType.PUBLIC_KEY, serverPubKey);
            currUser.GetStream().Write(package, 0, package.Length);
            Program.WriteToLog(DateTime.Now.ToString("[HH:mm]") + " O servidor enviou a sua chave publica ao cliente a tentar entrar");

            // Enquanto a transmissão com o cliente não acabar
            while (currUser.GetStream().CanRead)
            {
                // caso exista informação disponivel na stream
                if (currUser.GetStream().DataAvailable)
                {
                    // lê a informação da possivel mensagem enviada pelo cliente
                    currUser.GetStream().Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);

                    switch (protocolSI.GetCmdType())
                    {
                        case ProtocolSICmdType.PUBLIC_KEY:                  // Caso o Servidor receba a chave publica do cliente, armazena na variavel clientPubKey
                            clientPubKey = protocolSI.GetStringFromData();
                            Program.WriteToLog(DateTime.Now.ToString("[HH:mm]") + " O cliente " + currUser.id + " enviou a sua chave publica ao servidor");
                            break;
                        case ProtocolSICmdType.DIGITAL_SIGNATURE:           // Caso so servidor receba uma assinatura, armazena-a numa variavel para validar o package mais tarde
                            signature = protocolSI.GetData();
                            break;
                        case ProtocolSICmdType.USER_OPTION_1:               // USER_OPTION_1 == Cliente a tentar fazer o login              
                            if (!currUser.GetLogin())                       // caso o utilizador atual ainda não esteja logado
                            {
                                Program.WriteToLog(DateTime.Now.ToString("[HH:mm]") + " O cliente " + currUser.id + " enviou as suas credencias encriptadas pela chave publica do servidor");
                                // Decripta a mensagem recebida pelo cliente, que neste caso é uma combinação do username e da password dividido por uma '/'
                                string userPass = System.Text.Encoding.UTF8.GetString(DecryptAssym(protocolSI.GetData(), serverPrivKey));

                                Program.WriteToLog(DateTime.Now.ToString("[HH:mm]") + " O servidor utilizou a sua chave privada para a decriptação das credencias");
                                currUser.ChangeUsername(userPass.Substring(0, userPass.IndexOf('/')));
                                string password = userPass.Substring(userPass.IndexOf('/') + 1, userPass.Length - currUser.GetUsername().Length - 1);

                                Program.WriteToLog(DateTime.Now.ToString("[HH:mm]") + " A validar as credenciais do cliente " + currUser.id + "...");
                                if (VerifyLogin(currUser.GetUsername(), password)) // Validação das credencias enviadas pelo cliente
                                {
                                    Program.WriteToLog(DateTime.Now.ToString("[HH:mm]") + " As credenciais do cliente " + currUser.id + " são validas!");

                                    currUser.ChangeLogin(true); // alteração do estado de login do cliente
                                    currUser.ChangeUsername(currUser.GetUsername()); // alteração do username do cliente para o username enviado pelo cliente

                                    Program.WriteToLog(DateTime.Now.ToString("[HH:mm]") + " A encriptar a chave secreta com a chave publica do cliente " + currUser.GetUsername() + "!");
                                    Program.WriteToLog(DateTime.Now.ToString("[HH:mm]") + " A enviar a chave secreta ao cliente " + currUser.GetUsername() + "...");

                                    // encripta a chave simetrica com a chave publica do cliente e envia-a
                                    package = protocolSI.Make(ProtocolSICmdType.SECRET_KEY, EncryptAssym(aes.Key, clientPubKey));
                                    currUser.GetStream().Write(package, 0, package.Length);
                                    currUser.GetStream().Flush();


                                    // envia uma mensagem a todos os outros clientes a avisar que alguem entrou no chat
                                    Program.WriteToLog(DateTime.Now.ToString("[HH:mm]") + " A avisar o resto dos clientes que o utilizador " + currUser.GetUsername() + " entrou no chat!");
                                    string mensagem = DateTime.Now.ToString("[HH:mm]") + " " + currUser.GetUsername() + " entrou no chat!";
                                    package = protocolSI.Make(ProtocolSICmdType.DATA, mensagem);
                                    Program.SendToEveryone(package);
                                }
                                else // Caso o login não seja valido
                                {
                                    Program.WriteToLog(DateTime.Now.ToString("[HH:mm]") + " As credenciais do cliente " + currUser.id + " não são validas!");
                                    package = protocolSI.Make(ProtocolSICmdType.NACK);
                                    currUser.GetStream().Write(package, 0, package.Length);
                                    currUser.GetStream().Flush();
                                }
                            }
                            break;
                        case ProtocolSICmdType.USER_OPTION_2: //utilizador registar
                            // obtem o username e password enviada pelo cliente
                            Program.WriteToLog(DateTime.Now.ToString("[HH:mm]") + " O cliente " + currUser.id + " enviou as suas credencias encriptadas pela chave publica do servidor para se registar!");
                            string userPas = System.Text.Encoding.UTF8.GetString(DecryptAssym(protocolSI.GetData(), serverPrivKey));

                            Program.WriteToLog(DateTime.Now.ToString("[HH:mm]") + " O servidor utilizou a sua chave privada para a decriptação das credencias");
                            string username = userPas.Substring(0, userPas.IndexOf('/'));
                            string pass = userPas.Substring(userPas.IndexOf('/') + 1, userPas.Length - username.Length - 1);


                            byte[] salt = GenerateSalt(SALTSIZE);
                            byte[] hash = GenerateSaltedHash(pass, salt);

                            // regista o cliente e verifica se foi efetuado com succeso ou não
                            if (Register(username, hash, salt))
                            {
                                Program.WriteToLog(DateTime.Now.ToString("[hh:MM]") + " O registo foi efetuado com sucesso!");
                                package = protocolSI.Make(ProtocolSICmdType.USER_OPTION_1);
                            }
                            else
                            {
                                Program.WriteToLog(DateTime.Now.ToString("[HH:mm]") + " O registo não foi efetuado com sucesso!");
                                package = protocolSI.Make(ProtocolSICmdType.USER_OPTION_2);
                            }
                            currUser.GetStream().Write(package, 0, package.Length);
                            currUser.GetStream().Flush();
                            break;
                        case ProtocolSICmdType.IV:
                            Program.WriteToLog(DateTime.Now.ToString("[HH:mm]") + " O servidor recebeu um vetor de inicialização");
                            aes.IV = protocolSI.GetData();
                            package = protocolSI.Make(ProtocolSICmdType.IV, protocolSI.GetData());
                            Program.SendToEveryone(package);
                            break;
                        // caso a informação recebida seja uma mensagem
                        case ProtocolSICmdType.SYM_CIPHER_DATA:
                            if (currUser.GetLogin())
                            {
                                Program.WriteToLog(DateTime.Now.ToString("[HH:mm]") + " O cliente " + currUser.GetUsername() + " enviou uma mensagem encriptada com a chave secreta para o servidor!");

                                byte[] textWithUsername = System.Text.Encoding.UTF8.GetBytes(DateTime.Now.ToString("[HH:mm]") + " " + currUser.GetUsername() + ": " + DecryptSymm(protocolSI.GetData(), aes.Key, aes.IV));

                                Program.WriteToLog(DateTime.Now.ToString("[HH:mm]") + " O servidor assinou a mensagem enviada pelo cliente " + currUser.GetUsername() + "!");
                                // assinar o hash
                                RSACryptoServiceProvider rsa1 = new RSACryptoServiceProvider();
                                rsa1.FromXmlString(serverPrivKey);
                                byte[] signature = rsa1.SignData(textWithUsername, CryptoConfig.MapNameToOID("SHA1"));

                                // envia a todos os clientes
                                Program.WriteToLog(DateTime.Now.ToString("[HH:mm]") + " O servidor enviou a assinatura para todos os clientes!");
                                package = protocolSI.Make(ProtocolSICmdType.DIGITAL_SIGNATURE, signature);
                                Program.SendToEveryone(package);

                                Program.WriteToLog(DateTime.Now.ToString("[HH:mm]") + " O servidor enviou a mensagem encriptada e assinada para todos os clientes!");
                                package = protocolSI.Make(ProtocolSICmdType.SYM_CIPHER_DATA, EncryptSymm(textWithUsername, aes.Key, aes.IV));
                                Program.SendToEveryone(package);

                            }
                            else
                            {
                                // erro, não está logado
                            }
                            break;
                        // caso a informação recebida tenha sido de fim de transmissão
                        case ProtocolSICmdType.EOT:
                            string mesg = DateTime.Now.ToString("[HH:mm]") + " " + currUser.GetUsername() + " saiu do chat.";
                            Program.WriteToLog(mesg);
                            package = protocolSI.Make(ProtocolSICmdType.USER_OPTION_2, currUser.GetUsername());
                            Globals.users.Remove(currUser);
                            Program.SendToEveryone(package);

                            currUser.GetStream().Close();
                            currUser.GetClient().Close();
                            break;
                    }
                    currUser.GetStream().Flush();
                }
            }
            // encerramento de ligação com o cliente
            currUser.GetStream().Close();
            currUser.GetClient().Close();
        }
        private byte[] EncryptSymm(byte[] data, byte[] symmKey, byte[] IV) // Função dedicada a encriptar com uma chave secreta | Criptografia simetrica
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
                            streamWriter.Write(System.Text.Encoding.UTF8.GetString(data));
                        }

                        encryptedData = memoryStream.ToArray();
                    }
                }
            }
            return encryptedData;
        }
        private byte[] EncryptAssym(byte[] data, string pubKey) // Função que encripta apartir da chave publica | Criptografia assimetrica 
        {
            byte[] encryptedData;

            RSACryptoServiceProvider temp = new RSACryptoServiceProvider();
            temp.FromXmlString(pubKey);
            encryptedData = temp.Encrypt(data, false);

            return encryptedData;
        }
        private byte[] DecryptAssym(byte[] encryptedData, string privKey) // Função que decripta apartir da chave privada | Criptografia assimetrica
        {
            RSACryptoServiceProvider temp = new RSACryptoServiceProvider();
            temp.FromXmlString(privKey);
            byte[] data = temp.Decrypt(encryptedData, false);

            return data;
        }

        private bool VerifyLogin(string username, string password) // Verifica as credenciais recebida por parametro e acede à base de dados
        {
            SqlConnection conn = null;
            try
            {
                // Configurar ligação à Base de Dados
                conn = new SqlConnection();

                string path = Directory.GetCurrentDirectory();
                path = path.Remove(path.IndexOf("Servidor") + 9);
                conn.ConnectionString = String.Format(@"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename='" + path + "Database.mdf';Integrated Security=True");

                // Abrir ligação à Base de Dados
                conn.Open();

                // Declaração do comando SQL
                String sql = "SELECT * FROM Users WHERE Username = @username";
                SqlCommand cmd = new SqlCommand();
                cmd.CommandText = sql;

                // Declaração dos parâmetros do comando SQL
                SqlParameter param = new SqlParameter("@username", username);

                // Introduzir valor ao parâmentro registado no comando SQL
                cmd.Parameters.Add(param);

                // Associar ligação à Base de Dados ao comando a ser executado
                cmd.Connection = conn;

                // Executar comando SQL
                SqlDataReader reader = cmd.ExecuteReader();

                if (!reader.HasRows)
                {
                    throw new Exception("Error while trying to access an user");
                }

                // Ler resultado da pesquisa
                reader.Read();

                // Obter Hash (password + salt)
                byte[] saltedPasswordHashStored = (byte[])reader["SaltedPasswordHash"];

                // Obter salt
                byte[] saltStored = (byte[])reader["Salt"];

                conn.Close();

                //byte[] pass = Encoding.UTF8.GetBytes(password);

                byte[] hash = GenerateSaltedHash(password, saltStored);

                return saltedPasswordHashStored.SequenceEqual(hash);

                //TODO: verificar se a password na base de dados
                throw new NotImplementedException();
            }
            catch (Exception e)
            {
                return false;
            }
        }

        private bool Register(string username, byte[] saltedPasswordHash, byte[] salt) // Regista um novo utilizador e armazena a password encriptada na base de dados
        {
            SqlConnection conn = null;
            try
            {
                // Configurar ligação à Base de Dados
                conn = new SqlConnection();
                string path = Directory.GetCurrentDirectory();
                path = path.Remove(path.IndexOf("Servidor") + 9);
                conn.ConnectionString = String.Format(@"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename='" + path + "Database.mdf';Integrated Security=True");

                // Abrir ligação à Base de Dados
                conn.Open();

                // Verificação de se o username já existe 
                String sql = "SELECT * FROM Users WHERE Username = @username";
                SqlCommand cmd = new SqlCommand();
                cmd.CommandText = sql;

                // Declaração dos parâmetros do comando SQL
                SqlParameter param = new SqlParameter("@username", username);

                // Introduzir valor ao parâmentro registado no comando SQL
                cmd.Parameters.Add(param);

                // Associar ligação à Base de Dados ao comando a ser executado
                cmd.Connection = conn;

                // Executar comando SQL
                SqlDataReader reader = cmd.ExecuteReader();

                if (!reader.HasRows)
                {
                    // Declaração dos parâmetros do comando SQL
                    SqlParameter paramUsername = new SqlParameter("@username", username);
                    SqlParameter paramPassHash = new SqlParameter("@saltedPasswordHash", saltedPasswordHash);
                    SqlParameter paramSalt = new SqlParameter("@salt", salt);

                    // Declaração do comando SQL
                    sql = "INSERT INTO Users (Username, SaltedPasswordHash, Salt) VALUES (@username,@saltedPasswordHash,@salt)";

                    // Prepara comando SQL para ser executado na Base de Dados
                    cmd = new SqlCommand(sql, conn);

                    // Introduzir valores aos parâmentros registados no comando SQL
                    cmd.Parameters.Add(paramUsername);
                    cmd.Parameters.Add(paramPassHash);
                    cmd.Parameters.Add(paramSalt);

                    // Executar comando SQL
                    int lines = cmd.ExecuteNonQuery();

                    // Fechar ligação
                    conn.Close();
                    return true;
                    if (lines == 0)
                    {
                        // Se forem devolvidas 0 linhas alteradas então o não foi executado com sucesso
                        throw new Exception("Error while inserting an user");
                    }
                }
                else
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                throw new Exception("Error while inserting an user:" + e.Message);
            }
        }
        private static byte[] GenerateSalt(int size) // Criação de um salt aleatorio para a password
        {
            //Generate a cryptographic random number.
            RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
            byte[] buff = new byte[size];
            rng.GetBytes(buff);
            return buff;
        }
        private static byte[] GenerateSaltedHash(string plainText, byte[] salt) // Criação de um hash baseado no salt enviado por parametro
        {
            Rfc2898DeriveBytes rfc2898 = new Rfc2898DeriveBytes(plainText, salt, NUMBER_OF_ITERATIONS);
            return rfc2898.GetBytes(32);
        }
    }
}