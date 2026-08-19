using EventLoopTool.Models;

namespace EventLoopTool.Forms;

public class EditProfileForm : Form
{
    private readonly TextBox _txtName;
    private readonly TextBox _txtConnStr;

    public ConnectionProfile Profile { get; private set; } = new();

    public EditProfileForm(ConnectionProfile? existing = null)
    {
        Text = existing == null ? "New Connection" : "Edit Connection";
        Size = new Size(520, 220);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 9f);

        var lblName = new Label { Text = "Name:", Location = new Point(16, 20), AutoSize = true };
        _txtName = new TextBox
        {
            Location = new Point(140, 17),
            Width = 340,
            Text = existing?.Name ?? ""
        };

        var lblConn = new Label { Text = "Connection String:", Location = new Point(16, 56), AutoSize = true };
        _txtConnStr = new TextBox
        {
            Location = new Point(140, 53),
            Width = 340,
            Text = existing?.ConnectionString ?? "Server=;Database=;Integrated Security=SSPI;TrustServerCertificate=True"
        };

        var btnOk = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Size = new Size(80, 30),
            Location = new Point(310, 140)
        };
        btnOk.Click += (_, _) =>
        {
            Profile = new ConnectionProfile
            {
                Name = _txtName.Text.Trim(),
                ConnectionString = _txtConnStr.Text.Trim()
            };
        };

        var btnCancel = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Size = new Size(80, 30),
            Location = new Point(400, 140)
        };

        AcceptButton = btnOk;
        CancelButton = btnCancel;
        Controls.AddRange([lblName, _txtName, lblConn, _txtConnStr, btnOk, btnCancel]);
    }
}
