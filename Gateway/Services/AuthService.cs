using Gateway.Data;
using Gateway.Security;

namespace Gateway.Services;

/// <summary>
/// Dịch vụ xử lý xác thực người dùng tại Gateway.
/// Chịu trách nhiệm điều phối giữa PasswordHasher và UserRepository.
/// </summary>
public sealed class AuthService
{
    private readonly UserRepository _userRepository;
    private readonly SessionService _sessionService;

    public AuthService(UserRepository userRepository, SessionService sessionService)
    {
        _userRepository = userRepository;
        _sessionService = sessionService;
    }

    /// <summary>
    /// Thực hiện đăng ký tài khoản mới.
    /// </summary>
    public async Task<string> RegisterAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        // 1. Tạo UserId định danh duy nhất (32 ký tự hex)
        var userId = Guid.NewGuid().ToString("N");

        // 2. Băm mật khẩu kèm Salt bằng BCrypt để lưu trữ an toàn
        var passwordHash = PasswordHasher.Hash(password);

        // 3. Ghi dữ liệu vào database (bao gồm bảng Users và khởi tạo PlayerStats mặc định)
        await _userRepository.CreateUserAsync(userId, username, passwordHash, cancellationToken);

        return userId;
    }

    /// <summary>
    /// Thực hiện đăng nhập và cấp phiên làm việc (Session).
    /// </summary>
    public async Task<string?> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        // 1. Tìm bản ghi người dùng dựa trên tên đăng nhập
        var user = await _userRepository.FindUserByUsernameAsync(username, cancellationToken);
        if (user is null)
        {
            return null; // Không tìm thấy người dùng
        }

        // 2. Xác thực mật khẩu người dùng nhập vào với chuỗi hash đã lưu
        if (!PasswordHasher.Verify(password, user.Value.PasswordHash))
        {
            return null; // Sai mật khẩu
        }

        // 3. Cập nhật thời điểm đăng nhập cuối cùng vào bảng Users
        await _userRepository.UpdateLastLoginAsync(user.Value.UserId, cancellationToken);

        // 4. Tạo mã SessionId mới (định dạng userId:guid)
        var sessionId = _sessionService.CreateSession(user.Value.UserId);

        // 5. Lưu thông tin phiên làm việc vào bảng Sessions trong database
        await _userRepository.SaveSessionAsync(sessionId, user.Value.UserId, cancellationToken);

        return sessionId;
    }
}