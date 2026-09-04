namespace SmartMacroAutomation.UI;

internal sealed class TextInputForm : Form
{
    private readonly TextBox _textBox = new() { Left = 12, Top = 35, Width = 300 };

    public string Value => _textBox.Text;

    public TextInputForm(string title, string label)
    {
        Text = title;
        Width = 340;
        Height = 140;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;

        var lbl = new Label { Text = label, Left = 12, Top = 12, Width = 300 };

        var okButton = new Button { Text = "OK", Left = 140, Top = 65, Width = 80, DialogResult = DialogResult.OK };
        var cancelButton = new Button { Text = "Cancel", Left = 230, Top = 65, Width = 80, DialogResult = DialogResult.Cancel };

        Controls.Add(lbl);
        Controls.Add(_textBox);
        Controls.Add(okButton);
        Controls.Add(cancelButton);

        AcceptButton = okButton;
        CancelButton = cancelButton;
    }
}
