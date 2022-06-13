using EI.SI;
using System;
using System.Net.Sockets;
using System.Threading;

namespace Servidor
{
    internal class User
    {
        private TcpClient client;
        private bool isLogged;
        private string username;
        public int id;

        public User(TcpClient client, bool isLoggedIn = false, int id = 0)
        {
            this.client = client;
            this.isLogged = isLoggedIn;
            this.id = id;
        }

        public TcpClient GetClient()
        {
            return this.client;
        }
        public NetworkStream GetStream()
        {
            return this.client.GetStream();
        }
        public bool GetLogin()
        {
            return this.isLogged;
        }
        public void ChangeLogin(bool value)
        {
            this.isLogged = value;
        }
        public string GetUsername()
        {
            return username;
        }
        public void ChangeUsername(string newUsername)
        {
            this.username = newUsername;
        }
    }
}
