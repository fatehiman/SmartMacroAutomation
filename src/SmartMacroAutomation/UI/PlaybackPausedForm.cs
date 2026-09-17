using SmartMacroAutomation.Playback;

namespace SmartMacroAutomation.UI;

/// <summary>
/// Shown when the user presses Esc during playback. Playback has already stopped by the
/// time this dialog appears; the user decides whether to resume or stop for good.
/// </summary>
internal sealed class PlaybackPausedForm : Form
{
    public MismatchDecision Decision { get; private set; } = MismatchDecision.Stop;

    public PlaybackPausedForm()
    {
        Text = "Playback stopped";
        Width = 360;
        Height = 170;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        TopMost = true;

        var message = new Label
        {
            Text = "Playback was stopped because Esc was pressed.\n\nDo you want to continue this macro?",
            Left = 12,
            Top = 12,
            Width = 320,
            Height = 70
        };

        var continueButton = new Button { Text = "Continue", Left = 70, Top = 95, Width = 100 };
        continueButton.Click += (_, _) => { Decision = MismatchDecision.Continue; Close(); };

        var stopButton = new Button { Text = "Stop", Left = 180, Top = 95, Width = 100 };
        stopButton.Click += (_, _) => { Decision = MismatchDecision.Stop; Close(); };

        Controls.Add(message);
        Controls.Add(continueButton);
        Controls.Add(stopButton);
        AcceptButton = continueButton;
        CancelButton = stopButton;
    }
}
