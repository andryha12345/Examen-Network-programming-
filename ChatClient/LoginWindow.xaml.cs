using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ChatClient
{
    /// <summary>
    /// Interaction logic for LoginWindow.xaml
    /// </summary>
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
        }

        private async void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            string login = txtLogin.Text;
            string password = txtPassword.Password;

            try
            {
                TcpClient client = new TcpClient();
                await client.ConnectAsync("127.0.0.1", 5000);
                NetworkStream stream = client.GetStream();

                stream.Write(Encoding.UTF8.GetBytes(login));
                stream.Write(Encoding.UTF8.GetBytes(password));

                byte[] buffer = new byte[1024];
                int bytes = await stream.ReadAsync(buffer);
                string response = Encoding.UTF8.GetString(buffer, 0, bytes);

                if (response == "Congratulations")
                {
                    MainWindow main = new MainWindow(client, stream, login);
                    main.Show();
                    this.Close();
                }
                else
                {
                    txtError.Text = "Invalid login or password";
                }
            }
            catch (Exception ex)
            {
                txtError.Text = $"Error: {ex.Message}";
            }
        }
    }
}
