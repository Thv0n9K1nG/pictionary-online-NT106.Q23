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
            var payload = new DrawPayload(_lastPoint.Value.X, _lastPoint.Value.Y, e.Location.X, e.Location.Y, CurrentColor, BrushSize, IsEraser);
            DrawFromRemote(payload);
            LocalDraw?.Invoke(this, payload);
            _lastPoint = e.Location;
        }
        else
        {
            Invalidate(); // Vẽ nháp (Preview) hình khối
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (!CanDraw || _lastPoint is null || e.Button != MouseButtons.Left) return;

        if (CurrentTool != DrawTool.Pen)
        {
            CommitShape(_graphics, _startShapePoint, e.Location);
            Invalidate();
        }

        _isDrawingShape = false;
        _lastPoint = null;
    }

    private void CommitShape(Graphics g, Point start, Point end)
    {
        using var pen = new Pen(IsEraser ? Color.White : ColorTranslator.FromHtml(CurrentColor), BrushSize)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };

        int x = Math.Min(start.X, end.X);
        int y = Math.Min(start.Y, end.Y);
        int w = Math.Abs(start.X - end.X);
        int h = Math.Abs(start.Y - end.Y);

        switch (CurrentTool)
        {
            case DrawTool.Line: g.DrawLine(pen, start, end); break;
            case DrawTool.Rectangle: g.DrawRectangle(pen, x, y, w, h); break;
            case DrawTool.Ellipse: g.DrawEllipse(pen, x, y, w, h); break;
            case DrawTool.Triangle:
                Point p1 = new Point(start.X + (end.X - start.X) / 2, start.Y);
                Point p2 = new Point(start.X, end.Y);
                Point p3 = new Point(end.X, end.Y);
                g.DrawPolygon(pen, new Point[] { p1, p2, p3 });
                break;
        }
    }

    public void DrawFromRemote(DrawPayload payload)
    {
        using var pen = new Pen(payload.IsEraser ? Color.White : ColorTranslator.FromHtml(payload.Color), payload.BrushSize)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        _graphics.DrawLine(pen, payload.X1, payload.Y1, payload.X2, payload.Y2);
        Invalidate();
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

    public void ClearCanvas()
    {
        _graphics.Clear(Color.White);
        Invalidate();
    }
}