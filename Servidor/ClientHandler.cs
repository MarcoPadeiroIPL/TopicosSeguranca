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
        private bool isLogged;
        private string currUsername;
        internal ClientHandler(User currUser)
        {
            this.currUser = currUser;
            this.currClient = currUser.GetClient();
            this.currStream = currUser.GetStream();
            this.protocolSI = new ProtocolSI();
            this.isLogged = currUser.GetLogin();
            this.currUsername = currUser.GetUsername();
        }

        internal void Handle()
        {
            // Criação de Thread
            Thread thread = new Thread(threadHandler);
            thread.Start();
        }

        private void threadHandler()
        {
            Program.WriteToLog(DateTime.Now.ToString("[HH:mm]") + " Alguém está a tentar entrar...");
            // Enquanto a transmissão com o cliente não acabar
            while(protocolSI.GetCmdType() != ProtocolSICmdType.EOT)
            {
                try
                {
                    // lê a informação da possivel mensagem enviada pelo cliente
                    currStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);
                }
                catch { }

                // criação um pacote responsavel por armazenar a mensagem a ser enviada pelo servidor 
                byte[] package;


                switch(protocolSI.GetCmdType())
                {
                    // caso esteja a tentar fazer o login
                    case ProtocolSICmdType.USER_OPTION_1:
                        if (!currUser.GetLogin()) // se não estiver logado
                        {
                            // obtem o username e password
                            string userPass = protocolSI.GetStringFromData();
                            currUsername = userPass.Substring(0, userPass.IndexOf('/'));
                            string password = userPass.Substring(userPass.IndexOf('/') + 1, userPass.Length - currUsername.Length - 1);
                   
                            // verifica se são validos
                            if (VerifyLogin(currUsername, password)) {
                                // altera o atribuito do cliente para o codigo reconhecer que está logado
                                currUser.ChangeLogin(true);
                                isLogged = true;

                                currUser.ChangeUsername(currUsername);

                                // envia uma mensagem ao cliente a avisar que o login é valido
                                package = protocolSI.Make(ProtocolSICmdType.ACK);
                                currStream.Write(package, 0, package.Length);
                                currStream.Flush();
                                
                                // envia uma mensagem a todos os outros clientes a avisar que alguem entrou no chat
                                string mensagem = DateTime.Now.ToString("[HH:mm]") + " " + currUsername + " entrou no chat!";
                                Program.WriteToLog(mensagem);

                                package = protocolSI.Make(ProtocolSICmdType.USER_OPTION_1, currUsername);
                                Program.SendToEveryone(package);

                            } 
                            else { // credenciais invalidas 
                                package = protocolSI.Make(ProtocolSICmdType.NACK);
                                currStream.Write(package, 0, package.Length);
                                currStream.Flush();
                            }

                        }
                        else
                        {
                            // erro, já está logado 
                        }
                        break;

                    case ProtocolSICmdType.USER_OPTION_2: //utilizador registar
                        // obtem o username e password enviada pelo cliente
                        string userPas = protocolSI.GetStringFromData();
                        string username = userPas.Substring(0, userPas.IndexOf('/'));
                        string pass = userPas.Substring(userPas.IndexOf('/') + 1, userPas.Length - username.Length - 1);

                        byte[] salt = GenerateSalt(SALTSIZE);
                        byte[] hash = GenerateSaltedHash(pass, salt);

                        // regista o cliente e verifica se foi efetuado com succeso ou não
                        if (Register(username, hash, salt))
                        {
                            package = protocolSI.Make(ProtocolSICmdType.ACK);
                        } else
                        {
                            package = protocolSI.Make(ProtocolSICmdType.NACK);
                        }
                        currStream.Write(package, 0, package.Length);
                        currStream.Flush();
                        break;

                    // caso a informação recebida seja uma mensagem
                    case ProtocolSICmdType.DATA:
                        if (currUser.GetLogin())
                        {
                            string msg = DateTime.Now.ToString("[HH:mm]") + " " + currUsername + ": " + protocolSI.GetStringFromData();
                            Program.WriteToLog(msg);

                            // envia a todos os clientes
                            package = protocolSI.Make(ProtocolSICmdType.DATA, msg);
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