using Gateway.Core;

var clientPort = args.Length > 0 && int.TryParse(args[0], out var parsedClientPort)
    ? parsedClientPort
    : 5000;

var gameServerPort = args.Length > 1 && int.TryParse(args[1], out var parsedGameServerPort)
    ? parsedGameServerPort
    : 6000;

using var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    Console.WriteLine("Gateway shutdown requested.");
    cts.Cancel();
};

var server = new GatewayServer(clientPort, gameServerPort);

try
{
    await server.StartAsync(cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Gateway stopped.");
}