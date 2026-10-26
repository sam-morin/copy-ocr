namespace CopyOCR;

/// <summary>
/// Freezes the screen and lets the user drag a rectangle. One overlay window per monitor
/// (so mixed-DPI setups line up correctly). Esc or right-click cancels.
/// </summary>
sealed class SelectionOverlay : Form
{
    readonly Bitmap _screenshot;   // this monitor's pixels
    readonly Action _cancelAll;
    readonly Action<Bitmap> _complete;
    Point _start;
    Rectangle _selection;
    bool _dragging;
    bool _closed;

    /// <summary>Returns the selected area as a bitmap, or null if cancelled.</summary>
    public static Task<Bitmap?> SelectRegionAsync()
    {
        var tcs = new TaskCompletionSource<Bitmap?>();
        var overlays = new List<SelectionOverlay>();
        Bitmap? result = null;
        int open = 0;

        void CloseAll()
        {
            foreach (var o in overlays)
                if (!o._closed) o.Close();
        }

        foreach (var screen in Screen.AllScreens)
        {
            var b = screen.Bounds;
            var shot = new Bitmap(b.Width, b.Height);
            using (var g = Graphics.FromImage(shot))
                g.CopyFromScreen(b.Location, Point.Empty, b.Size);

            var overlay = new SelectionOverlay(screen, shot, CloseAll, bmp => { result = bmp; CloseAll(); });
            overlay.FormClosed += (_, _) =>
            {
                overlay._closed = true;
                CloseAll(); // closing one (e.g. Alt+F4) closes them all
                if (--open == 0) tcs.TrySetResult(result);
            };
            overlays.Add(overlay);
        }

        open = overlays.Count;
        foreach (var o in overlays) o.Show();
        overlays[0].Activate();
        return tcs.Task;
    }

    SelectionOverlay(Screen screen, Bitmap screenshot, Action cancelAll, Action<Bitmap> complete)
    {
        _screenshot = screenshot;
        _cancelAll = cancelAll;
        _complete = complete;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = screen.Bounds;
        TopMost = true;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.None;
        DoubleBuffered = true;
        Cursor = Cursors.Cross;
        KeyPreview = true;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Activate();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape) _cancelAll();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right) { _cancelAll(); return; }
        if (e.Button != MouseButtons.Left) return;
        _dragging = true;
        _start = e.Location;
        _selection = Rectangle.Empty;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (!_dragging) return;
        var old = _selection;
        _selection = Rectangle.FromLTRB(
            Math.Min(_start.X, e.X), Math.Min(_start.Y, e.Y),
            Math.Max(_start.X, e.X), Math.Max(_start.Y, e.Y));
        var dirty = Rectangle.Union(old, _selection);
        dirty.Inflate(3, 3);
        Invalidate(dirty);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (!_dragging || e.Button != MouseButtons.Left) return;
        _dragging = false;

        var r = Rectangle.Intersect(_selection, new Rectangle(Point.Empty, _screenshot.Size));
        if (r.Width < 4 || r.Height < 4)
        {
            _selection = Rectangle.Empty;
            Invalidate();
            return;
        }
        _complete(_screenshot.Clone(r, _screenshot.PixelFormat));
    }

    protected override void OnPaintBackground(PaintEventArgs e) { }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.DrawImageUnscaled(_screenshot, 0, 0);

        // Dim everything except the selection.
        using var dim = new SolidBrush(Color.FromArgb(110, 0, 0, 0));
        using var region = new Region(ClientRectangle);
        if (!_selection.IsEmpty) region.Exclude(_selection);
        g.FillRegion(dim, region);

        if (!_selection.IsEmpty)
        {
            using var pen = new Pen(Color.FromArgb(0x4F, 0xC3, 0xF7), 2f);
            g.DrawRectangle(pen, _selection);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _screenshot.Dispose();
        base.Dispose(disposing);
    }
}
