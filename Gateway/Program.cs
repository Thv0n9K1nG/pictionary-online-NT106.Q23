using Gateway.Core;

var port = args.Length > 0 && int.TryParse(args[0], out var parsedPort)
    ? parsedPort
    : 5000;

var server = new GatewayServer(port);

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    Console.WriteLine("Gateway shutdown requested.");
};

await server.StartAsync();
