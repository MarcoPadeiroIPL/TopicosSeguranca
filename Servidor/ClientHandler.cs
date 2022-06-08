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
        private User currUser;
        private const int SALTSIZE = 8;
        private const int NUMBER_OF_ITERATIONS = 50000;

        // para conveniencia
        private TcpClient currClient;
        private NetworkStream currStream;
        private ProtocolSI protocolSI;
        private string currUsername;

        // armazenamento da chave simetrica, publica e privada do servidor
        private Aes aes;
        private string serverPrivKey;
        private string serverPubKey;

        private string clientPubKey;

        private byte[] signature;

        internal ClientHandler(User currUser, byte[] symmKey, byte[] IV, string serverPrivKey, string serverPubKey)
        {
            this.currUser = currUser;
            this.currClient = currUser.GetClient();
            this.currStream = currUser.GetStream();
            this.protocolSI = new ProtocolSI();
            this.isLogged = currUser.GetLogin();
            this.currUsername = currUser.GetUsername();
            this.aes = Aes.Create();
            this.aes.Key = symmKey;
            this.aes.IV = IV;
            this.serverPrivKey = serverPrivKey;
            this.serverPubKey = serverPrivKey;
        }

        internal void Handle()
        {
            // Criação de Thread
            Thread thread = new Thread(threadHandler);
            thread.Start();
        }

        private void SendEncryptedAssym(byte[] data, string pubKey)
        {
            byte[] encryptedData = EncryptAssym(data, pubKey);
            byte[] package = protocolSI.Make(ProtocolSICmdType.ASSYM_CIPHER_DATA, encryptedData);
            currStream.Write(package, 0, package.Length);
            currStream.Flush();
        }
        private void SendEncryptedSym(byte[] data, byte[] symmKey, byte[] IV)
        {
            byte[] encryptedData = EncryptSymm(data, symmKey, IV);
            byte[] package = protocolSI.Make(ProtocolSICmdType.SYM_CIPHER_DATA, encryptedData);
            currStream.Write(package, 0, package.Length);
            currStream.Flush();
        }
        private byte[] EncryptAssym(byte[] data, string pubKey)
        {
            byte[] encryptedData;

            RSACryptoServiceProvider temp = new RSACryptoServiceProvider();
            temp.FromXmlString(pubKey);
            encryptedData = temp.Encrypt(data, false);

            return encryptedData;
        }
        private byte[] DecryptAssym(byte[] encryptedData, string privKey)
        {
            RSACryptoServiceProvider temp = new RSACryptoServiceProvider();
            temp.FromXmlString(privKey);
            byte[] data = temp.Decrypt(encryptedData, false);

            return data;
        }
        private byte[] EncryptSymm(byte[] data, byte[] symmKey, byte[] IV)
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
        private byte[] DecryptSymm(byte[] encryptedData, byte[] symmKey, byte[] IV)
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
        private void threadHandler()
        {
            Program.WriteToLog(DateTime.Now.ToString("[HH:mm]") + " Alguém está a tentar entrar...");

            // mal o cliente entra, envia a chave publica ao cliente
            byte[] package = protocolSI.Make(ProtocolSICmdType.PUBLIC_KEY, serverPubKey);
            currStream.Write(package, 0, package.Length);

            // Enquanto a transmissão com o cliente não acabar
            while (currStream.CanRead)
            {
                if (currStream.DataAvailable)
                {
                    // lê a informação da possivel mensagem enviada pelo cliente
                    currStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);
                    switch (protocolSI.GetCmdType())
                    {
                        case ProtocolSICmdType.PUBLIC_KEY:
                            clientPubKey = protocolSI.GetStringFromData();
                            break;
                        case ProtocolSICmdType.DIGITAL_SIGNATURE:
                            signature = protocolSI.GetData();
                            Console.WriteLine(System.Text.Encoding.UTF8.GetString(signature));
                            break;
                        case ProtocolSICmdType.USER_OPTION_1:
                            if (!currUser.GetLogin())
                            {
                                string userPass = System.Text.Encoding.UTF8.GetString(DecryptAssym(protocolSI.GetData(), serverPrivKey));
                                currUsername = userPass.Substring(0, userPass.IndexOf('/'));
                                string password = userPass.Substring(userPass.IndexOf('/') + 1, userPass.Length - currUsername.Length - 1);
                                if(VerifyLogin(currUsername, password))
                                {
                                    currUser.ChangeLogin(true);
                                    isLogged = true;

                                    currUser.ChangeUsername(currUsername);

                                    // envia uma mensagem ao cliente a avisar que o login é valido
                                    package = protocolSI.Make(ProtocolSICmdType.SECRET_KEY, EncryptAssym(aes.Key, clientPubKey));
                                    currStream.Write(package, 0, package.Length);
                                    currStream.Flush();


                                    // envia uma mensagem a todos os outros clientes a avisar que alguem entrou no chat
                                    string mensagem = DateTime.Now.ToString("[HH:mm]") + " " + currUsername + " entrou no chat!";
                                    Program.WriteToLog(mensagem);

                                    package = protocolSI.Make(ProtocolSICmdType.DATA, mensagem);
                                    Program.SendToEveryone(package);
                                } else
                                {
                                    package = protocolSI.Make(ProtocolSICmdType.NACK);
                                    currStream.Write(package, 0, package.Length);
                                    currStream.Flush();
                                }
                            }
                            break;
                        case ProtocolSICmdType.USER_OPTION_2: //utilizador registar
                            // obtem o username e password enviada pelo cliente
                            string userPas = System.Text.Encoding.UTF8.GetString(DecryptAssym(protocolSI.GetData(), serverPrivKey));
                            string username = userPas.Substring(0, userPas.IndexOf('/'));
                            string pass = userPas.Substring(userPas.IndexOf('/') + 1, userPas.Length - username.Length - 1);

                            byte[] salt = GenerateSalt(SALTSIZE);
                            byte[] hash = GenerateSaltedHash(pass, salt);

                            // regista o cliente e verifica se foi efetuado com succeso ou não
                            if (Register(username, hash, salt))
                            {
                                package = protocolSI.Make(ProtocolSICmdType.ACK);
                            } else {
                                package = protocolSI.Make(ProtocolSICmdType.NACK);
                            }
                            currStream.Write(package, 0, package.Length);
                            currStream.Flush();
                            break;
                        case ProtocolSICmdType.IV:
                            package = protocolSI.Make(ProtocolSICmdType.IV, protocolSI.GetData());
                            Program.SendToEveryone(package);
                            break;
                        // caso a informação recebida seja uma mensagem
                        case ProtocolSICmdType.SYM_CIPHER_DATA:
                            if (currUser.GetLogin())
                            {
                                Console.WriteLine(currUsername + " enviou uma mensagem.");
                                // assinar o hash
                                RSACryptoServiceProvider rsa1 = new RSACryptoServiceProvider();
                                rsa1.FromXmlString(serverPrivKey);
                                byte[] signature = rsa1.SignData(protocolSI.GetData(), CryptoConfig.MapNameToOID("SHA1"));

                                // envia a todos os clientes
                                package = protocolSI.Make(ProtocolSICmdType.DIGITAL_SIGNATURE, signature);
                                Program.SendToEveryone(package);
                                package = protocolSI.Make(ProtocolSICmdType.DATA, DateTime.Now.ToString("[HH:mm]") + " " + currUsername + ": ");
                                Program.SendToEveryone(package);
                                package = protocolSI.Make(ProtocolSICmdType.SYM_CIPHER_DATA, protocolSI.GetData());
                                Program.SendToEveryone(package);
                                
                            } else
                            {
                                // erro, não está logado
                            }
                            break;
                        // caso a informação recebida tenha sido de fim de transmissão
                        case ProtocolSICmdType.EOT:
                            string mesg = DateTime.Now.ToString("[HH:mm]") + " " + currUsername + " saiu do chat.";
                            Program.WriteToLog(mesg);
                            package = protocolSI.Make(ProtocolSICmdType.USER_OPTION_2, currUsername);
                            Globals.users.Remove(currUser);
                            Program.SendToEveryone(package);

                            currStream.Close();
                            currClient.Close();
                            break;
                    }
                }
        }
        // encerramento de ligação com o cliente
        currStream.Close();
        currClient.Close();
    }
        private static byte[] GenerateSalt(int size)
        {
            //Generate a cryptographic random number.
            RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
            byte[] buff = new byte[size];
            rng.GetBytes(buff);
            return buff;
        }
        private bool VerifyLogin(string username, string password)
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
        private static byte[] GenerateSaltedHash(string plainText, byte[] salt)
        {
            Rfc2898DeriveBytes rfc2898 = new Rfc2898DeriveBytes(plainText, salt, NUMBER_OF_ITERATIONS);
            return rfc2898.GetBytes(32);
        }
        private bool Register(string username, byte[] saltedPasswordHash, byte[] salt)
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
                } else
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                throw new Exception("Error while inserting an user:" + e.Message);
            }
        }
        
    }
}