using System.Drawing;
using System.Drawing.Imaging;
using SmartMacroAutomation.Models;
using SmartMacroAutomation.Native;
using SmartMacroAutomation.Playback;
using SmartMacroAutomation.Storage;

namespace SmartMacroAutomation.UI;

/// <summary>
/// Finds a window by title, shows its position / size / monitor and a picture of it (taken with
/// PrintWindow, so it works even when the window is covered). Clicking on the picture picks a point;
/// the point can be test-clicked and added to the selected macro as FindWindow + WindowClick actions.
/// </summary>
internal sealed class WindowToolForm : Form
{
    /// <summary>Size of the reference image stored with a WindowClick, centered on the click point.</summary>
    private const int ReferenceSize = 24;

    private readonly string? _macroName;

    private readonly TextBox _titleBox = new() { Left = 150, Top = 10, Width = 260 };
    private readonly TextBox _processBox = new() { Left = 540, Top = 10, Width = 160 };
    private readonly Button _findButton = new() { Text = "Find", Left = 710, Top = 8, Width = 80 };
    private readonly Label _infoLabel = new() { Left = 10, Top = 40, Width = 860, Height = 36, Text = "Type part of the window title and click Find." };

    private readonly ComboBox _anchorBox = new() { Left = 60, Top = 82, Width = 110, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _modeBox = new() { Left = 220, Top = 82, Width = 110, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _buttonBox = new() { Left = 390, Top = 82, Width = 80, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Button _testButton = new() { Text = "Test Click", Left = 480, Top = 80, Width = 90, Enabled = false };
    private readonly Button _addButton = new() { Text = "Add to Selected Macro", Left = 580, Top = 80, Width = 160, Enabled = false };
    private readonly Button _refreshButton = new() { Text = "Refresh", Left = 750, Top = 80, Width = 80, Enabled = false };
    private readonly Label _pointLabel = new() { Left = 10, Top = 112, Width = 860, Height = 20, Text = "Click on the window picture below to pick a point." };

    private readonly PictureBox _picture = new() { SizeMode = PictureBoxSizeMode.AutoSize, Cursor = Cursors.Cross };

    private WindowInfo? _window;
    private Bitmap? _capture;
    private Point? _point; // selected point, in window coordinates

    public WindowToolForm(string? macroName)
    {
        _macroName = macroName;

        Text = "Window Tool" + (macroName != null ? $" - macro '{macroName}'" : "");
        Width = 900;
        Height = 760;
        StartPosition = FormStartPosition.CenterParent;

        _anchorBox.Items.AddRange(Enum.GetNames<WindowAnchor>());
        _anchorBox.SelectedItem = nameof(WindowAnchor.TopRight);
        _modeBox.Items.AddRange(Enum.GetNames<WindowClickMode>());
        _modeBox.SelectedItem = nameof(WindowClickMode.Background);
        _buttonBox.Items.AddRange(Enum.GetNames<MouseButtonKind>());
        _buttonBox.SelectedItem = nameof(MouseButtonKind.Left);

        var top = new Panel { Dock = DockStyle.Top, Height = 136 };
        top.Controls.AddRange(new Control[]
        {
            new Label { Text = "Window title contains:", Left = 10, Top = 13, Width = 140 }, _titleBox,
            new Label { Text = "Process (optional):", Left = 420, Top = 13, Width = 120 }, _processBox,
            _findButton, _infoLabel,
            new Label { Text = "Anchor:", Left = 10, Top = 85, Width = 50 }, _anchorBox,
            new Label { Text = "Mode:", Left = 180, Top = 85, Width = 40 }, _modeBox,
            new Label { Text = "Button:", Left = 340, Top = 85, Width = 50 }, _buttonBox,
            _testButton, _addButton, _refreshButton, _pointLabel
        });

        var picturePanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.DimGray };
        picturePanel.Controls.Add(_picture);

        Controls.Add(picturePanel);
        Controls.Add(top);

        AcceptButton = _findButton;
        _findButton.Click += (_, _) => FindWindow();
        _refreshButton.Click += (_, _) => RefreshWindow();
        _testButton.Click += (_, _) => TestClick();
        _addButton.Click += (_, _) => AddToMacro();
        _anchorBox.SelectedIndexChanged += (_, _) => UpdatePointLabel();
        _picture.MouseDown += Picture_MouseDown;
        _picture.Paint += Picture_Paint;
    }

    private WindowAnchor SelectedAnchor => Enum.Parse<WindowAnchor>((string)_anchorBox.SelectedItem!);
    private WindowClickMode Mode => Enum.Parse<WindowClickMode>((string)_modeBox.SelectedItem!);
    private MouseButtonKind ButtonKind => Enum.Parse<MouseButtonKind>((string)_buttonBox.SelectedItem!);

    private void FindWindow()
    {
        _point = null;
        if (_titleBox.Text.Trim().Length == 0 && _processBox.Text.Trim().Length == 0)
        {
            ShowWindowInfo(null);
            _infoLabel.Text = "Type part of the window title (or a process name) first.";
            return;
        }

        var found = WindowFinder.Find(_titleBox.Text, _processBox.Text);
        if (found == null)
        {
            ShowWindowInfo(null);
            _infoLabel.Text = "No visible window matches that title" + (_processBox.Text.Trim().Length > 0 ? " and process." : ".");
            return;
        }
        ShowWindowInfo(found);
    }

    private void RefreshWindow()
    {
        if (_window == null) return;
        var refreshed = WindowFinder.Refresh(_window.Handle);
        ShowWindowInfo(refreshed);
        if (refreshed == null)
            _infoLabel.Text = "The window was closed.";
    }

    private void ShowWindowInfo(WindowInfo? window)
    {
        _window = window;
        _capture?.Dispose();
        _capture = window != null ? WindowCapture.Capture(window.Handle) : null;
        _picture.Image?.Dispose();
        _picture.Image = _capture != null ? new Bitmap(_capture) : null;

        _infoLabel.Text = window == null ? "" :
            window.Describe() + (window.IsMinimized ? " Restore it to see its picture and pick a point." : "") +
            $"\nClass: {window.ClassName}, handle: 0x{window.Handle:X}";

        _refreshButton.Enabled = window != null;
        UpdatePointLabel();
    }

    private void Picture_MouseDown(object? sender, MouseEventArgs e)
    {
        if (_capture == null) return;
        _point = new Point(Math.Clamp(e.X, 0, _capture.Width - 1), Math.Clamp(e.Y, 0, _capture.Height - 1));

        // Pick the nearest corner as anchor, so the point stays right when the window is resized.
        bool right = _point.Value.X > _capture.Width / 2;
        bool bottom = _point.Value.Y > _capture.Height / 2;
        _anchorBox.SelectedItem = (bottom ? "Bottom" : "Top") + (right ? "Right" : "Left");

        UpdatePointLabel();
        _picture.Invalidate();
    }

    private void Picture_Paint(object? sender, PaintEventArgs e)
    {
        if (_point is not { } p) return;
        using var pen = new Pen(Color.Lime, 1);
        e.Graphics.DrawRectangle(pen, ReferenceRect(p));
        e.Graphics.DrawLine(pen, p.X - 6, p.Y, p.X + 6, p.Y);
        e.Graphics.DrawLine(pen, p.X, p.Y - 6, p.X, p.Y + 6);
    }

    private void UpdatePointLabel()
    {
        bool ready = _point != null && _window != null && !_window.IsMinimized;
        _testButton.Enabled = ready;
        _addButton.Enabled = ready && _macroName != null;

        if (!ready)
        {
            _pointLabel.Text = _macroName == null
                ? "Click on the window picture to pick a point. (Select a macro in the main window to be able to add it.)"
                : "Click on the window picture below to pick a point.";
            return;
        }

        var p = _point!.Value;
        var offset = WindowInput.ToAnchorOffset(_window!.Bounds.Size, SelectedAnchor, p);
        _pointLabel.Text = $"Point in window: ({p.X},{p.Y})   offset from {SelectedAnchor}: X={offset.X} Y={offset.Y}   " +
                           $"on screen: ({_window.Bounds.X + p.X},{_window.Bounds.Y + p.Y})";
    }

    /// <summary>The reference image area around a point, clipped to the window picture.</summary>
    private Rectangle ReferenceRect(Point p)
    {
        var rect = new Rectangle(p.X - ReferenceSize / 2, p.Y - ReferenceSize / 2, ReferenceSize, ReferenceSize);
        if (_capture != null)
            rect.Intersect(new Rectangle(0, 0, _capture.Width, _capture.Height));
        return rect;
    }

    private void TestClick()
    {
        if (_window == null || _point is not { } p) return;

        var screen = new Point(_window.Bounds.X + p.X, _window.Bounds.Y + p.Y);
        if (Mode == WindowClickMode.Foreground)
            WindowInput.ForegroundClick(_window.Handle, screen, ButtonKind, 1600);
        else
            WindowInput.BackgroundClick(_window.Handle, screen, ButtonKind);

        // Give the app a moment to react, then show the result (e.g. "is minimized").
        Thread.Sleep(700);
        var point = _point;
        RefreshWindow();
        _point = _window is { IsMinimized: false } ? point : null;
        UpdatePointLabel();
        _picture.Invalidate();
    }

    private void AddToMacro()
    {
        if (_macroName == null || _window == null || _capture == null || _point is not { } p) return;

        var macro = MacroStorage.LoadMacro(_macroName);
        if (macro == null) return;

        // Only add a FindWindow if the macro does not already look for this same window last.
        var lastFind = macro.Actions.LastOrDefault(a => a.Type == ActionType.FindWindow);
        if (lastFind == null || lastFind.WindowTitle != _window.Title || lastFind.ProcessName != _window.ProcessName)
        {
            macro.Actions.Add(new ActionRecord
            {
                Type = ActionType.FindWindow,
                WindowTitle = _window.Title,
                ProcessName = _window.ProcessName,
                TimeoutMs = 3000
            });
        }

        var refRect = ReferenceRect(p);
        int index = macro.Actions.Count;
        string fileName = $"window_{index:0000}.png";
        string imagesFolder = MacroStorage.GetImagesFolder(_macroName);
        Directory.CreateDirectory(imagesFolder);
        using (var reference = WindowCapture.Crop(_capture, refRect))
            reference.Save(Path.Combine(imagesFolder, fileName), ImageFormat.Png);

        var offset = WindowInput.ToAnchorOffset(_window.Bounds.Size, SelectedAnchor, p);
        macro.Actions.Add(new ActionRecord
        {
            Type = ActionType.WindowClick,
            X = offset.X,
            Y = offset.Y,
            Anchor = SelectedAnchor,
            ClickMode = Mode,
            Button = ButtonKind,
            ImageFile = fileName,
            RefOffsetX = refRect.X - p.X,
            RefOffsetY = refRect.Y - p.Y,
            RefWidth = refRect.Width,
            RefHeight = refRect.Height,
            DelayMs = 300
        });

        MacroStorage.SaveMacro(macro);
        MessageBox.Show(this, $"Added a click at {SelectedAnchor} X={offset.X} Y={offset.Y} on '{_window.Title}' to macro '{_macroName}'.\n\n" +
                              "Tip: if the button changes color when the mouse is over it, use Review Screenshots to crop the " +
                              "reference image down to just the icon, so it matches in both states.",
            "Added", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _capture?.Dispose();
            _picture.Image?.Dispose();
        }
        base.Dispose(disposing);
    }
}
