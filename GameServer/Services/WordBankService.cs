namespace GameServer.Services;

public sealed class WordBankService
{
    private static readonly string[] Words =
    [
        "mèo", "chó", "voi", "hổ", "gà",
        "thỏ", "gấu", "cá heo", "chim cánh cụt", "khủng long",
        "rùa", "cá vàng", "ngựa", "bò sữa", "ong mật",
        "bánh mì", "phở", "cơm tấm", "trà sữa", "pizza",
        "bún bò", "bánh xèo", "kem", "sushi", "mì spaghetti",
        "bánh chưng", "gỏi cuốn", "cà phê", "nước mía", "dưa hấu",
        "bóng đá", "cầu lông", "bơi lội", "chạy bộ", "bóng rổ",
        "bóng chuyền", "cờ vua", "trượt ván", "đạp xe", "leo núi",
        "cây dừa", "hoa hồng", "núi", "biển", "mặt trời",
        "mặt trăng", "ngôi sao", "cầu vồng", "thác nước", "sa mạc",
        "rừng tre", "đám mây", "sấm sét", "ngọn lửa", "bông tuyết",
        "máy tính", "điện thoại", "bàn phím", "chuột", "tai nghe",
        "máy ảnh", "đồng hồ", "tủ lạnh", "máy giặt", "điều hòa",
        "xe máy", "ô tô", "máy bay", "tàu hỏa", "thuyền buồm",
        "xe cứu hỏa", "xe đạp", "tàu ngầm", "trực thăng", "tên lửa",
        "bác sĩ", "giáo viên", "đầu bếp", "lính cứu hỏa", "phi hành gia",
        "họa sĩ", "ca sĩ", "nông dân", "thợ xây", "cảnh sát",
        "cây bút", "quyển sách", "ba lô", "cái kéo", "ô che mưa",
        "chìa khóa", "đèn pin", "gương", "ghế sofa", "giường ngủ",
        "nhà thờ", "trường học", "bệnh viện", "siêu thị", "sân bay",
        "công viên", "rạp chiếu phim", "thư viện", "bưu điện", "nhà hàng",
        "robot", "ma thuật", "kho báu", "vương miện", "lâu đài",
        "bản đồ", "la bàn", "kính lúp", "bong bóng", "diều giấy"
    ];

    public int WordCount => Words.Length;

    public IReadOnlyList<string> GetWords(int count = 5)
    {
        if (count <= 0)
        {
            return [];
        }

        return Words.OrderBy(_ => Guid.NewGuid()).Take(count).ToList();
    }

    public IReadOnlyList<string> GetFallbackWords(int count = 5)
    {
        return GetWords(count);
    }
}
