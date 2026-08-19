using Microsoft.Data.SqlClient;
using EventLoopTool.Models;

namespace EventLoopTool.Forms;

public class EditProfileForm : Form
{
    private readonly TextBox _txtName;
    private readonly TextBox _txtServer;
    private readonly TextBox _txtDatabase;
    private readonly RadioButton _rbWindows;
    private readonly RadioButton _rbSql;
    private readonly TextBox _txtUserID;
    private readonly TextBox _txtPassword;
    private readonly CheckBox _chkShowPassword;
    private readonly Label _lblUserID;
    private readonly Label _lblPassword;
    private readonly TextBox _txtPreview;
    private readonly Button _btnTest;

    public ConnectionProfile Profile { get; private set; } = new();

    public EditProfileForm(ConnectionProfile? existing = null)
    {
        Text = "Edit Connection Profile";
        Size = new Size(490, 480);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 9f);
        Padding = new Padding(16);

        // ── Profile name, Server, Database ──────────────────────────────────

        int labelX = 16;
        int fieldX = 120;
        int fieldWidth = 330;
        int y = 16;

        var lblName = new Label { Text = "Profile name:", Location = new Point(labelX, y + 3), AutoSize = true };
        _txtName = new TextBox { Location = new Point(fieldX, y), Width = fieldWidth, Text = existing?.Name ?? "" };
        y += 32;

        var lblServer = new Label { Text = "Server:", Location = new Point(labelX, y + 3), AutoSize = true };
        _txtServer = new TextBox { Location = new Point(fieldX, y), Width = fieldWidth, Text = existing?.Server ?? "" };
        y += 32;

        var lblDatabase = new Label { Text = "Database:", Location = new Point(labelX, y + 3), AutoSize = true };
        _txtDatabase = new TextBox { Location = new Point(fieldX, y), Width = fieldWidth, Text = existing?.Database ?? "" };
        y += 38;

        // ── Authentication group ────────────────────────────────────────────

        var grpAuth = new GroupBox
        {
            Text = "Authentication",
            Location = new Point(labelX, y),
            Size = new Size(fieldX + fieldWidth - labelX, 130),
        };

        _rbWindows = new RadioButton
        {
            Text = "Windows Authentication",
            Location = new Point(16, 24),
            AutoSize = true,
            Checked = existing?.UseWindowsAuth ?? true,
        };

        _rbSql = new RadioButton
        {
            Text = "SQL Server Authentication",
            Location = new Point(16, 48),
            AutoSize = true,
            Checked = existing != null && !existing.UseWindowsAuth,
        };

        _lblUserID = new Label { Text = "User ID:", Location = new Point(40, 78), AutoSize = true };
        _txtUserID = new TextBox
        {
            Location = new Point(120, 75),
            Width = 160,
            Text = existing?.UserID ?? "",
        };

        _lblPassword = new Label { Text = "Password:", Location = new Point(40, 104), AutoSize = true };
        _txtPassword = new TextBox
        {
            Location = new Point(120, 101),
            Width = 160,
            UseSystemPasswordChar = true,
            Text = existing?.Password ?? "",
        };

        _chkShowPassword = new CheckBox
        {
            Text = "Show",
            Location = new Point(290, 103),
            AutoSize = true,
        };
        _chkShowPassword.CheckedChanged += (_, _) =>
            _txtPassword.UseSystemPasswordChar = !_chkShowPassword.Checked;

        grpAuth.Controls.AddRange([_rbWindows, _rbSql, _lblUserID, _txtUserID, _lblPassword, _txtPassword, _chkShowPassword]);

        _rbWindows.CheckedChanged += (_, _) => UpdateAuthFields();
        _rbSql.CheckedChanged += (_, _) => UpdateAuthFields();
        UpdateAuthFields();

        y += grpAuth.Height + 12;

        // ── Connection String Preview ───────────────────────────────────────

        var grpPreview = new GroupBox
        {
            Text = "Connection String Preview",
            Location = new Point(labelX, y),
            Size = new Size(fieldX + fieldWidth - labelX, 64),
        };

        _txtPreview = new TextBox
        {
            Location = new Point(12, 22),
            Width = grpPreview.Width - 28,
            ReadOnly = true,
            BackColor = SystemColors.Window,
            ForeColor = Color.FromArgb(107, 101, 96),
        };
        grpPreview.Controls.Add(_txtPreview);

        y += grpPreview.Height + 12;

        // ── Test Connection button ──────────────────────────────────────────

        _btnTest = new Button
        {
            Text = "Test Connection",
            Location = new Point(labelX, y),
            Size = new Size(120, 30),
        };
        _btnTest.Click += async (_, _) => await TestConnectionAsync();

        // ── OK / Cancel ─────────────────────────────────────────────────────

        var btnOk = new Button
        {
            Text = "OK",
            Size = new Size(80, 30),
            BackColor = Color.FromArgb(96, 55, 164),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
        };
        btnOk.FlatAppearance.BorderSize = 0;
        btnOk.Location = new Point(ClientSize.Width - 180, ClientSize.Height - 46);
        btnOk.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        btnOk.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_txtName.Text))
            {
                MessageBox.Show("Profile name is required.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtName.Focus();
                return;
            }
            if (string.IsNullOrWhiteSpace(_txtServer.Text))
            {
                MessageBox.Show("Server is required.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtServer.Focus();
                return;
            }

            Profile = BuildProfile();
            DialogResult = DialogResult.OK;
            Close();
        };

        var btnCancel = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Size = new Size(80, 30),
        };
        btnCancel.Location = new Point(ClientSize.Width - 92, ClientSize.Height - 46);
        btnCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

        AcceptButton = btnOk;
        CancelButton = btnCancel;

        Controls.AddRange([
            lblName, _txtName,
            lblServer, _txtServer,
            lblDatabase, _txtDatabase,
            grpAuth, grpPreview,
            _btnTest, btnOk, btnCancel,
        ]);

        // Wire up preview updates
        _txtServer.TextChanged += (_, _) => UpdatePreview();
        _txtDatabase.TextChanged += (_, _) => UpdatePreview();
        _rbWindows.CheckedChanged += (_, _) => UpdatePreview();
        _rbSql.CheckedChanged += (_, _) => UpdatePreview();
        _txtUserID.TextChanged += (_, _) => UpdatePreview();
        _txtPassword.TextChanged += (_, _) => UpdatePreview();
        UpdatePreview();
    }

    private void UpdateAuthFields()
    {
        bool sqlAuth = _rbSql.Checked;
        _lblUserID.Enabled = sqlAuth;
        _txtUserID.Enabled = sqlAuth;
        _lblPassword.Enabled = sqlAuth;
        _txtPassword.Enabled = sqlAuth;
        _chkShowPassword.Enabled = sqlAuth;
    }

    private void UpdatePreview()
    {
        _txtPreview.Text = BuildProfile().ConnectionString;
    }

    private ConnectionProfile BuildProfile() => new()
    {
        Name = _txtName.Text.Trim(),
        Server = _txtServer.Text.Trim(),
        Database = _txtDatabase.Text.Trim(),
        UseWindowsAuth = _rbWindows.Checked,
        UserID = _txtUserID.Text.Trim(),
        Password = _txtPassword.Text,
    };

    private async Task TestConnectionAsync()
    {
        _btnTest.Enabled = false;
        _btnTest.Text = "Testing...";
        Cursor = Cursors.WaitCursor;

        try
        {
            var profile = BuildProfile();
            using var conn = new SqlConnection(profile.ConnectionString);
            await Task.Run(() => conn.Open());
            conn.Close();

            MessageBox.Show("Connection successful.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Connection failed:\n\n{ex.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnTest.Text = "Test Connection";
            _btnTest.Enabled = true;
            Cursor = Cursors.Default;
        }
    }
}
