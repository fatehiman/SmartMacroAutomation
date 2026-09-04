using System.Drawing;
using SmartMacroAutomation.Playback;

namespace SmartMacroAutomation.UI;

internal sealed class MismatchForm : Form
{
    public MismatchDecision Decision { get; private set; } = MismatchDecision.Stop;

    public MismatchForm(double similarity, double required, Bitmap reference, Bitmap current)
    {
        Text = "Screen does not match";
        Width = 420;
        Height = 300;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        TopMost = true;

        var message = new Label
        {
            Text = $"The current screen does not match the recorded state.\n\n" +
                   $"Similarity: {similarity:0.0}%\nRequired: {required:0.0}%\n\n" +
                   "Do you want to continue this macro?",
            Left = 12,
            Top = 12,
            Width = 396,
            Height = 90
        };

        var refBox = new PictureBox
        {
            Image = new Bitmap(reference),
            SizeMode = PictureBoxSizeMode.Zoom,
            BorderStyle = BorderStyle.FixedSingle,
            Left = 12,
            Top = 110,
            Width = 150,
            Height = 100
        };
        var refLabel = new Label { Text = "Recorded", Left = 12, Top = 212, Width = 150, TextAlign = ContentAlignment.MiddleCenter };

        var curBox = new PictureBox
        {
            Image = new Bitmap(current),
            SizeMode = PictureBoxSizeMode.Zoom,
            BorderStyle = BorderStyle.FixedSingle,
            Left = 200,
            Top = 110,
            Width = 150,
            Height = 100
        };
        var curLabel = new Label { Text = "Current", Left = 200, Top = 212, Width = 150, TextAlign = ContentAlignment.MiddleCenter };

        var continueButton = new Button { Text = "Continue", Left = 130, Top = 235, Width = 100 };
        continueButton.Click += (_, _) => { Decision = MismatchDecision.Continue; Close(); };

        var stopButton = new Button { Text = "Stop", Left = 240, Top = 235, Width = 100 };
        stopButton.Click += (_, _) => { Decision = MismatchDecision.Stop; Close(); };

        Controls.Add(message);
        Controls.Add(refBox);
        Controls.Add(refLabel);
        Controls.Add(curBox);
        Controls.Add(curLabel);
        Controls.Add(continueButton);
        Controls.Add(stopButton);
        AcceptButton = continueButton;
        CancelButton = stopButton;
    }
}
