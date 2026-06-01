using System.IO;
using System.Text;
using System.Text.Json;
using Shared.Enums;
using Shared.Models;

namespace Client.Services;

public sealed class SocketService
{
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private Stream? _stream;
    private StreamReader? _reader;
    private CancellationTokenSource? _receiveCts;
    private int _connectionVersion;

    public event EventHandler<GameMessage>? MessageReceived;
    public event EventHandler<string>? ReceiveError;
    public event EventHandler? ConnectionClosed;

    public bool IsConnected => _stream is not null;

    public void Disconnect()
    {
        CloseCurrentConnection();
    }

    public async Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        await _connectLock.WaitAsync(cancellationToken);
        try
        {
            CloseCurrentConnection();

            var factory = new Client.Security.TlsClientFactory();
            var stream = await factory.ConnectAsync(host, port, cancellationToken);
            var reader = new StreamReader(stream, Encoding.UTF8);
            var receiveCts = new CancellationTokenSource();
            var version = Interlocked.Increment(ref _connectionVersion);

            _stream = stream;
            _reader = reader;
            _receiveCts = receiveCts;

            _ = Task.Run(() => ReceiveLoopAsync(stream, reader, version, receiveCts.Token));
        }
        finally
        {
            _connectLock.Release();
        }
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

    private async Task ReceiveLoopAsync(Stream stream, StreamReader reader, int version, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(token);
                if (line is null)
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    var message = ParseGameMessage(line);
                    if (message is not null)
                    {
                        RaiseMessageReceived(message);
                    }
                }
                catch (Exception ex)
                {
                    ReceiveError?.Invoke(this, $"Invalid message from Gateway: {ex.Message}");
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            ReceiveError?.Invoke(this, $"Connection receive error: {ex.Message}");
        }
        finally
        {
            var isCurrentConnection = version == Volatile.Read(ref _connectionVersion) &&
                ReferenceEquals(stream, _stream);

            stream.Close();

            if (isCurrentConnection)
            {
                _stream = null;
                _reader = null;
                _receiveCts = null;
                ConnectionClosed?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public void RaiseMessageForTest(GameMessage message)
    {
        RaiseMessageReceived(message);
    }

    private void RaiseMessageReceived(GameMessage message)
    {
        var handlers = MessageReceived;
        if (handlers is null)
        {
            return;
        }

        foreach (EventHandler<GameMessage> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(this, message);
            }
            catch (Exception ex)
            {
                ReceiveError?.Invoke(this, $"Client message handler failed: {ex.Message}");
            }
        }
    }

    private static GameMessage? ParseGameMessage(string line)
    {
        try
        {
            var message = GameMessage.FromJson(line);
            if (message is not null && message.Type != MessageType.Unknown)
            {
                return message;
            }
        }
        catch
        {
            // Fall through to the tolerant parser below.
        }

        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;

        if (!TryGetPropertyIgnoreCase(root, "type", out var typeElement))
        {
            throw new InvalidOperationException("Missing 'type' property.");
        }

        object? payload = null;
        if (TryGetPropertyIgnoreCase(root, "payload", out var payloadElement))
        {
            payload = payloadElement.Clone();
        }

        string? senderId = null;
        if (TryGetPropertyIgnoreCase(root, "senderId", out var senderElement))
        {
            senderId = senderElement.ValueKind == JsonValueKind.String
                ? senderElement.GetString()
                : senderElement.ToString();
        }

        return new GameMessage
        {
            Type = ParseMessageType(typeElement),
            Payload = payload,
            SenderId = senderId,
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    private static MessageType ParseMessageType(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var number))
        {
            return Enum.IsDefined(typeof(MessageType), number) ? (MessageType)number : MessageType.Unknown;
        }

        var raw = element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return MessageType.Unknown;
        }

        if (Enum.TryParse<MessageType>(raw, ignoreCase: true, out var direct))
        {
            return direct;
        }

        var normalized = NormalizeToken(raw);
        foreach (var value in Enum.GetValues<MessageType>())
        {
            if (NormalizeToken(value.ToString()) == normalized)
            {
                return value;
            }
        }

        return MessageType.Unknown;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static string NormalizeToken(string value)
    {
        var chars = value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray();
        return new string(chars);
    }

    private void CloseCurrentConnection()
    {
        var cts = _receiveCts;
        _receiveCts = null;
        cts?.Cancel();
        cts?.Dispose();

        _reader = null;
        _stream?.Close();
        _stream = null;
    }
}
