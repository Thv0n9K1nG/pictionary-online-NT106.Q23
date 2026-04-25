namespace GameServer.Services;

public sealed class WordBankService
{
    private static readonly string[] Words =
    [
        "mèo", "chó", "voi", "hổ", "gà",
        "bánh mì", "phở", "cơm tấm", "trà sữa", "pizza",
        "bóng đá", "cầu lông", "bơi lội", "chạy bộ", "bóng rổ",
        "cây dừa", "hoa hồng", "núi", "biển", "mặt trời",
        "máy tính", "điện thoại", "bàn phím", "chuột", "tai nghe"
    ];

    public IReadOnlyList<string> GetFallbackWords(int count = 5)
    {
        return Words.OrderBy(_ => Guid.NewGuid()).Take(count).ToList();
    }
}
