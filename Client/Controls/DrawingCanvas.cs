using Shared.Models;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Client.Controls;

public enum DrawTool { Pen, Line, Rectangle, Ellipse, Triangle }

public sealed class DrawingCanvas : Panel
{
    private Bitmap _canvasBitmap;
    private Graphics _graphics;
    private Point? _lastPoint;
    private Point _startShapePoint;
    private bool _isDrawingShape = false;

    public bool CanDraw { get; set; } = false;
    public string CurrentColor { get; set; } = "#000000";
    public int BrushSize { get; set; } = 6;
    public bool IsEraser { get; set; }
    public DrawTool CurrentTool { get; set; } = DrawTool.Pen;

    public event EventHandler<DrawPayload>? LocalDraw;

    public DrawingCanvas()
    {
        DoubleBuffered = true;
        BackColor = Color.White;
        Width = 800;
        Height = 600;

        _canvasBitmap = new Bitmap(Width, Height);
        _graphics = Graphics.FromImage(_canvasBitmap);
        _graphics.Clear(Color.White);
        _graphics.SmoothingMode = SmoothingMode.AntiAlias;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!CanDraw || e.Button != MouseButtons.Left) return;

        _lastPoint = e.Location;
        _startShapePoint = e.Location;
        _isDrawingShape = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!CanDraw || _lastPoint is null || e.Button != MouseButtons.Left) return;

        if (CurrentTool == DrawTool.Pen)
        {
            var payload = new DrawPayload(_lastPoint.Value.X, _lastPoint.Value.Y, e.Location.X, e.Location.Y, CurrentColor, BrushSize, IsEraser, DrawTool.Pen.ToString());
            DrawFromRemote(payload);
            LocalDraw?.Invoke(this, payload);
            _lastPoint = e.Location;
        }
        else
        {
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (!CanDraw || _lastPoint is null || e.Button != MouseButtons.Left) return;

        if (CurrentTool != DrawTool.Pen)
        {
            CommitShape(_graphics, _startShapePoint, e.Location, CurrentTool, CurrentColor, BrushSize, IsEraser);
            var payload = new DrawPayload(_startShapePoint.X, _startShapePoint.Y, e.Location.X, e.Location.Y, CurrentColor, BrushSize, IsEraser, CurrentTool.ToString());
            LocalDraw?.Invoke(this, payload);
            Invalidate();
        }

        _isDrawingShape = false;
        _lastPoint = null;
    }

    private void CommitShape(Graphics g, Point start, Point end)
    {
        CommitShape(g, start, end, CurrentTool, CurrentColor, BrushSize, IsEraser);
    }

    // Renders a complete drawing command so local previews and remote replay stay identical.
    private static void CommitShape(Graphics g, Point start, Point end, DrawTool tool, string color, int brushSize, bool isEraser)
    {
        using var pen = CreatePen(color, brushSize, isEraser);
        if (pen is null)
        {
            return;
        }

        var x = Math.Min(start.X, end.X);
        var y = Math.Min(start.Y, end.Y);
        var w = Math.Abs(start.X - end.X);
        var h = Math.Abs(start.Y - end.Y);

        switch (tool)
        {
            case DrawTool.Line:
                g.DrawLine(pen, start, end);
                break;
            case DrawTool.Rectangle:
                g.DrawRectangle(pen, x, y, w, h);
                break;
            case DrawTool.Ellipse:
                g.DrawEllipse(pen, x, y, w, h);
                break;
            case DrawTool.Triangle:
                var p1 = new Point(start.X + (end.X - start.X) / 2, start.Y);
                var p2 = new Point(start.X, end.Y);
                var p3 = new Point(end.X, end.Y);
                g.DrawPolygon(pen, new[] { p1, p2, p3 });
                break;
            default:
                g.DrawLine(pen, start, end);
                break;
        }
    }

    private static Pen? CreatePen(string color, int brushSize, bool isEraser)
    {
        Color resolvedColor;
        try
        {
            resolvedColor = isEraser ? Color.White : ColorTranslator.FromHtml(color);
        }
        catch
        {
            return null;
        }

        return new Pen(resolvedColor, Math.Max(1, brushSize))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
    }

    private static DrawTool ParseTool(string? tool)
    {
        return Enum.TryParse<DrawTool>(tool, ignoreCase: true, out var parsed)
            ? parsed
            : DrawTool.Pen;
    }

    private static bool IsClearCommand(DrawPayload payload)
    {
        return string.Equals(payload.Color, "CLEAR", StringComparison.OrdinalIgnoreCase);
    }

    private static void DrawPenStroke(Graphics g, DrawPayload payload)
    {
        using var pen = CreatePen(payload.Color, payload.BrushSize, payload.IsEraser);
        if (pen is null)
        {
            return;
        }

        g.DrawLine(pen, payload.X1, payload.Y1, payload.X2, payload.Y2);
    }

    public void DrawFromRemote(DrawPayload payload)
    {
        if (_graphics == null) return;

        if (IsClearCommand(payload))
        {
            ClearCanvas(false);
            return;
        }

        var tool = ParseTool(payload.Tool);
        if (tool == DrawTool.Pen)
        {
            DrawPenStroke(_graphics, payload);
        }
        else
        {
            CommitShape(
                _graphics,
                new Point(payload.X1, payload.Y1),
                new Point(payload.X2, payload.Y2),
                tool,
                payload.Color,
                payload.BrushSize,
                payload.IsEraser);
        }

        if (InvokeRequired) Invoke(new Action(Invalidate));
        else Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.DrawImageUnscaled(_canvasBitmap, Point.Empty);

        if (_isDrawingShape && CurrentTool != DrawTool.Pen)
        {
            CommitShape(e.Graphics, _startShapePoint, PointToClient(Cursor.Position));
        }
    }

    public void ClearCanvas(bool triggerEvent = true)
    {
        _graphics.Clear(Color.White);
        Invalidate();

        if (triggerEvent)
        {
            var payload = new DrawPayload(0, 0, 0, 0, "CLEAR", 0, false, DrawTool.Pen.ToString());
            LocalDraw?.Invoke(this, payload);
        }
    }
}
