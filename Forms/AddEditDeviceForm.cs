using MikroTikSDN.Models;

namespace MikroTikSDN.Forms;

/// <summary>Formulário para adicionar ou editar um router MikroTik.</summary>
public class AddEditDeviceForm : Form
{
    public RouterDevice Device { get; private set; } = new();

    private readonly TextBox _txtName     = new();
    private readonly TextBox _txtHost     = new();
    private readonly TextBox _txtUser     = new();
    private readonly TextBox _txtPassword = new();
    private readonly NumericUpDown _numPort = new();
    private readonly CheckBox _chkHttps   = new();
    private readonly Button _btnOk        = new();
    private readonly Button _btnCancel    = new();

    public AddEditDeviceForm(RouterDevice? existing = null)
    {
        Text = existing == null ? "Adicionar Dispositivo" : "Editar Dispositivo";
        Size = new Size(380, 310);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9.5f);

        BuildUI();

        if (existing != null)
        {
            _txtName.Text      = existing.Name;
            _txtHost.Text      = existing.Host;
            _txtUser.Text      = existing.Username;
            _txtPassword.Text  = existing.Password;
            _numPort.Value     = existing.Port;
            _chkHttps.Checked  = existing.UseHttps;
        }
    }

    private void BuildUI()
    {
        int y = 18, rowH = 38;
        var fields = new (string Label, Control Ctrl)[]
        {
            ("Nome:",       _txtName),
            ("Host / IP:",  _txtHost),
            ("Utilizador:", _txtUser),
            ("Password:",   _txtPassword),
            ("Porto:",      _numPort),
        };

        _txtPassword.UseSystemPasswordChar = true;
        _numPort.Minimum = 0; _numPort.Maximum = 65535; _numPort.Value = 0;

        foreach (var (label, ctrl) in fields)
        {
            Controls.Add(new Label { Text = label, Location = new Point(14, y + 3), AutoSize = true });
            ctrl.Location = new Point(130, y);
            ctrl.Width = 210;
            Controls.Add(ctrl);
            y += rowH;
        }

        _chkHttps.Text = "Usar HTTPS (recomendado)";
        _chkHttps.Checked = true;
        _chkHttps.Location = new Point(130, y);
        _chkHttps.AutoSize = true;
        Controls.Add(_chkHttps);
        y += rowH;

        var hint = new Label
        {
            Text = "Porto 0 = automático (443 HTTPS / 80 HTTP)",
            Location = new Point(130, y - 20),
            AutoSize = true,
            ForeColor = Color.Gray,
            Font = new Font("Segoe UI", 8f)
        };
        Controls.Add(hint);

        _btnOk.Text = "Guardar";
        _btnOk.Location = new Point(130, y + 10);
        _btnOk.Size = new Size(95, 30);
        _btnOk.BackColor = Color.FromArgb(0, 120, 215);
        _btnOk.ForeColor = Color.White;
        _btnOk.FlatStyle = FlatStyle.Flat;
        _btnOk.FlatAppearance.BorderSize = 0;
        _btnOk.Click += BtnOk_Click;

        _btnCancel.Text = "Cancelar";
        _btnCancel.Location = new Point(235, y + 10);
        _btnCancel.Size = new Size(90, 30);
        _btnCancel.FlatStyle = FlatStyle.Flat;
        _btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        Controls.Add(_btnOk);
        Controls.Add(_btnCancel);
        AcceptButton = _btnOk;
        CancelButton = _btnCancel;
    }

    private void BtnOk_Click(object? s, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_txtHost.Text) || string.IsNullOrWhiteSpace(_txtUser.Text))
        {
            MessageBox.Show("Host e Utilizador são obrigatórios.", "Validação",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Device = new RouterDevice
        {
            Name     = string.IsNullOrWhiteSpace(_txtName.Text) ? _txtHost.Text : _txtName.Text,
            Host     = _txtHost.Text.Trim(),
            Username = _txtUser.Text.Trim(),
            Password = _txtPassword.Text,
            Port     = (int)_numPort.Value,
            UseHttps = _chkHttps.Checked
        };

        DialogResult = DialogResult.OK;
        Close();
    }
}
