namespace GameServer.Security;

public sealed class InternalNodeAuthorizer
{
    public bool IsAuthorizedGateway(string token)
    {
        // TODO: Validate internal shared secret or node token.
        return !string.IsNullOrWhiteSpace(token);
    }
}
