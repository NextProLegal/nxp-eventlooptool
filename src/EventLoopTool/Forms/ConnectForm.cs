using EventLoopTool.Models;
using EventLoopTool.Data;

namespace EventLoopTool.Forms;

public class ConnectForm : Form
{
    private readonly ListBox _lstProfiles;
    private readonly TextBox _txtConnStr;
    private readonly Button _btnConnect;
    private readonly Button _btnEdit;
    private readonly Button _btnDelete;
    private List<ConnectionProfile> _profiles = new();

    public ConnectionProfile? SelectedProfile { get; private set; }

    public ConnectForm()
    {
        Text = "Event Loop Tool \u2014 Connect";
        Size = new Size(560, 380);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        Font = new Font("Segoe UI", 9f);

        _lstProfiles = new ListBox
        {
            Location = new Point(16, 16),
            Size = new Size(300, 200),
        };
        _lstProfiles.SelectedIndexChanged += (_, _) => UpdateButtons();
        _lstProfiles.DoubleClick += (_, _) => { if (_lstProfiles.SelectedItem != null) DoConnect(); };

        var lblConn = new Label
        {
            Text = "Connection string:",
            Location = new Point(16, 226),
            AutoSize = true,
            ForeColor = Color.FromArgb(107, 101, 96),
        };
        _txtConnStr = new TextBox
        {
            Location = new Point(16, 246),
            Size = new Size(510, 23),
            ReadOnly = true,
            BackColor = Color.FromArgb(250, 250, 249),
        };

        _btnConnect = new Button { Text = "Connect", Size = new Size(90, 32), Location = new Point(340, 16) };
        _btnConnect.Click += (_, _) => DoConnect();

        var btnCancel = new Button { Text = "Cancel", Size = new Size(90, 32), Location = new Point(340, 56) };
        btnCancel.Click += (_, _) => Close();

        var btnNew = new Button { Text = "New...", Size = new Size(90, 32), Location = new Point(340, 112) };
        btnNew.Click += BtnNew_Click;

        _btnEdit = new Button { Text = "Edit...", Size = new Size(90, 32), Location = new Point(340, 152) };
        _btnEdit.Click += BtnEdit_Click;

        _btnDelete = new Button { Text = "Delete", Size = new Size(90, 32), Location = new Point(340, 192) };
        _btnDelete.Click += BtnDelete_Click;

        AcceptButton = _btnConnect;
        CancelButton = btnCancel;
        Controls.AddRange([_lstProfiles, lblConn, _txtConnStr, _btnConnect, btnCancel, btnNew, _btnEdit, _btnDelete]);

        Load += (_, _) =>
        {
            _profiles = ProfileStore.Load();
            RefreshList();
        };
    }

    private void RefreshList()
    {
        _lstProfiles.Items.Clear();
        foreach (var p in _profiles) _lstProfiles.Items.Add(p);
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        bool sel = _lstProfiles.SelectedItem != null;
        _btnConnect.Enabled = sel;
        _btnEdit.Enabled = sel;
        _btnDelete.Enabled = sel;
        _txtConnStr.Text = sel
            ? ((ConnectionProfile)_lstProfiles.SelectedItem!).ConnectionString
            : "";
    }

    private void BtnNew_Click(object? sender, EventArgs e)
    {
        using var form = new EditProfileForm();
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            _profiles.Add(form.Profile);
            ProfileStore.Save(_profiles);
            RefreshList();
            _lstProfiles.SelectedItem = form.Profile;
        }
    }

    private void BtnEdit_Click(object? sender, EventArgs e)
    {
        var profile = (ConnectionProfile)_lstProfiles.SelectedItem!;
        using var form = new EditProfileForm(profile);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            profile.Name = form.Profile.Name;
            profile.Server = form.Profile.Server;
            profile.Database = form.Profile.Database;
            profile.UseWindowsAuth = form.Profile.UseWindowsAuth;
            profile.UserID = form.Profile.UserID;
            profile.Password = form.Profile.Password;
            ProfileStore.Save(_profiles);
            RefreshList();
        }
    }

    private void BtnDelete_Click(object? sender, EventArgs e)
    {
        var profile = (ConnectionProfile)_lstProfiles.SelectedItem!;
        if (MessageBox.Show($"Remove connection '{profile.Name}'?", "Confirm",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            _profiles.Remove(profile);
            ProfileStore.Save(_profiles);
            RefreshList();
        }
    }

    private void DoConnect()
    {
        SelectedProfile = (ConnectionProfile)_lstProfiles.SelectedItem!;
        DialogResult = DialogResult.OK;
        Close();
    }
}
