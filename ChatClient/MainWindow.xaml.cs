using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;


namespace ChatClient;

public partial class MainWindow : Window
{
    private TcpClient _client;
    private NetworkStream _stream;
    private string _username;

    public MainWindow(TcpClient client, NetworkStream stream, string username)
    {
        InitializeComponent();
        _client = client;
        _stream = stream;
        //
        _username = username;
        //
        Title = $"Chat - {username}";
        //Task.Run(ListenForMessages);
        Task.Run(async () =>
        {
            try { await ListenForMessages(); }
            catch (Exception ex) { Dispatcher.Invoke(() => MessageBox.Show("Listen error: " + ex.Message)); }
        });
    }

    private async Task ListenForMessages()
    {
        byte[] buffer = new byte[4096];
        //while (true)
        //{
        //    int bytes = await _stream.ReadAsync(buffer);
        //    if (bytes == 0) break;
        //    string msg = Encoding.UTF8.GetString(buffer, 0, bytes);
        //    Dispatcher.Invoke(() => listMessages.Items.Add($"[{DateTime.Now:HH:mm}] {_username} = {msg}"));
        //}
        var sb = new StringBuilder();

        while (true)
        {
            int bytes = await _stream.ReadAsync(buffer);
            if (bytes == 0) break;
            sb.Append(Encoding.UTF8.GetString(buffer, 0, bytes));

            int newlineIndex;
            while ((newlineIndex = sb.ToString().IndexOf('\n')) >= 0)
            {
                string line = sb.ToString(0, newlineIndex);
                sb.Remove(0, newlineIndex + 1);

                if (!string.IsNullOrWhiteSpace(line))
                {
                    
                    Dispatcher.Invoke(() => listMessages.Items.Add($"[{DateTime.Now:HH:mm}] = {line}"));
                }
            }
        }
    }

    private async void btnSend_Click(object sender, RoutedEventArgs e)
    {
        //if (string.IsNullOrWhiteSpace(txtMessage.Text)) return;
        //await _stream.WriteAsync(Encoding.UTF8.GetBytes(txtMessage.Text));
        //txtMessage.Text = "";
        //
        if (string.IsNullOrWhiteSpace(txtMessage.Text)) return;
        string msg = txtMessage.Text;
        await _stream.WriteAsync(Encoding.UTF8.GetBytes(msg));
        listMessages.Items.Add($"[{DateTime.Now:HH:mm}] {_username} = {msg}");
        txtMessage.Text = "";
        // так напевно краще але якщо що сорі я не видаляв тввою частину :)


    }

    private void txtMessage_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) btnSend_Click(sender, e);
    }
}