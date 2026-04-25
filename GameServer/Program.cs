using GameServer.Core;

var serverId = args.Length > 0 ? args[0] : $"gs-{Guid.NewGuid():N}"[..8];
var gatewayHost = args.Length > 1 ? args[1] : "127.0.0.1";
var gatewayPort = args.Length > 2 && int.TryParse(args[2], out var parsedPort)
    ? parsedPort
    : 6000;

var server = new GameServerNode(serverId, gatewayHost, gatewayPort);
await server.StartAsync();
