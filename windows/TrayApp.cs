using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Microsoft.Win32;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace CopyOCR;

/// <summary>
/// Notification-area app: left-click the icon, drag over an area of the screen,
/// and the text in that area is recognized (Windows OCR) and copied to the clipboard.
/// Right-click the icon for options.
/// </summary>
sealed class TrayApp : ApplicationContext
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string RunValue = "CopyOCR";

    readonly NotifyIcon _tray;
    readonly ToolStripMenuItem _startupItem;
    bool _busy;

    public TrayApp()
    {
        _startupItem = new ToolStripMenuItem("Start with Windows", null, (_, _) => ToggleStartup());

        var menu = new ContextMenuStrip();
        menu.Items.Add("Capture Text", null, async (_, _) => await CaptureAsync());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_startupItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());
        menu.Opening += (_, _) => _startupItem.Checked = IsStartupEnabled();

        _tray = new NotifyIcon
        {
            Icon = CreateTrayIcon(),
            Text = "Copy OCR — click to copy text from a screen area",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _tray.MouseClick += async (_, e) =>
        {
            if (e.Button == MouseButtons.Left) await CaptureAsync();
        };
    }

    async Task CaptureAsync()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            using var region = await SelectionOverlay.SelectRegionAsync();
            if (region == null) return; // cancelled

            string text = await RecognizeAsync(region);
            if (string.IsNullOrWhiteSpace(text))
            {
                _tray.ShowBalloonTip(2000, "Copy OCR", "No text found in that area.", ToolTipIcon.Warning);
                return;
            }

            Clipboard.SetText(text);
            string preview = text.Length > 120 ? text[..120] + "…" : text;
            _tray.ShowBalloonTip(2000, "Copied to clipboard", preview, ToolTipIcon.None);
        }
        catch (Exception ex)
        {
            _tray.ShowBalloonTip(4000, "Copy OCR error", ex.Message, ToolTipIcon.Error);
        }
        finally
        {
            _busy = false;
        }
    }

    static async Task<string> RecognizeAsync(Bitmap bitmap)
    {
        var engine = OcrEngine.TryCreateFromUserProfileLanguages()
            ?? throw new InvalidOperationException("No OCR language is installed. Add one in Settings → Time & Language → Language.");

        // Windows OCR is much more accurate on small UI text when the image is upscaled.
        int maxDim = (int)OcrEngine.MaxImageDimension;
        double scale = Math.Min(3.0, (double)maxDim / Math.Max(bitmap.Width, bitmap.Height));
        scale = Math.Max(1.0, scale);

        using var scaled = new Bitmap((int)(bitmap.Width * scale), (int)(bitmap.Height * scale));
        using (var g = Graphics.FromImage(scaled))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(bitmap, 0, 0, scaled.Width, scaled.Height);
        }

        using var png = new MemoryStream();
        scaled.Save(png, ImageFormat.Png);

        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
        {
            writer.WriteBytes(png.ToArray());
            await writer.StoreAsync();
            await writer.FlushAsync();
            writer.DetachStream();
        }

        var decoder = await BitmapDecoder.CreateAsync(stream);
        using var softwareBitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        var result = await engine.RecognizeAsync(softwareBitmap);

        return string.Join(Environment.NewLine, result.Lines.Select(l => l.Text)).Trim();
    }

    static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(RunValue) != null;
    }

    static void ToggleStartup()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true) ?? Registry.CurrentUser.CreateSubKey(RunKey);
        if (key.GetValue(RunValue) != null)
            key.DeleteValue(RunValue);
        else
            key.SetValue(RunValue, $"\"{Environment.ProcessPath}\"");
    }

    /// <summary>Draws a small "selection box with text lines" icon so no .ico file is needed.</summary>
    static Icon CreateTrayIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var corner = new Pen(Color.White, 3f);
            // Viewfinder corners
            g.DrawLines(corner, new[] { new Point(2, 10), new Point(2, 2), new Point(10, 2) });
            g.DrawLines(corner, new[] { new Point(22, 2), new Point(29, 2), new Point(29, 10) });
            g.DrawLines(corner, new[] { new Point(2, 22), new Point(2, 29), new Point(10, 29) });
            g.DrawLines(corner, new[] { new Point(22, 29), new Point(29, 29), new Point(29, 22) });
            // Text lines
            using var line = new Pen(Color.FromArgb(0x4F, 0xC3, 0xF7), 3f);
            g.DrawLine(line, 8, 11, 24, 11);
            g.DrawLine(line, 8, 16, 24, 16);
            g.DrawLine(line, 8, 21, 18, 21);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    protected override void ExitThreadCore()
    {
        _tray.Visible = false;
        _tray.Dispose();
        base.ExitThreadCore();
    }
}
