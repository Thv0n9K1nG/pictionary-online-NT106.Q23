using System.Net.Security;
using System.Text;
using Shared.Models;

namespace Client.Services;

public sealed class SocketService
{
    private Stream? _stream;

    public event EventHandler<GameMessage>? MessageReceived;

    public bool IsConnected => _stream is not null;

    public async Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        var factory = new Client.Security.TlsClientFactory();
        _stream = await factory.ConnectAsync(host, port, cancellationToken);
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

    public void RaiseMessageForTest(GameMessage message)
    {
        MessageReceived?.Invoke(this, message);
    }
}

