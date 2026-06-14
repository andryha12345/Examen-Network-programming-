using System.Net.Sockets;
using System.Text;
using System.Windows;

namespace ChatClient;

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
           
            MessageBox.Show(response);

            if (response.StartsWith("Congratulations"))
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

    private void txtServerIp_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {

    }

    private void btnRegister_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(txtLogin.Text))
            {
                txtError.Text = "Enter username";
                return;
            }

            if (string.IsNullOrWhiteSpace(txtPassword.Password))
            {
                txtError.Text = "Enter password";
                return;
            }

            TcpClient client = new TcpClient("127.0.0.1", 5000);

            NetworkStream stream = client.GetStream();

            byte[] loginBytes =
                Encoding.UTF8.GetBytes(
                    $"REGISTER|{txtLogin.Text}");

            stream.Write(loginBytes, 0, loginBytes.Length);

            Thread.Sleep(50);

            byte[] passwordBytes =
                Encoding.UTF8.GetBytes(
                    txtPassword.Password);

            stream.Write(passwordBytes, 0, passwordBytes.Length);

            byte[] buffer = new byte[1024];

            int bytes = stream.Read(buffer);

            string response =
                Encoding.UTF8.GetString(
                    buffer,
                    0,
                    bytes);

            if (response.Contains("REGISTER_OK"))
            {
                txtError.Foreground =
                    System.Windows.Media.Brushes.LightGreen;

                txtError.Text =
                    "Registration successful";

                txtLogin.Clear();
                txtPassword.Clear();
            }
            else
            {
                txtError.Foreground =
                    System.Windows.Media.Brushes.IndianRed;

                txtError.Text =
                    "User already exists";
            }

            client.Close();
        }
        catch (Exception ex)
        {
            txtError.Foreground =
                System.Windows.Media.Brushes.IndianRed;

            txtError.Text = ex.Message;
        }
    }
}