using System.Diagnostics;
using System.Drawing.Imaging;
using SmartMacroAutomation.Models;
using SmartMacroAutomation.Playback;
using SmartMacroAutomation.Recording;
using SmartMacroAutomation.Storage;

namespace SmartMacroAutomation.UI;

public sealed class MainForm : Form
{
    private readonly ListBox _macroList = new() { Dock = DockStyle.Fill };
    private readonly Button _newButton = new() { Text = "New Macro", Dock = DockStyle.Top, Height = 32 };
    private readonly Button _deleteButton = new() { Text = "Delete Macro", Dock = DockStyle.Top, Height = 32 };
    private readonly Button _editButton = new() { Text = "Edit in Notepad", Dock = DockStyle.Top, Height = 32 };
    private readonly Button _recordButton = new() { Text = "Start Recording", Dock = DockStyle.Top, Height = 40 };
    private readonly Button _stopButton = new() { Text = "Stop Recording", Dock = DockStyle.Top, Height = 40, Enabled = false };
    private readonly Button _playButton = new() { Text = "Play Macro", Dock = DockStyle.Top, Height = 40 };
    private readonly Button _reviewButton = new() { Text = "Review Screenshots", Dock = DockStyle.Top, Height = 32 };

    private readonly NumericUpDown _thresholdInput = new() { Minimum = 50, Maximum = 100, DecimalPlaces = 1, Increment = 0.5m, Value = 99, Dock = DockStyle.Top };
    private readonly Label _thresholdLabel = new() { Text = "Similarity threshold (%):", Dock = DockStyle.Top, Height = 20 };

    private readonly NumericUpDown _repeatInput = new() { Minimum = 1, Maximum = 999, DecimalPlaces = 0, Increment = 1, Value = 1, Dock = DockStyle.Top };
    private readonly Label _repeatLabel = new() { Text = "Repeat count:", Dock = DockStyle.Top, Height = 20 };

    private readonly NumericUpDown _speedInput = new() { Minimum = 0.1m, Maximum = 10m, DecimalPlaces = 1, Increment = 0.1m, Value = 1.0m, Dock = DockStyle.Top };
    private readonly Label _speedLabel = new() { Text = "Speed (1.0 = as recorded, 10.0 = no delay):", Dock = DockStyle.Top, Height = 32 };

    private readonly NumericUpDown _mouseSpeedInput = new() { Minimum = 0, Maximum = 20000, DecimalPlaces = 0, Increment = 100, Value = 1600, Dock = DockStyle.Top };
    private readonly Label _mouseSpeedLabel = new() { Text = "Mouse move speed (px/sec, 0 = instant jump):", Dock = DockStyle.Top, Height = 32 };

    private readonly Label _statusLabel = new() { Dock = DockStyle.Bottom, Height = 46, Text = "Ready.", TextAlign = System.Drawing.ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0) };

    private MacroRecorder? _recorder;
    private string? _recordingMacroName;
    private bool _isPlaying;
    private bool _loadingSettings;

    public MainForm()
    {
        Text = "SmartMacroAutomation";
        Width = 640;
        Height = 560;
        StartPosition = FormStartPosition.CenterScreen;

        var leftPanel = new Panel { Dock = DockStyle.Left, Width = 240, Padding = new Padding(8), AutoScroll = true };
        // Added in reverse order because Dock = Top stacks each new control above the previous one.
        leftPanel.Controls.Add(_editButton);
        leftPanel.Controls.Add(_reviewButton);
        leftPanel.Controls.Add(_playButton);
        leftPanel.Controls.Add(_mouseSpeedInput);
        leftPanel.Controls.Add(_mouseSpeedLabel);
        leftPanel.Controls.Add(_speedInput);
        leftPanel.Controls.Add(_speedLabel);
        leftPanel.Controls.Add(_repeatInput);
        leftPanel.Controls.Add(_repeatLabel);
        leftPanel.Controls.Add(_stopButton);
        leftPanel.Controls.Add(_recordButton);
        leftPanel.Controls.Add(_thresholdInput);
        leftPanel.Controls.Add(_thresholdLabel);
        leftPanel.Controls.Add(_deleteButton);
        leftPanel.Controls.Add(_newButton);

        var centerPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        centerPanel.Controls.Add(_macroList);
        var listLabel = new Label { Text = "Macros (each is a folder under .\\Macros):", Dock = DockStyle.Top, Height = 20 };
        centerPanel.Controls.Add(listLabel);

        Controls.Add(centerPanel);
        Controls.Add(leftPanel);
        Controls.Add(_statusLabel);

        _newButton.Click += NewButton_Click;
        _deleteButton.Click += DeleteButton_Click;
        _editButton.Click += EditButton_Click;
        _recordButton.Click += RecordButton_Click;
        _stopButton.Click += StopButton_Click;
        _playButton.Click += PlayButton_Click;
        _reviewButton.Click += ReviewButton_Click;
        _macroList.SelectedIndexChanged += (_, _) => LoadSelectedMacroSettings();
        _thresholdInput.ValueChanged += (_, _) => SaveCurrentSettings();
        _repeatInput.ValueChanged += (_, _) => SaveCurrentSettings();
        _speedInput.ValueChanged += (_, _) => SaveCurrentSettings();
        _mouseSpeedInput.ValueChanged += (_, _) => SaveCurrentSettings();
        Load += (_, _) => RefreshMacroList();
    }

    private void RefreshMacroList()
    {
        string? selected = _macroList.SelectedItem as string;
        _macroList.Items.Clear();
        foreach (var name in MacroStorage.ListMacros())
            _macroList.Items.Add(name);

        if (selected != null && _macroList.Items.Contains(selected))
            _macroList.SelectedItem = selected;
        else
            LoadSelectedMacroSettings();
    }

    private string? SelectedMacroName => _macroList.SelectedItem as string;

    /// <summary>Populates the settings controls from the selected macro's saved values, without triggering a re-save.</summary>
    private void LoadSelectedMacroSettings()
    {
        var macro = SelectedMacroName is { } name ? MacroStorage.LoadMacro(name) : null;

        _loadingSettings = true;
        try
        {
            _thresholdInput.Value = (decimal)Math.Clamp(macro?.SimilarityThreshold ?? 99.0, (double)_thresholdInput.Minimum, (double)_thresholdInput.Maximum);
            _repeatInput.Value = Math.Clamp(macro?.RepeatCount ?? 1, (int)_repeatInput.Minimum, (int)_repeatInput.Maximum);
            _speedInput.Value = (decimal)Math.Clamp(macro?.Speed ?? 1.0, (double)_speedInput.Minimum, (double)_speedInput.Maximum);
            _mouseSpeedInput.Value = (decimal)Math.Clamp(macro?.MouseMoveSpeed ?? 1600.0, (double)_mouseSpeedInput.Minimum, (double)_mouseSpeedInput.Maximum);
        }
        finally
        {
            _loadingSettings = false;
        }
    }

    /// <summary>Persists the threshold/repeat/speed controls to the selected macro's actions.json immediately.</summary>
    private void SaveCurrentSettings()
    {
        if (_loadingSettings) return;
        if (SelectedMacroName is not { } name) return;
        if (_recorder != null || _isPlaying) return;

        var macro = MacroStorage.LoadMacro(name);
        if (macro == null) return;

        macro.SimilarityThreshold = (double)_thresholdInput.Value;
        macro.RepeatCount = (int)_repeatInput.Value;
        macro.Speed = (double)_speedInput.Value;
        macro.MouseMoveSpeed = (double)_mouseSpeedInput.Value;
        MacroStorage.SaveMacro(macro);
    }

    private void NewButton_Click(object? sender, EventArgs e)
    {
        using var prompt = new TextInputForm("New Macro", "Macro name:");
        if (prompt.ShowDialog(this) != DialogResult.OK) return;

        string name = prompt.Value.Trim();
        if (string.IsNullOrEmpty(name))
        {
            MessageBox.Show(this, "Please enter a name.", "Invalid name", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (MacroStorage.Exists(name))
        {
            MessageBox.Show(this, "A macro with that name already exists.", "Duplicate name", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var macro = new Macro
        {
            Name = name,
            SimilarityThreshold = (double)_thresholdInput.Value,
            RepeatCount = (int)_repeatInput.Value,
            Speed = (double)_speedInput.Value,
            MouseMoveSpeed = (double)_mouseSpeedInput.Value
        };
        MacroStorage.SaveMacro(macro);
        RefreshMacroList();
        _macroList.SelectedItem = name;
    }

    private void DeleteButton_Click(object? sender, EventArgs e)
    {
        if (SelectedMacroName is not { } name) return;

        var result = MessageBox.Show(this, $"Delete macro '{name}'? This removes its folder and screenshots.",
            "Confirm delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (result != DialogResult.Yes) return;

        MacroStorage.DeleteMacro(name);
        RefreshMacroList();
    }

    private void EditButton_Click(object? sender, EventArgs e)
    {
        if (SelectedMacroName is not { } name) return;

        string path = Path.Combine(MacroStorage.GetMacroFolder(name), "actions.json");
        if (!File.Exists(path))
        {
            MessageBox.Show(this, "This macro has no actions.json yet. Record something first.", "Nothing to edit", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo("notepad.exe", $"\"{path}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not open Notepad: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RecordButton_Click(object? sender, EventArgs e)
    {
        if (SelectedMacroName is not { } name)
        {
            MessageBox.Show(this, "Select or create a macro first.", "No macro selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _recordingMacroName = name;
        _recorder = new MacroRecorder();
        _recorder.Start();

        SetRecordingState(true);
        _statusLabel.Text = $"Recording '{name}'... perform your mouse and keyboard actions. Click Stop Recording when done.";
    }

    private void StopButton_Click(object? sender, EventArgs e)
    {
        if (_recorder == null || _recordingMacroName == null) return;

        _recorder.Stop();

        var macro = MacroStorage.LoadMacro(_recordingMacroName) ?? new Macro { Name = _recordingMacroName };
        macro.SimilarityThreshold = (double)_thresholdInput.Value;
        macro.RepeatCount = (int)_repeatInput.Value;
        macro.Speed = (double)_speedInput.Value;
        macro.MouseMoveSpeed = (double)_mouseSpeedInput.Value;

        string imagesFolder = MacroStorage.GetImagesFolder(_recordingMacroName);
        Directory.CreateDirectory(imagesFolder);

        var actions = _recorder.Actions.ToList();
        for (int i = 0; i < actions.Count; i++)
        {
            if (actions[i].Type == ActionType.MouseClick && _recorder.CapturedImages.TryGetValue(i, out var bitmap))
            {
                string fileName = $"click_{i:0000}.png";
                bitmap.Save(Path.Combine(imagesFolder, fileName), ImageFormat.Png);
                actions[i].ImageFile = fileName;
                bitmap.Dispose();
            }
        }

        macro.Actions = actions;
        MacroStorage.SaveMacro(macro);

        SetRecordingState(false);
        _statusLabel.Text = $"Recording saved: {actions.Count} action(s) in '{_recordingMacroName}'.";
        _recordingMacroName = null;
        _recorder = null;
    }

    private void SetRecordingState(bool recording)
    {
        _recordButton.Enabled = !recording;
        _stopButton.Enabled = recording;
        _newButton.Enabled = !recording;
        _deleteButton.Enabled = !recording;
        _editButton.Enabled = !recording;
        _playButton.Enabled = !recording;
        _reviewButton.Enabled = !recording;
        _macroList.Enabled = !recording;
        _thresholdInput.Enabled = !recording;
        _repeatInput.Enabled = !recording;
        _speedInput.Enabled = !recording;
    }

    private void ReviewButton_Click(object? sender, EventArgs e)
    {
        if (SelectedMacroName is not { } name) return;
        var macro = MacroStorage.LoadMacro(name);
        if (macro == null) return;

        using var reviewForm = new ReviewForm(macro);
        reviewForm.ShowDialog(this);
    }

    private void PlayButton_Click(object? sender, EventArgs e)
    {
        if (_isPlaying) return;
        if (SelectedMacroName is not { } name) return;

        var macro = MacroStorage.LoadMacro(name);
        if (macro == null || macro.Actions.Count == 0)
        {
            MessageBox.Show(this, "This macro has no recorded actions.", "Nothing to play", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _isPlaying = true;
        _playButton.Enabled = false;
        _recordButton.Enabled = false;
        _statusLabel.Text = $"Playing '{name}'...";

        var player = new MacroPlayer(macro)
        {
            OnStep = (repeatIndex, repeatCount, actionIndex, actionCount, action) =>
            {
                Invoke(new MethodInvoker(() => _statusLabel.Text =
                    $"Playing '{name}': repeat {repeatIndex + 1}/{repeatCount}, step {actionIndex + 1}/{actionCount} ({action.Type})"));
            },
            OnMismatch = (action, similarity, threshold, reference, current) =>
            {
                return (MismatchDecision)Invoke(new Func<MismatchDecision>(() =>
                {
                    using var form = new MismatchForm(similarity, threshold, reference, current);
                    form.ShowDialog(this);
                    return form.Decision;
                }))!;
            }
        };

        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                player.Play();
            }
            catch (Exception ex)
            {
                Invoke(new MethodInvoker(() => MessageBox.Show(this, $"Playback error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)));
            }
            finally
            {
                Invoke(new MethodInvoker(() =>
                {
                    _isPlaying = false;
                    _playButton.Enabled = true;
                    _recordButton.Enabled = true;
                    _statusLabel.Text = player.IsCancelled ? $"Playback of '{name}' stopped by user." : $"Playback of '{name}' finished.";
                }));
            }
        });
    }
}
