using EventLoopTool.Models;

namespace EventLoopTool.Forms;

public class AccessDeniedForm : Form
{
    public AccessDeniedForm(string profName, SecurityPermissions permissions)
    {
        Text = "Access Denied \u2014 Event Loop Tool";
        Size = new Size(500, 260);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 9f);

        var icon = new PictureBox
        {
            Image = SystemIcons.Error.ToBitmap(),
            Size = new Size(48, 48),
            Location = new Point(20, 20),
            SizeMode = PictureBoxSizeMode.StretchImage,
        };

        var message = $"Your account ({profName}) does not have the required permissions " +
            $"to use the Event Loop Tool.\n\n" +
            $"Missing: {permissions.MissingDescription}\n\n" +
            "Contact your ProLaw administrator to request access.";

        var lblMessage = new Label
        {
            Text = message,
            Font = new Font("Segoe UI", 10f),
            Location = new Point(82, 16),
            Size = new Size(390, 140),
        };

        var btnClose = new Button
        {
            Text = "Close",
            Size = new Size(90, 30),
            Location = new Point(380, 178),
        };
        btnClose.Click += (_, _) => Close();

        AcceptButton = btnClose;
        Controls.AddRange([icon, lblMessage, btnClose]);
        FormClosed += (_, _) => Application.Exit();
    }
}
