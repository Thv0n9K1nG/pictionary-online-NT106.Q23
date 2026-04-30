using System.Net.Security;
using System.Text;
using System.Text.Json;
using System.IO;
using Shared.Models;

namespace Client.Services;

public sealed class SocketService
{
    private Stream? _stream;
    private StreamReader? _reader;
    private CancellationTokenSource? _receiveCts;

    public event EventHandler<GameMessage>? MessageReceived;

    public bool IsConnected => _stream is not null;

    public async Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        var factory = new Client.Security.TlsClientFactory();
        _stream = await factory.ConnectAsync(host, port, cancellationToken);
        
        // Khởi tạo bộ đọc hỗ trợ UTF-8
        _reader = new StreamReader(_stream, Encoding.UTF8);
        _receiveCts = new CancellationTokenSource();

        // Kích hoạt vòng lặp chạy ngầm lắng nghe dữ liệu từ Gateway
        _ = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token));
    }

    public async Task SendAsync(GameMessage message, CancellationToken cancellationToken = default)
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("Client is not connected to Gateway.");
        }

        var bytes = Encoding.UTF8.GetBytes(message.ToJsonLine());
        await _stream.WriteAsync(bytes, cancellationToken);
        await _stream.FlushAsync(cancellationToken);
    }

    // Logic lắng nghe và phân tách dữ liệu theo dòng (\n)
    private async Task ReceiveLoopAsync(CancellationToken token)
    {
        if (_reader is null) return;

        try
        {
            while (!token.IsCancellationRequested)
            {
                var line = await _reader.ReadLineAsync(token);
                if (string.IsNullOrEmpty(line))
                    break; // Mất kết nối từ server

                try
                {
                    // Chuyển đổi chuỗi JSON nhận được về đối tượng GameMessage
                    var message = JsonSerializer.Deserialize<GameMessage>(line, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    if (message != null)
                    {
                        MessageReceived?.Invoke(this, message);
                    }
                }
                catch
                {
                    // Bỏ qua lỗi nếu JSON không hợp lệ để không làm sập vòng lặp
                }
            }
        }
        catch
        {
            // Xử lý khi ngắt kết nối đột ngột
        }
        finally
        {
            _stream?.Close();
            _stream = null;
        }
    }

    public void RaiseMessageForTest(GameMessage message)
    {
        MessageReceived?.Invoke(this, message);
    }
}
