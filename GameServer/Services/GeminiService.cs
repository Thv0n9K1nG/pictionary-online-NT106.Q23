using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GameServer.Services;

public sealed class GeminiService
{
    private const string DefaultModel = "gemini-2.5-flash";
    private const int MaxLoggedTextLength = 240;
    private static readonly ConcurrentDictionary<string, string> HintCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly string[] FallbackHints =
    [
        "Bộ não gemini đã bị \"crash\" hãy dùng bộ não của bạn!",
        "Bộ não của bạn sẽ hữu ích hơn một hint sinh ra bởi AI. Vì vậy hãy dùng não.",
        "Hint là gì? Trust you bro!",
        "Tại sao cần dùng hint, hãy dùng não!",
        "Thằng drawer vẽ xấu quá sao? Hãy chửi nó.",
        "Nhắm mắt lại, suy nghĩ thêm một chút."
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
        if (string.IsNullOrWhiteSpace(word))
        {
            Console.WriteLine("[GameServer][Gemini] Hint skipped: empty word. Using fallback.");
            return CreateFallbackHint(word);
        }

        var cacheKey = NormalizeForCompare(word);
        if (HintCache.TryGetValue(cacheKey, out var cachedHint))
        {
            Console.WriteLine($"[GameServer][Gemini] Hint cache hit for word='{word}'. hint='{TrimForLog(cachedHint)}'");
            return cachedHint;
        }

        if (!IsConfigured)
        {
            Console.WriteLine($"[GameServer][Gemini] Hint skipped for word='{word}': API key is not configured. Using fallback.");
            return CacheFallback(cacheKey, word);
        }

        try
        {
            Console.WriteLine($"[GameServer][Gemini] Fetching hint from Gemini. model='{_model}', word='{word}'");
            var hint = await RequestHintAsync(word, cancellationToken);
            Console.WriteLine($"[GameServer][Gemini] Gemini hint result for word='{word}': '{TrimForLog(hint)}'");
            if (!string.IsNullOrWhiteSpace(hint))
            {
                HintCache[cacheKey] = hint!;
                Console.WriteLine($"[GameServer][Gemini] Accepted hint for word='{word}'.");
                return hint!;
            }

            Console.WriteLine($"[GameServer][Gemini] Empty Gemini hint for word='{word}'. Using fallback.");
        }
        catch (Exception ex)
        {
            // Gemini hints are optional; local fallback keeps gameplay moving.
            Console.WriteLine($"[GameServer][Gemini] Hint fetch failed for word='{word}': {ex.GetType().Name}: {ex.Message}. Using fallback.");
        }

        return CacheFallback(cacheKey, word);
    }

    private async Task<string?> RequestHintAsync(string word, CancellationToken cancellationToken)
    {
        var request = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = BuildPrompt(word) }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.8,
                maxOutputTokens = 80,
                thinkingConfig = new { thinkingBudget = 0 }
            }
        };

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(_model)}:generateContent");
        httpRequest.Headers.Add("x-goog-api-key", _apiKey);
        httpRequest.Content = JsonContent.Create(request);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        Console.WriteLine($"[GameServer][Gemini] HTTP {(int)response.StatusCode} {response.ReasonPhrase}. body='{TrimForLog(responseText)}'");
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using var document = JsonDocument.Parse(responseText);
        return ExtractText(document.RootElement);
    }

    private static string BuildPrompt(string word)
    {
        return $"""
Bạn là quản trò Pictionary tiếng Việt.
Tạo đúng 1 câu gợi ý gián tiếp cho đáp án: "{word}".
Không nhắc lại đáp án, không dùng từ con trong đáp án, không thêm tiền tố "Gợi ý:".
Chỉ trả về một câu tiếng Việt tự nhiên, 7-14 từ.
""";
    }

    private static string? ExtractText(JsonElement root)
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
                    return CleanHint(textElement.GetString());
                }
            }
        }

        return null;
    }

    private static string CacheFallback(string cacheKey, string word)
    {
        var fallback = CreateFallbackHint(word);
        HintCache[cacheKey] = fallback;
        Console.WriteLine($"[GameServer][Gemini] Fallback hint for word='{word}': '{TrimForLog(fallback)}'");
        return fallback;
    }

    private static string TrimForLog(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var compact = Regex.Replace(value.Trim(), @"\s+", " ");
        return compact.Length <= MaxLoggedTextLength ? compact : compact[..MaxLoggedTextLength] + "...";
    }

    private static string CreateFallbackHint(string word)
    {
        var normalized = NormalizeForCompare(word);
        var hash = normalized.Aggregate(17, (current, ch) => current * 31 + ch);
        var index = Math.Abs(hash) % FallbackHints.Length;
        return FallbackHints[index];
    }

    private static string CleanHint(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var hint = raw.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? raw;
        hint = Regex.Replace(hint.Trim(), @"^\s*[-*\d.)]+\s*", string.Empty);
        hint = Regex.Replace(hint, @"^\s*(gợi\s*ý|hint|suggestion)\s*:\s*", string.Empty, RegexOptions.IgnoreCase);
        hint = hint.Trim().Trim('"', '\'', '`');

        var words = hint.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length > 18 ? string.Join(' ', words.Take(18)) : hint;
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

    private static string NormalizeForCompare(string value)
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
