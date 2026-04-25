using Gateway.Data;
using Gateway.Security;

namespace Gateway.Services;

public sealed class AuthService
{
    private readonly UserRepository _userRepository;
    private readonly SessionService _sessionService;

    public AuthService(UserRepository userRepository, SessionService sessionService)
    {
        _userRepository = userRepository;
        _sessionService = sessionService;
    }

    public async Task<string> RegisterAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var userId = Guid.NewGuid().ToString("N");
        var passwordHash = PasswordHasher.Hash(password);

        await _userRepository.CreateUserAsync(userId, username, passwordHash, cancellationToken);
        return userId;
    }

    public async Task<string?> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.FindUserByUsernameAsync(username, cancellationToken);
        if (user is null)
        {
            return null;
        }

        return PasswordHasher.Verify(password, user.Value.PasswordHash)
            ? _sessionService.CreateSession(user.Value.UserId)
            : null;
    }
}
