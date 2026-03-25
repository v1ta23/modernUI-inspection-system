using App.WinForms.Controllers;
using System.Runtime.InteropServices;

namespace App.WinForms.Views;

internal sealed class LoginForm : Form
{
    private static readonly Color WindowBackground = Color.FromArgb(240, 248, 255);
    private static readonly Color ShellBackground = Color.FromArgb(252, 253, 255);
    private static readonly Color InfoBackground = Color.FromArgb(232, 236, 241);
    private static readonly Color InputBackground = Color.FromArgb(246, 248, 250);
    private static readonly Color BorderColor = Color.FromArgb(210, 217, 224);
    private static readonly Color HeaderTint = Color.FromArgb(230, 241, 249);
    private static readonly Color HeaderBorder = Color.FromArgb(201, 217, 228);
    private static readonly Color AccentColor = Color.FromArgb(109, 126, 150);
    private static readonly Color AccentHoverColor = Color.FromArgb(97, 114, 137);
    private static readonly Color TextPrimary = Color.FromArgb(44, 54, 65);
    private static readonly Color TextSecondary = Color.FromArgb(96, 108, 120);
    private static readonly Color TextMuted = Color.FromArgb(125, 136, 146);

    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;
    private const int DwmwaSystemBackdropType = 38;
    private const int DwmsbtTransientWindow = 3;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);

    private readonly LoginController _controller;
    private readonly AppCompositionRoot _compositionRoot;
    private readonly TextBox _accountTextBox;
    private readonly TextBox _passwordTextBox;
    private readonly CheckBox _rememberCheckBox;
    private readonly CheckBox _showPasswordCheckBox;

    public LoginForm(LoginController controller, AppCompositionRoot compositionRoot)
    {
        _controller = controller;
        _compositionRoot = compositionRoot;

        Text = "\u767b\u5f55";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = true;
        ClientSize = new Size(980, 620);
        MinimumSize = new Size(860, 540);
        Font = new Font("Microsoft YaHei UI", 9F);
        BackColor = WindowBackground;
        Padding = new Padding(24);

        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);

        _accountTextBox = CreateInputTextBox("\u8bf7\u8f93\u5165\u8d26\u53f7");
        _passwordTextBox = CreateInputTextBox("\u8bf7\u8f93\u5165\u5bc6\u7801", usePasswordChar: true);
        _rememberCheckBox = CreateOptionCheckBox("\u8bb0\u4f4f\u5bc6\u7801");
        _showPasswordCheckBox = CreateOptionCheckBox("\u663e\u793a\u5bc6\u7801");
        _showPasswordCheckBox.CheckedChanged += (_, _) =>
        {
            _passwordTextBox.UseSystemPasswordChar = !_showPasswordCheckBox.Checked;
        };

        Controls.Add(BuildShell());

        Load += OnLoad;
        Shown += (_, _) => _accountTextBox.Focus();
        HandleCreated += (_, _) => ApplyWindowChrome();
    }

    private Control BuildShell()
    {
        var borderPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = BorderColor,
            Padding = new Padding(1),
            Margin = new Padding(0)
        };

        var contentHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ShellBackground,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };

        var headerBand = BuildHeaderBand();
        var shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = ShellBackground,
            ColumnCount = 3,
            Margin = new Padding(0),
            Padding = new Padding(0),
            RowCount = 1
        };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34F));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 1F));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 66F));

        shell.Controls.Add(BuildInfoPanel(), 0, 0);
        shell.Controls.Add(new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = BorderColor,
            Margin = new Padding(0)
        }, 1, 0);
        shell.Controls.Add(BuildFormPanel(), 2, 0);

        contentHost.Controls.Add(shell);
        contentHost.Controls.Add(headerBand);
        borderPanel.Controls.Add(contentHost);
        return borderPanel;
    }

    private Control BuildHeaderBand()
    {
        var band = new Panel
        {
            Dock = DockStyle.Top,
            Height = 52,
            BackColor = HeaderTint,
            Margin = new Padding(0),
            Padding = new Padding(18, 0, 18, 0)
        };

        band.Controls.Add(new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 1,
            BackColor = HeaderBorder,
            Margin = new Padding(0)
        });

        var title = new Label
        {
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location = new Point(18, 16),
            Text = "\u767b\u5f55\u5165\u53e3"
        };
        band.Controls.Add(title);
        return band;
    }

    private Control BuildInfoPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = InfoBackground,
            Padding = new Padding(34, 36, 34, 36),
            Margin = new Padding(0)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 1,
            Margin = new Padding(0),
            Padding = new Padding(0),
            RowCount = 7
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        layout.Controls.Add(new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = AccentColor,
            Margin = new Padding(0, 0, 0, 12),
            Text = "ACCOUNT"
        }, 0, 0);
        layout.Controls.Add(new Label
        {
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 22F, FontStyle.Bold),
            ForeColor = TextPrimary,
            Margin = new Padding(0, 0, 0, 10),
            MaximumSize = new Size(260, 0),
            Text = "\u8f7b\u91cf\u3001\u6e05\u6670\u3001\u53ef\u76f4\u63a5\u4f7f\u7528"
        }, 0, 1);
        layout.Controls.Add(new Label
        {
            AutoSize = true,
            ForeColor = TextSecondary,
            MaximumSize = new Size(250, 0),
            Margin = new Padding(0),
            Text = "\u4f7f\u7528 AliceBlue \u4e0e\u6d45\u7070\u7684\u5b9e\u8272\u5c42\u6b21\uff0c\u8ba9\u767b\u5f55\u9875\u4fdd\u6301\u6613\u8bfb\u3001\u5b89\u9759\u3001\u53ef\u9760\u3002"
        }, 0, 2);
        layout.Controls.Add(new Label
        {
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
            ForeColor = TextPrimary,
            Margin = new Padding(0, 0, 0, 12),
            Text = "\u8fdb\u5165\u524d\u786e\u8ba4"
        }, 0, 4);
        layout.Controls.Add(BuildChecklist(), 0, 5);
        layout.Controls.Add(new Label
        {
            AutoSize = true,
            Dock = DockStyle.Bottom,
            ForeColor = TextMuted,
            Margin = new Padding(0, 18, 0, 0),
            MaximumSize = new Size(250, 0),
            Text = "\u9996\u6b21\u4f7f\u7528\u8bf7\u5148\u6ce8\u518c\uff0c\u767b\u5f55\u540e\u76f4\u63a5\u8fdb\u5165\u4e3b\u754c\u9762\u3002"
        }, 0, 6);

        panel.Controls.Add(layout);
        return panel;
    }

    private Control BuildChecklist()
    {
        var panel = new Panel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            Margin = new Padding(0)
        };

        var lines = new[]
        {
            "\u2022 \u8d26\u53f7\u5bc6\u7801\u4f7f\u7528\u6807\u51c6\u8f93\u5165\u6846\uff0c\u5bf9\u6bd4\u5145\u8db3",
            "\u2022 \u767b\u5f55\u4e0e\u6ce8\u518c\u5165\u53e3\u4fdd\u6301\u5728\u4e3b\u64cd\u4f5c\u533a",
            "\u2022 \u7a97\u53e3\u53ef\u7f29\u653e\uff0c\u5e03\u5c40\u4e0d\u88c1\u5207"
        };

        var top = 0;
        foreach (var line in lines)
        {
            var label = new Label
            {
                AutoSize = true,
                ForeColor = TextSecondary,
                MaximumSize = new Size(250, 0),
                Location = new Point(0, top),
                Text = line
            };
            panel.Controls.Add(label);
            top += label.PreferredHeight + 10;
        }

        panel.Height = top;
        return panel;
    }

    private Control BuildFormPanel()
    {
        var outer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Padding = new Padding(54, 38, 54, 38),
            Margin = new Padding(0)
        };

        var content = new Panel
        {
            Anchor = AnchorStyles.None,
            BackColor = Color.Transparent,
            Size = new Size(390, 360)
        };
        outer.Controls.Add(content);
        void CenterContent()
        {
            content.Left = Math.Max(0, (outer.ClientSize.Width - content.Width) / 2);
            content.Top = Math.Max(0, (outer.ClientSize.Height - content.Height) / 2);
        }
        outer.Resize += (_, _) => CenterContent();

        var top = 0;
        content.Controls.Add(CreateLabel("SIGN IN", new Font("Segoe UI", 9F, FontStyle.Bold), AccentColor, ref top, 8));
        content.Controls.Add(CreateLabel("\u6b22\u8fce\u767b\u5f55", new Font("Microsoft YaHei UI", 21F, FontStyle.Bold), TextPrimary, ref top, 8));
        content.Controls.Add(CreateWrappedLabel("\u8bf7\u8f93\u5165\u8d26\u53f7\u4e0e\u5bc6\u7801\uff0c\u767b\u5f55\u540e\u53ef\u4ee5\u76f4\u63a5\u7ee7\u7eed\u5f53\u524d\u4e1a\u52a1\u3002", TextSecondary, 390, ref top, 20));
        content.Controls.Add(CreateFieldLabel("\u8d26\u53f7", ref top));
        content.Controls.Add(CreateInputHost(_accountTextBox, ref top));
        content.Controls.Add(CreateFieldLabel("\u5bc6\u7801", ref top));
        content.Controls.Add(CreateInputHost(_passwordTextBox, ref top));
        content.Controls.Add(CreateOptionsPanel(ref top));
        content.Controls.Add(CreateButtonsPanel(ref top));
        content.Controls.Add(CreateWrappedLabel("\u9700\u8981\u65b0\u8d26\u53f7\u65f6\uff0c\u53ef\u4ee5\u76f4\u63a5\u70b9\u51fb\u6ce8\u518c\u3002", TextMuted, 390, ref top, 0));

        content.Size = new Size(content.Width, top);
        CenterContent();

        return outer;
    }

    private Control CreateOptionsPanel(ref int top)
    {
        var rememberSize = _rememberCheckBox.GetPreferredSize(Size.Empty);
        var showSize = _showPasswordCheckBox.GetPreferredSize(Size.Empty);
        _rememberCheckBox.Size = rememberSize;
        _showPasswordCheckBox.Size = showSize;

        var panelHeight = Math.Max(rememberSize.Height, showSize.Height) + 6;

        var panel = new Panel
        {
            Location = new Point(0, top + 2),
            Size = new Size(390, panelHeight)
        };

        _rememberCheckBox.Location = new Point(0, (panelHeight - rememberSize.Height) / 2);
        panel.Controls.Add(_rememberCheckBox);

        _showPasswordCheckBox.Location = new Point(
            panel.Width - _showPasswordCheckBox.Width,
            (panelHeight - showSize.Height) / 2);
        _showPasswordCheckBox.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        panel.Controls.Add(_showPasswordCheckBox);

        top += panelHeight + 22;
        return panel;
    }

    private Control CreateButtonsPanel(ref int top)
    {
        var panel = new Panel
        {
            Location = new Point(0, top),
            Size = new Size(390, 46)
        };

        var registerButton = CreateSecondaryButton("\u6ce8\u518c");
        registerButton.Bounds = new Rectangle(0, 0, 170, 46);
        registerButton.Click += (_, _) =>
        {
            using var registerForm = _compositionRoot.CreateRegisterForm();
            registerForm.ShowDialog(this);
        };

        var loginButton = CreatePrimaryButton("\u767b\u5f55");
        loginButton.Bounds = new Rectangle(180, 0, 210, 46);
        loginButton.Click += OnLoginClicked;
        AcceptButton = loginButton;

        panel.Controls.Add(registerButton);
        panel.Controls.Add(loginButton);
        top += 60;
        return panel;
    }

    private Control CreateFieldLabel(string text, ref int top)
    {
        return CreateLabel(text, new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold), TextPrimary, ref top, 6);
    }

    private Control CreateInputHost(TextBox textBox, ref int top)
    {
        var panel = new Panel
        {
            BackColor = InputBackground,
            BorderStyle = BorderStyle.FixedSingle,
            Location = new Point(0, top),
            Padding = new Padding(14, 11, 14, 11),
            Size = new Size(390, 44)
        };
        textBox.Location = new Point(14, 11);
        textBox.Size = new Size(panel.Width - 28, 22);
        textBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        panel.Controls.Add(textBox);
        panel.Enter += (_, _) => textBox.Focus();
        top += 58;
        return panel;
    }

    private static Control CreateLabel(string text, Font font, Color color, ref int top, int bottomSpacing)
    {
        var label = new Label
        {
            AutoSize = true,
            Font = font,
            ForeColor = color,
            Location = new Point(0, top),
            Text = text
        };
        top += label.PreferredHeight + bottomSpacing;
        return label;
    }

    private static Control CreateWrappedLabel(string text, Color color, int width, ref int top, int bottomSpacing)
    {
        var label = new Label
        {
            AutoSize = true,
            ForeColor = color,
            Location = new Point(0, top),
            MaximumSize = new Size(width, 0),
            Text = text
        };
        top += label.PreferredHeight + bottomSpacing;
        return label;
    }

    private static CheckBox CreateOptionCheckBox(string text)
    {
        return new CheckBox
        {
            AutoSize = true,
            CheckAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Microsoft YaHei UI", 9F),
            ForeColor = TextSecondary,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0, 2, 0, 2),
            TextAlign = ContentAlignment.MiddleLeft,
            Text = text
        };
    }

    private static TextBox CreateInputTextBox(string placeholderText, bool usePasswordChar = false)
    {
        return new TextBox
        {
            BackColor = InputBackground,
            BorderStyle = BorderStyle.None,
            Font = new Font("Microsoft YaHei UI", 11F),
            ForeColor = TextPrimary,
            Margin = new Padding(0),
            PlaceholderText = placeholderText,
            UseSystemPasswordChar = usePasswordChar
        };
    }

    private static Button CreatePrimaryButton(string text)
    {
        var button = new Button
        {
            BackColor = AccentColor,
            Cursor = Cursors.Hand,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold),
            ForeColor = Color.White,
            Text = text
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = AccentHoverColor;
        button.FlatAppearance.MouseDownBackColor = AccentHoverColor;
        return button;
    }

    private static Button CreateSecondaryButton(string text)
    {
        var button = new Button
        {
            BackColor = Color.White,
            Cursor = Cursors.Hand,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold),
            ForeColor = TextPrimary,
            Text = text
        };
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = BorderColor;
        button.FlatAppearance.MouseOverBackColor = InputBackground;
        button.FlatAppearance.MouseDownBackColor = InputBackground;
        return button;
    }

    private void OnLoad(object? sender, EventArgs e)
    {
        var state = _controller.LoadInitialState();
        _accountTextBox.Text = state.Account;
        _passwordTextBox.Text = state.Password;
        _rememberCheckBox.Checked = state.RememberPassword;
    }

    private void ApplyWindowChrome()
    {
        try
        {
            var cornerPreference = 2;
            DwmSetWindowAttribute(Handle, DwmwaWindowCornerPreference, ref cornerPreference, sizeof(int));

            var borderColor = ToColorRef(HeaderBorder);
            DwmSetWindowAttribute(Handle, DwmwaBorderColor, ref borderColor, sizeof(int));

            var captionColor = ToColorRef(HeaderTint);
            DwmSetWindowAttribute(Handle, DwmwaCaptionColor, ref captionColor, sizeof(int));

            var textColor = ToColorRef(TextPrimary);
            DwmSetWindowAttribute(Handle, DwmwaTextColor, ref textColor, sizeof(int));

            var backdropType = DwmsbtTransientWindow;
            DwmSetWindowAttribute(Handle, DwmwaSystemBackdropType, ref backdropType, sizeof(int));
        }
        catch
        {
            // Unsupported DWM attributes fall back to the in-app header band.
        }
    }

    private static int ToColorRef(Color color)
    {
        return color.R | (color.G << 8) | (color.B << 16);
    }

    private void OnLoginClicked(object? sender, EventArgs e)
    {
        try
        {
            var result = _controller.Login(_accountTextBox.Text, _passwordTextBox.Text, _rememberCheckBox.Checked);
            if (!result.Success)
            {
                MessageBox.Show(this, result.Message, "\u767b\u5f55\u5931\u8d25", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var dashboard = _compositionRoot.CreateDashboardForm(result.Account!);
            dashboard.FormClosed += (_, _) => Close();
            Hide();
            dashboard.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"\u767b\u5f55\u8fc7\u7a0b\u4e2d\u53d1\u751f\u9519\u8bef\uff1a{ex.Message}", "\u9519\u8bef", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
