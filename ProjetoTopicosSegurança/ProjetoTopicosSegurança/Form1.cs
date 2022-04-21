namespace ProjetoTopicosSegurança
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void buttonClose_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBoxMensagem_Enter(object sender, EventArgs e)
        {
            if(textBoxMensagem.Text == "Escreva uma mensagem!")
            {
                textBoxMensagem.Text = "";
               
            }
        }

        private void textBoxMensagem_Leave(object sender, EventArgs e)
        {
            if (textBoxMensagem.Text == "")
            {
                textBoxMensagem.Text = "Escreva uma mensagem!";
                
            }
        }
    }
}