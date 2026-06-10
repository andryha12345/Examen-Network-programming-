using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace ChatClient;

public partial class MainWindow : Window
{
    private TcpClient _client;
    private NetworkStream _stream;

    public MainWindow(TcpClient client, NetworkStream stream, string username)
    {
        InitializeComponent();
        _client = client;
        _stream = stream;
        Title = $"Chat - {username}";
        Task.Run(ListenForMessages);
    }

    private async Task ListenForMessages()
    {
        byte[] buffer = new byte[4096];
        while (true)
        {
            int bytes = await _stream.ReadAsync(buffer);
            if (bytes == 0) break;
            string msg = Encoding.UTF8.GetString(buffer, 0, bytes);
            Dispatcher.Invoke(() => listMessages.Items.Add(msg));
        }
    }

    private async void btnSend_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtMessage.Text)) return;
        await _stream.WriteAsync(Encoding.UTF8.GetBytes(txtMessage.Text));
        txtMessage.Text = "";
    }

    private void txtMessage_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) btnSend_Click(sender, e);
    }
}