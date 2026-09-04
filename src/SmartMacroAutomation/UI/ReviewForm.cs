using System.Drawing;
using System.Drawing.Imaging;
using SmartMacroAutomation.Models;
using SmartMacroAutomation.Storage;

namespace SmartMacroAutomation.UI;

/// <summary>
/// Lets the user look at every recorded click's reference screenshot and,
/// optionally, crop it down to just the relevant part of the UI.
/// </summary>
internal sealed class ReviewForm : Form
{
    private const int Zoom = 4;

    private readonly Macro _macro;
    private readonly ListBox _list = new() { Dock = DockStyle.Left, Width = 220 };
    private readonly PictureBox _picture = new() { BorderStyle = BorderStyle.FixedSingle, BackColor = Color.Black };
    private readonly Label _info = new() { Dock = DockStyle.Bottom, Height = 40, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0) };
    private readonly Button _applyCropButton = new() { Text = "Apply Crop", Dock = DockStyle.Bottom, Height = 32 };
    private readonly Button _resetButton = new() { Text = "Reset to Full 50x50", Dock = DockStyle.Bottom, Height = 32 };

    private readonly List<int> _clickActionIndexes = new();
    private Bitmap? _currentImage;
    private Point? _dragStart;
    private Rectangle _selection;
    private bool _dragging;

    public ReviewForm(Macro macro)
    {
        _macro = macro;

        Text = $"Review Screenshots - {macro.Name}";
        Width = 800;
        Height = 600;
        StartPosition = FormStartPosition.CenterParent;

        for (int i = 0; i < macro.Actions.Count; i++)
        {
            if (macro.Actions[i].Type == ActionType.MouseClick)
            {
                _clickActionIndexes.Add(i);
                _list.Items.Add($"Click #{_clickActionIndexes.Count} at ({macro.Actions[i].X},{macro.Actions[i].Y})");
            }
        }

        var picturePanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        _picture.SizeMode = PictureBoxSizeMode.AutoSize;
        _picture.MouseDown += Picture_MouseDown;
        _picture.MouseMove += Picture_MouseMove;
        _picture.MouseUp += Picture_MouseUp;
        _picture.Paint += Picture_Paint;
        picturePanel.Controls.Add(_picture);

        _applyCropButton.Click += ApplyCropButton_Click;
        _resetButton.Click += ResetButton_Click;
        _list.SelectedIndexChanged += (_, _) => LoadSelected();

        var rightPanel = new Panel { Dock = DockStyle.Fill };
        rightPanel.Controls.Add(picturePanel);
        rightPanel.Controls.Add(_info);
        rightPanel.Controls.Add(_applyCropButton);
        rightPanel.Controls.Add(_resetButton);

        Controls.Add(rightPanel);
        Controls.Add(_list);

        if (_list.Items.Count > 0)
            _list.SelectedIndex = 0;
        else
            _info.Text = "This macro has no mouse-click screenshots yet.";
    }

    private ActionRecord? CurrentAction =>
        _list.SelectedIndex >= 0 ? _macro.Actions[_clickActionIndexes[_list.SelectedIndex]] : null;

    private void LoadSelected()
    {
        _currentImage?.Dispose();
        _currentImage = null;
        _selection = Rectangle.Empty;

        var action = CurrentAction;
        if (action?.ImageFile == null)
        {
            _picture.Image = null;
            return;
        }

        string path = Path.Combine(MacroStorage.GetImagesFolder(_macro.Name), action.ImageFile);
        if (!File.Exists(path))
        {
            _picture.Image = null;
            _info.Text = "Reference image file is missing.";
            return;
        }

        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            _currentImage = new Bitmap(new Bitmap(fs));

        _picture.Image = new Bitmap(_currentImage.Width * Zoom, _currentImage.Height * Zoom);
        using (var g = Graphics.FromImage(_picture.Image))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.DrawImage(_currentImage, 0, 0, _currentImage.Width * Zoom, _currentImage.Height * Zoom);
        }

        _info.Text = $"Image size: {_currentImage.Width}x{_currentImage.Height}. Drag on the image to select a crop area, then click Apply Crop.";
    }

    private void Picture_MouseDown(object? sender, MouseEventArgs e)
    {
        _dragging = true;
        _dragStart = e.Location;
        _selection = new Rectangle(e.Location, Size.Empty);
    }

    private void Picture_MouseMove(object? sender, MouseEventArgs e)
    {
        if (!_dragging || _dragStart == null) return;
        int x = Math.Min(_dragStart.Value.X, e.X);
        int y = Math.Min(_dragStart.Value.Y, e.Y);
        int w = Math.Abs(e.X - _dragStart.Value.X);
        int h = Math.Abs(e.Y - _dragStart.Value.Y);
        _selection = new Rectangle(x, y, w, h);
        _picture.Invalidate();
    }

    private void Picture_MouseUp(object? sender, MouseEventArgs e)
    {
        _dragging = false;
    }

    private void Picture_Paint(object? sender, PaintEventArgs e)
    {
        if (_selection.Width > 0 && _selection.Height > 0)
        {
            using var pen = new Pen(Color.Lime, 2);
            e.Graphics.DrawRectangle(pen, _selection);
        }
    }

    private void ApplyCropButton_Click(object? sender, EventArgs e)
    {
        var action = CurrentAction;
        if (action == null || _currentImage == null || _selection.Width < 2 || _selection.Height < 2)
        {
            MessageBox.Show(this, "Drag a selection rectangle on the image first.", "Nothing to crop", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // Convert the on-screen (zoomed) selection back to original image pixel coordinates.
        int srcX = Math.Max(0, _selection.X / Zoom);
        int srcY = Math.Max(0, _selection.Y / Zoom);
        int srcW = Math.Min(_currentImage.Width - srcX, Math.Max(1, _selection.Width / Zoom));
        int srcH = Math.Min(_currentImage.Height - srcY, Math.Max(1, _selection.Height / Zoom));

        var cropped = new Bitmap(srcW, srcH, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(cropped))
            g.DrawImage(_currentImage, new Rectangle(0, 0, srcW, srcH), new Rectangle(srcX, srcY, srcW, srcH), GraphicsUnit.Pixel);

        // The crop was taken from an image whose top-left is at (action.X + action.RefOffsetX, action.Y + action.RefOffsetY).
        action.RefOffsetX += srcX;
        action.RefOffsetY += srcY;
        action.RefWidth = srcW;
        action.RefHeight = srcH;

        string imagesFolder = MacroStorage.GetImagesFolder(_macro.Name);
        string path = Path.Combine(imagesFolder, action.ImageFile!);
        cropped.Save(path, ImageFormat.Png);
        cropped.Dispose();

        MacroStorage.SaveMacro(_macro);

        MessageBox.Show(this, "Crop applied and saved.", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
        LoadSelected();
    }

    private void ResetButton_Click(object? sender, EventArgs e)
    {
        MessageBox.Show(this, "To fully reset, re-record this click. Cropping only narrows the existing reference image.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _currentImage?.Dispose();
        base.Dispose(disposing);
    }
}
