using GameServer.Services;

namespace GameServer.Engine;

public sealed class GameEngine
{
    public const int RoundSeconds = 60;

    private readonly GeminiService _geminiService;
    private readonly WordBankService _wordBankService;

    public GameEngine()
        : this(new GeminiService(), new WordBankService())
    {
    }

    public GameEngine(GeminiService geminiService, WordBankService wordBankService)
    {
        _geminiService = geminiService;
        _wordBankService = wordBankService;
    }

    public async Task<IReadOnlyList<string>> GetWordOptionsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var words = await _geminiService.GenerateWordsAsync("pictionary", cancellationToken);
            if (words.Count >= 5)
            {
                return words.Take(5).ToList();
            }
        }
        catch
        {
            // Gemini is optional for the course demo; the local word bank keeps gameplay alive.
        }

        return _wordBankService.GetFallbackWords(5);
    }

    public int CalculateGuessScore(int elapsedSeconds)
    {
        return Math.Max((int)(100 - elapsedSeconds * 1.5), 10);
    }

    public int CalculateDrawerScore(int correctGuessers)
    {
        return correctGuessers * 20;
    }

    public string MaskWord(string word)
    {
        return string.Join(" ", word.Select(ch => char.IsWhiteSpace(ch) ? '/' : '_'));
    }

    public bool IsCorrectGuess(string guess, string word)
    {
        return string.Equals(Normalize(guess), Normalize(word), StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string value)
    {
        return string.Join(' ', value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
