using Client.State;
using Shared.Enums;
using Shared.Models;

namespace Client.Services;

public sealed class ReconnectService : IDisposable
{
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(3),
        TimeSpan.FromSeconds(5)
    ];

    private readonly SocketService _socketService;
    private readonly object _sync = new();
    private CancellationTokenSource? _connectionLoopCts;
    private ClientState? _state;
    private bool _autoReconnectEnabled;
    private bool _disposed;

    public event Action? OnRoomStateRestored;
    public event Action<TimeSpan, Exception>? ConnectionRetryScheduled;
    public event Action<string>? ConnectionStatusChanged;

    public ReconnectService(SocketService socketService)
    {
        _socketService = socketService;
        _socketService.MessageReceived += OnMessageReceived;
        _socketService.ConnectionClosed += OnConnectionClosed;
    }

    public void StartGatewayConnectionLoop(ClientState state, bool forceReconnect = false)
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _state = state;
            _autoReconnectEnabled = true;
            _connectionLoopCts?.Cancel();
            _connectionLoopCts?.Dispose();
            _connectionLoopCts = new CancellationTokenSource();

            if (forceReconnect)
            {
                _socketService.Disconnect();
            }

            _ = Task.Run(() => RunConnectionLoopAsync(state, _connectionLoopCts.Token));
        }
    }

    public void StopGatewayConnectionLoop()
    {
        lock (_sync)
        {
            _autoReconnectEnabled = false;
            _connectionLoopCts?.Cancel();
            _connectionLoopCts?.Dispose();
            _connectionLoopCts = null;
        }
    }

    public async Task TryReconnectAsync(ClientState state, CancellationToken cancellationToken = default)
    {
        _state = state;

        if (!_socketService.IsConnected)
        {
            await _socketService.ConnectAsync(state.GatewayHost, state.GatewayPort, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(state.SessionId))
        {
            return;
        }

        await SendReconnectRequestAsync(state.SessionId, cancellationToken);
    }

    private async Task RunConnectionLoopAsync(ClientState state, CancellationToken cancellationToken)
    {
        var attempt = 0;

        while (!cancellationToken.IsCancellationRequested && !_socketService.IsConnected)
        {
            var endpoint = $"{state.GatewayHost}:{state.GatewayPort}";
            var connectingState = attempt == 0
                ? ClientConnectionState.Connecting
                : ClientConnectionState.Reconnecting;

            state.SetConnectionState(connectingState);
            ConnectionStatusChanged?.Invoke($"Connecting to Gateway {endpoint}...");

            try
            {
                await _socketService.ConnectAsync(state.GatewayHost, state.GatewayPort, cancellationToken);
                state.SetConnectionState(ClientConnectionState.Connected);
                ConnectionStatusChanged?.Invoke($"Connected to Gateway {endpoint}.");

                if (!string.IsNullOrWhiteSpace(state.SessionId))
                {
                    await SendReconnectRequestAsync(state.SessionId, cancellationToken);
                }

                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                state.LastErrorMessage = ex.Message;
                state.SetConnectionState(ClientConnectionState.Reconnecting);

                var delay = RetryDelays[Math.Min(attempt, RetryDelays.Length - 1)];
                ConnectionRetryScheduled?.Invoke(delay, ex);
                ConnectionStatusChanged?.Invoke($"Gateway unavailable, retrying in {(int)delay.TotalSeconds}s...");

                try
                {
                    await Task.Delay(delay, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                attempt++;
            }
        }
    }

    private async Task SendReconnectRequestAsync(string sessionId, CancellationToken cancellationToken)
    {
        var reconnectMessage = new GameMessage
        {
            Type = MessageType.Reconnect,
            Payload = new { sessionId }
        };

        await _socketService.SendAsync(reconnectMessage, cancellationToken);
    }

    private void OnConnectionClosed(object? sender, EventArgs args)
    {
        ClientState? state;
        lock (_sync)
        {
            if (!_autoReconnectEnabled || _disposed)
            {
                return;
            }

            state = _state;
        }

        if (state is null)
        {
            return;
        }

        state.SetConnectionState(ClientConnectionState.Disconnected);
        StartGatewayConnectionLoop(state);
    }

    private void OnMessageReceived(object? sender, GameMessage message)
    {
        if (message.Type == MessageType.RoomRecovered)
        {
            OnRoomStateRestored?.Invoke();
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _autoReconnectEnabled = false;
            _connectionLoopCts?.Cancel();
            _connectionLoopCts?.Dispose();
            _connectionLoopCts = null;
        }

        _socketService.MessageReceived -= OnMessageReceived;
        _socketService.ConnectionClosed -= OnConnectionClosed;
    }
}
