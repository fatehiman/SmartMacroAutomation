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
    private readonly Button _recordButton = new() { Text = "Start Recording", Dock = DockStyle.Top, Height = 40 };
    private readonly Button _stopButton = new() { Text = "Stop Recording", Dock = DockStyle.Top, Height = 40, Enabled = false };
    private readonly Button _playButton = new() { Text = "Play Macro", Dock = DockStyle.Top, Height = 40 };
    private readonly Button _reviewButton = new() { Text = "Review Screenshots", Dock = DockStyle.Top, Height = 32 };
    private readonly NumericUpDown _thresholdInput = new() { Minimum = 50, Maximum = 100, DecimalPlaces = 1, Increment = 0.5m, Value = 99, Dock = DockStyle.Top };
    private readonly Label _thresholdLabel = new() { Text = "Similarity threshold (%):", Dock = DockStyle.Top, Height = 20 };
    private readonly Label _statusLabel = new() { Dock = DockStyle.Bottom, Height = 46, Text = "Ready.", TextAlign = System.Drawing.ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0) };

    private MacroRecorder? _recorder;
    private string? _recordingMacroName;
    private bool _isPlaying;

    public MainForm()
    {
        Text = "SmartMacroAutomation";
        Width = 640;
        Height = 480;
        StartPosition = FormStartPosition.CenterScreen;

        var leftPanel = new Panel { Dock = DockStyle.Left, Width = 220, Padding = new Padding(8) };
        leftPanel.Controls.Add(_reviewButton);
        leftPanel.Controls.Add(_playButton);
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
        _recordButton.Click += RecordButton_Click;
        _stopButton.Click += StopButton_Click;
        _playButton.Click += PlayButton_Click;
        _reviewButton.Click += ReviewButton_Click;
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
    }

    private string? SelectedMacroName => _macroList.SelectedItem as string;

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

        var macro = new Macro { Name = name, SimilarityThreshold = (double)_thresholdInput.Value };
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
        _playButton.Enabled = !recording;
        _reviewButton.Enabled = !recording;
        _macroList.Enabled = !recording;
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
            OnStep = (index, total, action) =>
            {
                Invoke(new MethodInvoker(() => _statusLabel.Text = $"Playing '{name}': step {index + 1}/{total} ({action.Type})"));
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
