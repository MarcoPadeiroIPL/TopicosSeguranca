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

        public User(TcpClient client, bool isLoggedIn = false){
            this.client = client;
            this.isLogged = isLoggedIn;
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
