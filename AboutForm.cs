using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace PiHoleTray;

class AboutForm : Form
{
    public AboutForm()
    {
        Text            = "About Pi-Hole Tray";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        ShowInTaskbar   = false;
        StartPosition   = FormStartPosition.CenterScreen;
        AutoScaleMode   = AutoScaleMode.Dpi;
        ClientSize      = new Size(420, 310);

        var table = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 1,
            RowCount    = 7,
            Padding     = new Padding(20, 16, 20, 16),
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        for (int i = 0; i < 7; i++)
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // App icon
        var pb = new PictureBox
        {
            Width    = 48,
            Height   = 48,
            SizeMode = PictureBoxSizeMode.Zoom,
            Anchor   = AnchorStyles.None,
            Margin   = new Padding(0, 0, 0, 10),
        };
        try
        {
            var path = Environment.ProcessPath ?? Application.ExecutablePath;
            pb.Image = Icon.ExtractAssociatedIcon(path)?.ToBitmap();
        }
        catch { }

        // Name
        var lblName = new Label
        {
            Text      = "Pi-Hole Tray",
            Font      = new Font("Segoe UI", 15f, FontStyle.Bold),
            AutoSize  = true,
            Anchor    = AnchorStyles.None,
            Margin    = new Padding(0, 0, 0, 2),
        };

        // Version
        var lblVer = new Label
        {
            Text      = "Version 2.1",
            Font      = new Font("Segoe UI", 9f),
            ForeColor = Color.Gray,
            AutoSize  = true,
            Anchor    = AnchorStyles.None,
            Margin    = new Padding(0, 0, 0, 16),
        };

        // GitHub link
        var lnkGit = new LinkLabel
        {
            Text     = "github.com/Euphonique/Pi-Hole-Tray",
            AutoSize = true,
            Anchor   = AnchorStyles.None,
            Margin   = new Padding(0, 0, 0, 10),
        };
        lnkGit.LinkClicked += (_, _) => OpenUrl("https://github.com/Euphonique/Pi-Hole-Tray");

        // Author
        var lblBy = new Label
        {
            Text      = "Made with \u2665 by Pascal Pagel",
            Font      = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(60, 60, 60),
            AutoSize  = true,
            Anchor    = AnchorStyles.None,
            Margin    = new Padding(0, 0, 0, 2),
        };

        // Website
        var lnkWeb = new LinkLabel
        {
            Text     = "www.pascalpagel.de",
            AutoSize = true,
            Anchor   = AnchorStyles.None,
            Margin   = new Padding(0, 0, 0, 20),
        };
        lnkWeb.LinkClicked += (_, _) => OpenUrl("https://www.pascalpagel.de");

        // OK button
        var btnOk = new Button
        {
            Text      = "OK",
            Width     = 80,
            Height    = 28,
            Anchor    = AnchorStyles.None,
            FlatStyle = FlatStyle.System,
            Margin    = new Padding(0),
        };
        btnOk.Click += (_, _) => Close();
        AcceptButton = btnOk;

        table.Controls.Add(pb,      0, 0);
        table.Controls.Add(lblName, 0, 1);
        table.Controls.Add(lblVer,  0, 2);
        table.Controls.Add(lnkGit,  0, 3);
        table.Controls.Add(lblBy,   0, 4);
        table.Controls.Add(lnkWeb,  0, 5);
        table.Controls.Add(btnOk,   0, 6);

        Controls.Add(table);
    }

    private static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { }
    }
}
