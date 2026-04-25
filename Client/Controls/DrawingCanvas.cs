using Shared.Models;

namespace Client.Controls;

public sealed class DrawingCanvas : Panel
{
    private Point? _lastPoint;

    public bool CanDraw { get; set; }
    public string CurrentColor { get; set; } = "#000000";
    public int BrushSize { get; set; } = 4;
    public bool IsEraser { get; set; }

    public event EventHandler<DrawPayload>? LocalDraw;

    public DrawingCanvas()
    {
        DoubleBuffered = true;
        BackColor = Color.White;
        Width = 800;
        Height = 600;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (!CanDraw)
        {
            return;
        }

        _lastPoint = e.Location;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (!CanDraw || _lastPoint is null || e.Button != MouseButtons.Left)
        {
            return;
        }

        var payload = new DrawPayload(
            _lastPoint.Value.X,
            _lastPoint.Value.Y,
            e.Location.X,
            e.Location.Y,
            CurrentColor,
            BrushSize,
            IsEraser
        );

        LocalDraw?.Invoke(this, payload);
        DrawFromRemote(payload);

        _lastPoint = e.Location;
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _lastPoint = null;
    }

    public void DrawFromRemote(DrawPayload payload)
    {
        using var graphics = CreateGraphics();
        using var pen = new Pen(payload.IsEraser ? Color.White : ColorTranslator.FromHtml(payload.Color), payload.BrushSize)
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap = System.Drawing.Drawing2D.LineCap.Round
        };

        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.DrawLine(pen, payload.X1, payload.Y1, payload.X2, payload.Y2);
    }
}
