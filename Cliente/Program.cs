using System;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using EI.SI;
namespace ProjetoTopicosSegurança
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new Form1());
        }
    }
}