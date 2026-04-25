using System.Text.Json;
using Shared.Enums;

namespace Shared.Models;

public sealed record GameMessage
{
    public MessageType Type { get; init; } = MessageType.Unknown;
    public object? Payload { get; init; }
    public string? SenderId { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public string ToJsonLine()
    {
        return JsonSerializer.Serialize(this, JsonOptions) + "\n";
    }

    public static GameMessage? FromJson(string json)
    {
        return JsonSerializer.Deserialize<GameMessage>(json, JsonOptions);
    }
}
