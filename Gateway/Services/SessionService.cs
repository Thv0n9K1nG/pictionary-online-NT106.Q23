namespace Gateway.Services;

public sealed class SessionService
{
    public string CreateSession(string userId)
    {
        return $"{userId}:{Guid.NewGuid():N}";
    }
}
