using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GameServer.Services;

public sealed class GeminiService
{
    private const string DefaultModel = "gemini-2.5-flash";
    private static readonly ConcurrentDictionary<string, string> HintCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly string[] FallbackHints =
    [
        "Bộ não gemini đã bị \"crash\" hãy dùng bộ não của bạn!",
        "Bộ não của bạn sẽ hữu ích hơn một hint sinh ra bời AI. Vì vậy hãy dùng não.",
        "Hint là gì? Trust you bro!",
        "Tại sao cần dùng hint, hãy dùng não!",
        "Thằng drawer vẽ xấu quá sao? Hãy chửi nó.",
        "Nhắm mát lại, suy nghĩ thêm một chút."
    ];

    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(12)
    };

    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;
    private readonly string _model;

    public GeminiService()
        : this(SharedHttpClient)
    {
    }

    public GeminiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _apiKey = ReadApiKey();
        _model = ReadSetting("GEMINI_MODEL") ?? DefaultModel;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public async Task<string> GenerateHintAsync(string word, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(word) || !IsConfigured)
        {
            return CreateFallbackHint(word);
        }

        var cacheKey = NormalizeVietnamese(word);
        if (HintCache.TryGetValue(cacheKey, out var cachedHint))
        {
            return cachedHint;
        }

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                var request = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = BuildHintPrompt(word, attempt) }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = attempt == 1 ? 0.9 : 1.0,
                        topP = 0.92,
                        maxOutputTokens = 160,
                        thinkingConfig = new
                        {
                            thinkingBudget = 0
                        }
                    }
                };

                using var httpRequest = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(_model)}:generateContent");
                httpRequest.Headers.Add("x-goog-api-key", _apiKey);
                httpRequest.Content = JsonContent.Create(request);

                using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    await DelayForRateLimitAsync(response, cancellationToken);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);
                var hint = ExtractHint(document.RootElement, word);

                if (!string.IsNullOrWhiteSpace(hint))
                {
                    HintCache[cacheKey] = hint;
                    return hint;
                }
            }
            catch
            {
                // Gemini hints are optional; retry once, then use a local safe hint.
            }
        }

        var fallbackHint = CreateFallbackHint(word);
        HintCache[cacheKey] = fallbackHint;
        return fallbackHint;
    }

    private static string BuildHintPrompt(string word, int attempt)
    {
        var extraInstruction = attempt == 1
            ? "Viết tự nhiên, giàu hình ảnh, tránh từ quá ngắn."
            : "Lần này bắt buộc dùng một câu 7-12 từ, không được trả lời 1-3 từ.";

        return $"""
Bạn là người quản trò cho game Pictionary tiếng Việt.
Hãy tạo đúng 1 gợi ý sáng tạo, gián tiếp cho đáp án bí mật.

Đáp án bí mật: "{word}"

Luật bắt buộc:
- Không được nhắc lại đáp án bí mật.
- Không dùng từng từ con rõ ràng trong đáp án bí mật.
- Không mô tả quá trực diện như định nghĩa từ điển.
- Không đưa ra danh từ gần nghĩa quá hiển nhiên.
- Không dùng dấu ngoặc kép.
- Không thêm tiền tố như "Gợi ý:".
- Chỉ trả về một câu duy nhất, 7-12 từ.
- Không trả lời dạng danh sách.
- Không giải thích thêm, không chào hỏi, không mở đầu.
- Nếu trả về danh sách hoặc lời dẫn, câu trả lời bị tính sai.
- Câu phải gợi hình để người chơi vẽ/suy luận, không phải đáp án.
- {extraInstruction}
""";
    }

    private static string? ExtractHint(JsonElement root, string word)
    {
        if (!TryGetPropertyIgnoreCase(root, "candidates", out var candidates) ||
            candidates.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var candidate in candidates.EnumerateArray())
        {
            if (!TryGetPropertyIgnoreCase(candidate, "content", out var content) ||
                !TryGetPropertyIgnoreCase(content, "parts", out var parts) ||
                parts.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var part in parts.EnumerateArray())
            {
                if (TryGetPropertyIgnoreCase(part, "text", out var textElement) &&
                    textElement.ValueKind == JsonValueKind.String)
                {
                    foreach (var hint in BuildHintCandidates(textElement.GetString()))
                    {
                        if (IsUsableHint(hint, word))
                        {
                            return hint;
                        }
                    }
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> BuildHintCandidates(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            yield break;
        }

        var normalized = raw.Replace("\r\n", "\n");
        foreach (var line in normalized.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var hint = CleanHint(line);
            if (!string.IsNullOrWhiteSpace(hint))
            {
                yield return hint;
            }
        }

        var fullHint = CleanHint(raw);
        if (!string.IsNullOrWhiteSpace(fullHint))
        {
            yield return fullHint;
        }
    }

    private static string CreateFallbackHint(string word)
    {
        var normalized = NormalizeVietnamese(word);
        var hash = normalized.Aggregate(17, (current, ch) => current * 31 + ch);
        var index = Math.Abs(hash) % FallbackHints.Length;
        return FallbackHints[index];
    }

    private static async Task DelayForRateLimitAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var delay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromMilliseconds(1500);
        if (delay > TimeSpan.FromSeconds(5))
        {
            delay = TimeSpan.FromSeconds(5);
        }

        await Task.Delay(delay, cancellationToken);
    }

    private static bool IsUsableHint(string? hint, string word)
    {
        if (string.IsNullOrWhiteSpace(hint))
        {
            return false;
        }

        var words = hint.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (hint.Length < 18 || words.Length < 4)
        {
            return false;
        }

        var normalizedHint = NormalizeVietnamese(hint);
        var normalizedWord = NormalizeVietnamese(word);
        if (normalizedHint.Contains(normalizedWord, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var meaningfulTokens = normalizedWord
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token.Length >= 3);

        return !meaningfulTokens.Any(token => normalizedHint.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static string CleanHint(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var hint = raw.Trim();
        hint = Regex.Replace(hint, @"^\s*[-*\d.)]+\s*", string.Empty);
        hint = Regex.Replace(hint, @"^\s*gợi\s*ý\s*:\s*", string.Empty, RegexOptions.IgnoreCase);
        hint = Regex.Replace(hint, @"^\s*(hint|suggestion)\s*:\s*", string.Empty, RegexOptions.IgnoreCase);
        hint = hint.Trim().Trim('"', '\'', '`');

        var words = hint.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 18)
        {
            hint = string.Join(' ', words.Take(18));
        }

        return hint;
    }

    private static string? ReadApiKey()
    {
        var direct = ReadSetting("GEMINI_API_KEY") ?? ReadSetting("GOOGLE_API_KEY");
        if (!string.IsNullOrWhiteSpace(direct))
        {
            return direct;
        }

        var configuredPath = ReadSetting("GEMINI_API_KEY_FILE");
        foreach (var path in CandidateKeyFiles(configuredPath))
        {
            if (!File.Exists(path))
            {
                continue;
            }

            var key = File.ReadAllText(path).Trim();
            if (!string.IsNullOrWhiteSpace(key))
            {
                return key;
            }
        }

        return null;
    }

    private static IEnumerable<string> CandidateKeyFiles(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            yield return configuredPath;
        }

        yield return Path.Combine(Environment.CurrentDirectory, "GameServer", "secrets", "gemini-api-key.txt");
        yield return Path.Combine(Environment.CurrentDirectory, "secrets", "gemini-api-key.txt");
        yield return Path.Combine(AppContext.BaseDirectory, "secrets", "gemini-api-key.txt");

        foreach (var path in AncestorKeyFiles(AppContext.BaseDirectory))
        {
            yield return path;
        }

        foreach (var path in AncestorKeyFiles(Environment.CurrentDirectory))
        {
            yield return path;
        }
    }

    private static IEnumerable<string> AncestorKeyFiles(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null)
        {
            yield return Path.Combine(directory.FullName, "secrets", "gemini-api-key.txt");
            yield return Path.Combine(directory.FullName, "GameServer", "secrets", "gemini-api-key.txt");
            directory = directory.Parent;
        }
    }

    private static string? ReadSetting(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static string NormalizeVietnamese(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var ch in normalized)
        {
            var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
        }

        return Regex.Replace(builder.ToString().Normalize(NormalizationForm.FormC), @"\s+", " ").Trim();
    }
}
