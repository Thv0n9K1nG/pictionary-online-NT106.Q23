namespace Shared.Models;

public sealed record DrawPayload(
    int X1,
    int Y1,
    int X2,
    int Y2,
    string Color,
    int BrushSize,
    bool IsEraser
);
