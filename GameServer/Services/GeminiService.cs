namespace GameServer.Services;

public sealed class GeminiService
{
    public Task<IReadOnlyList<string>> GenerateWordsAsync(string category, CancellationToken cancellationToken = default)
    {
        // TODO: Call Gemini API from server side only.
        IReadOnlyList<string> words = ["mèo", "bánh mì", "bóng đá", "cây dừa", "máy tính"];
        return Task.FromResult(words);
    }
}
