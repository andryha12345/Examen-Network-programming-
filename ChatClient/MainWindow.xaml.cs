using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

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
        _username = username;
        Title = $"Chat - {username}";

        txtMessage.Text = "/msg name text — private message";
        txtMessage.GotFocus += TxtMessage_GotFocus;
        txtMessage.LostFocus += TxtMessage_LostFocus;

        Task.Run(async () =>
        {
            try { await ListenForMessages(); }
            catch (Exception ex) { Dispatcher.Invoke(() => MessageBox.Show("Listen error: " + ex.Message)); }
        });
    }

    private async Task ListenForMessages()
    {
        byte[] buffer = new byte[4096];
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
                    Dispatcher.Invoke(() =>
                    {
                        // Handle delete
                        if (line.StartsWith("DEL|"))
                        {
                            int msgId = int.Parse(line.Substring(4));
                            for (int i = 0; i < listMessages.Items.Count; i++)
                            {
                                var item = listMessages.Items[i] as ListBoxItem;
                                if (item != null && item.Tag != null && (int)item.Tag == msgId)
                                {
                                    listMessages.Items.RemoveAt(i);
                                    break;
                                }
                            }
                            return;
                        }

                        // Handle normal message
                        if (line.StartsWith("MSG|"))
                        {
                            var parts = line.Split('|');
                            int msgId = int.Parse(parts[1]);
                            string sender = parts[2];
                            string text = parts[3];

                            bool isMyMessage = (sender == _username);

                            var panel = new StackPanel();
                            panel.Orientation = Orientation.Horizontal;
                            panel.Margin = new Thickness(5);

                            var icon = new TextBlock();
                            icon.Text = "👤";
                            icon.FontSize = 16;
                            icon.Margin = new Thickness(0, 0, 10, 0);
                            icon.VerticalAlignment = VerticalAlignment.Center;

                            var name = new TextBlock();
                            name.Text = $"{sender}:";
                            name.FontWeight = FontWeights.Bold;
                            name.Foreground = new SolidColorBrush(Colors.Blue);
                            name.FontSize = 14;
                            name.Margin = new Thickness(0, 0, 8, 0);
                            name.VerticalAlignment = VerticalAlignment.Center;

                            var messageText = new TextBlock();
                            messageText.Text = text;
                            messageText.FontSize = 14;
                            messageText.TextWrapping = TextWrapping.Wrap;
                            messageText.VerticalAlignment = VerticalAlignment.Center;

                            panel.Children.Add(icon);
                            panel.Children.Add(name);
                            panel.Children.Add(messageText);

                            var item = new ListBoxItem();
                            item.Content = panel;
                            item.Tag = msgId;

                            if (isMyMessage)
                                item.HorizontalContentAlignment = HorizontalAlignment.Right;
                            else
                                item.HorizontalContentAlignment = HorizontalAlignment.Left;

                            listMessages.Items.Add(item);
                            listMessages.ScrollIntoView(item);
                        }
                        else if (line.Contains("[PM from") || line.Contains("[PM to"))
                        {
                            var msg = new TextBlock();
                            msg.Text = $"[{DateTime.Now:HH:mm}] {line}";
                            msg.Foreground = new SolidColorBrush(Colors.Orange);
                            msg.FontSize = 12;
                            msg.Margin = new Thickness(5);
                            listMessages.Items.Add(msg);
                        }
                    });
                }
            }
        }
    }

    private async void btnSend_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtMessage.Text)) return;
        string msg = txtMessage.Text;
        await _stream.WriteAsync(Encoding.UTF8.GetBytes(msg + "\n"));
        txtMessage.Text = "";
    }

    private void txtMessage_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) btnSend_Click(sender, e);
    }

    private void TxtMessage_GotFocus(object sender, RoutedEventArgs e)
    {
        if (txtMessage.Text == "/msg name text — private message")
        {
            txtMessage.Text = "";
            txtMessage.Foreground = Brushes.Black;
        }
    }

    private void TxtMessage_LostFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtMessage.Text))
        {
            txtMessage.Text = "/msg name text — private message";
            txtMessage.Foreground = Brushes.Gray;
        }
    }

    private async void btnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (listMessages.SelectedItem == null)
        {
            MessageBox.Show("Select a message to delete!");
            return;
        }

        var selectedItem = listMessages.SelectedItem as ListBoxItem;
        if (selectedItem.Tag != null && (int)selectedItem.Tag > 0)
        {
            int msgId = (int)selectedItem.Tag;
            await _stream.WriteAsync(Encoding.UTF8.GetBytes($"/del {msgId}\n"));
        }
        else
        {
            MessageBox.Show("You cannot delete this message!");
        }
    }
}