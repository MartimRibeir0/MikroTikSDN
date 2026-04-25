using MikroTikSDN.Api;
using MikroTikSDN.Models;
using MikroTikSDN.Services;
using System.Security.Cryptography;
using Chaos.NaCl;
using Newtonsoft.Json;

namespace MikroTikSDN.Forms;

// ── Gerador Matemático Real de Chaves WireGuard (Curve25519) ────────
public static class WireGuardKeyGen
{
    public static (string PrivateKey, string PublicKey) Generate()
    {
        byte[] privKey = new byte[32];
        RandomNumberGenerator.Fill(privKey);

        privKey[0] &= 248;
        privKey[31] &= 127;
        privKey[31] |= 64;

        byte[] pubKey = MontgomeryCurve25519.GetPublicKey(privKey);

        return (Convert.ToBase64String(privKey), Convert.ToBase64String(pubKey));
    }
}

// ── Cofre Local para guardar Chaves Privadas (1 Ficheiro por Router) 
public static class WireGuardKeyStore
{
    private static string GetFilePath(string host)
    {
        var safeHost = string.Join("_", host.Split(Path.GetInvalidFileNameChars()));
        return $"wg_keys_{safeHost}.json";
    }

    public static void Save(string host, string publicKey, string privateKey)
    {
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(publicKey) || string.IsNullOrWhiteSpace(privateKey)) return;

        var path = GetFilePath(host);
        var keys = new Dictionary<string, string>();

        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path);
                keys = JsonConvert.DeserializeObject<Dictionary<string, string>>(json) ?? new();
            }
            catch { }
        }

        keys[publicKey] = privateKey;

        try
        {
            File.WriteAllText(path, JsonConvert.SerializeObject(keys, Formatting.Indented));
        }
        catch { }
    }

    public static string? GetPrivateKey(string host, string publicKey)
    {
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(publicKey)) return null;

        var path = GetFilePath(host);
        if (!File.Exists(path)) return null;

        try
        {
            var json = File.ReadAllText(path);
            var keys = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            if (keys != null && keys.TryGetValue(publicKey, out var priv))
            {
                return priv;
            }
        }
        catch { }

        return null;
    }
}
// ──────────────────────────────────────────────────────────────────

// ════════════════════════════════════════════════════════════════════
//  BASE PANEL
// ════════════════════════════════════════════════════════════════════

public abstract class BasePanel : UserControl
{
    protected readonly MikroTikClient Client;
    protected readonly DataGridView Grid;
    protected readonly FlowLayoutPanel Toolbar;
    protected readonly Label LblLoading;

    protected BasePanel(MikroTikClient client)
    {
        Client = client;
        Padding = new Padding(4);

        Toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 44,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(4),
            BackColor = Color.FromArgb(245, 245, 245)
        };

        LblLoading = new Label
        {
            Text = "A carregar...",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.Gray,
            Font = new Font("Segoe UI", 10f),
            Visible = false
        };

        Grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            RowHeadersVisible = false,
            EnableHeadersVisualStyles = false,
            ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            }
        };

        Controls.Add(Grid);
        Controls.Add(LblLoading);
        Controls.Add(Toolbar);

        Load += async (_, _) => await LoadDataAsync();
    }

    protected abstract Task LoadDataAsync();

    protected Button AddToolbarButton(string text, Color? color = null)
    {
        var btn = new Button
        {
            Text = text,
            Height = 30,
            AutoSize = true,
            Padding = new Padding(10, 0, 10, 0),
            BackColor = color ?? Color.FromArgb(60, 60, 60),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0, 4, 6, 0),
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 0;
        Toolbar.Controls.Add(btn);
        return btn;
    }

    protected void ShowError(Exception ex)
    {
        MessageBox.Show(ex.Message, "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    protected string? SelectedId()
    {
        if (Grid.SelectedRows.Count == 0) return null;
        var cell = Grid.SelectedRows[0].Cells[".id"];
        return cell?.Value?.ToString();
    }
}

// ════════════════════════════════════════════════════════════════════
//  INTERFACES PANEL
// ════════════════════════════════════════════════════════════════════

public class InterfacesPanel : BasePanel
{
    private readonly InterfaceService _svc;

    public InterfacesPanel(MikroTikClient client) : base(client)
    {
        _svc = new InterfaceService(client);

        AddToolbarButton("✔ Ativar", Color.FromArgb(0, 140, 80)).Click += async (_, _) => await ToggleAsync(true);
        AddToolbarButton("✕ Desativar", Color.FromArgb(160, 40, 40)).Click += async (_, _) => await ToggleAsync(false);
        AddToolbarButton("↺ Atualizar", Color.FromArgb(0, 100, 180)).Click += async (_, _) => await LoadDataAsync();

        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = ".id", HeaderText = "ID", Visible = false });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "name", HeaderText = "Nome", FillWeight = 25 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "type", HeaderText = "Tipo", FillWeight = 20 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "mac-address", HeaderText = "MAC", FillWeight = 25 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "mtu", HeaderText = "MTU", FillWeight = 10 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "running", HeaderText = "A correr", FillWeight = 10 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "disabled", HeaderText = "Desativada", FillWeight = 10 });
    }

    protected override async Task LoadDataAsync()
    {
        try
        {
            var list = await _svc.GetAllAsync();
            Grid.Rows.Clear();
            foreach (var i in list)
            {
                Grid.Rows.Add(i.Id, i.Name, i.Type, i.MacAddress, i.Mtu, i.Running ? "✔" : "✘", i.Disabled ? "Sim" : "Não");
            }
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private async Task ToggleAsync(bool enable)
    {
        var id = SelectedId();
        if (id == null)
        {
            MessageBox.Show("Seleciona uma interface.");
            return;
        }

        try
        {
            if (enable) await _svc.EnableAsync(id);
            else await _svc.DisableAsync(id);
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }
}

// ════════════════════════════════════════════════════════════════════
//  WIRELESS PANEL
// ════════════════════════════════════════════════════════════════════

public class WirelessPanel : BasePanel
{
    private readonly WirelessService _svc;

    public WirelessPanel(MikroTikClient client) : base(client)
    {
        _svc = new WirelessService(client);

        AddToolbarButton("✎ Editar / Configurar", Color.FromArgb(80, 80, 80)).Click += BtnEdit_Click;
        AddToolbarButton("✔ Ativar", Color.FromArgb(0, 140, 80)).Click += async (_, _) => await ToggleAsync(true);
        AddToolbarButton("✕ Desativar", Color.FromArgb(160, 40, 40)).Click += async (_, _) => await ToggleAsync(false);

        AddToolbarButton("🛡️ Gerir Perfis de Segurança", Color.DarkMagenta).Click += async (_, _) =>
        {
            using var frm = new SecurityProfilesForm(_svc);
            frm.ShowDialog();
            await LoadDataAsync();
        };

        AddToolbarButton("↺ Atualizar", Color.FromArgb(0, 100, 180)).Click += async (_, _) => await LoadDataAsync();

        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = ".id", HeaderText = "ID", Visible = false });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "name", HeaderText = "Nome", FillWeight = 15 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ssid", HeaderText = "SSID", FillWeight = 20 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "disabled", HeaderText = "Disabled", FillWeight = 10 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "sec", HeaderText = "Segurança", FillWeight = 20 });
    }

    protected override async Task LoadDataAsync()
    {
        try
        {
            var list = await _svc.GetInterfacesAsync();
            Grid.Rows.Clear();
            foreach (var i in list)
            {
                Grid.Rows.Add(i.Id, i.Name, i.Ssid, i.Disabled ? "Sim" : "Não", i.SecurityProfile);
            }
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private async void BtnEdit_Click(object? s, EventArgs e)
    {
        var id = SelectedId();
        if (id == null)
        {
            MessageBox.Show("Seleciona uma interface wireless.");
            return;
        }

        try
        {
            var profiles = await _svc.GetSecurityProfilesAsync();
            var row = Grid.SelectedRows[0];
            string currentName = row.Cells["name"].Value?.ToString() ?? "";
            string currentSsid = row.Cells["ssid"].Value?.ToString() ?? "";
            string currentProfile = row.Cells["sec"].Value?.ToString() ?? "default";

            var profileNames = profiles.Select(p => p.Name).ToList();

            using var frm = new WirelessEditForm(currentName, currentSsid, currentProfile, profileNames);
            if (frm.ShowDialog() != DialogResult.OK) return;

            var changes = new Dictionary<string, object>
            {
                { "ssid", frm.Ssid },
                { "security-profile", frm.SecurityProfile }
            };

            await _svc.UpdateInterfaceAsync(id, changes);
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private async Task ToggleAsync(bool enable)
    {
        var id = SelectedId();
        if (id == null) return;

        try
        {
            if (enable) await _svc.EnableInterfaceAsync(id);
            else await _svc.DisableInterfaceAsync(id);
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }
}

public class WirelessEditForm : Form
{
    public string Ssid => _txtSsid.Text.Trim();
    public string SecurityProfile => _cboProfile.Text;

    private readonly TextBox _txtSsid = new();
    private readonly ComboBox _cboProfile = new() { DropDownStyle = ComboBoxStyle.DropDownList };

    public WirelessEditForm(string ifaceName, string currentSsid, string currentProfile, List<string> profiles)
    {
        Text = $"Configurar Wireless: {ifaceName}";
        Size = new Size(340, 200);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9.5f);

        int y = 20;

        Controls.Add(new Label { Text = "SSID (Nome da Rede):", Location = new Point(14, y + 3), AutoSize = true });
        _txtSsid.Location = new Point(150, y);
        _txtSsid.Width = 150;
        _txtSsid.Text = currentSsid;
        Controls.Add(_txtSsid);

        y += 40;

        Controls.Add(new Label { Text = "Perfil de Segurança:", Location = new Point(14, y + 3), AutoSize = true });
        _cboProfile.Location = new Point(150, y);
        _cboProfile.Width = 150;

        foreach (var p in profiles)
        {
            _cboProfile.Items.Add(p);
        }

        if (_cboProfile.Items.Contains(currentProfile))
        {
            _cboProfile.SelectedItem = currentProfile;
        }
        else if (_cboProfile.Items.Count > 0)
        {
            _cboProfile.SelectedIndex = 0;
        }

        Controls.Add(_cboProfile);

        y += 45;

        var ok = new Button
        {
            Text = "Guardar",
            Location = new Point(150, y),
            Size = new Size(150, 30),
            BackColor = Color.DodgerBlue,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };

        ok.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_txtSsid.Text))
            {
                MessageBox.Show("O SSID não pode estar vazio.");
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        };

        Controls.Add(ok);
        AcceptButton = ok;
    }
}

public class SecurityProfilesForm : Form
{
    private readonly WirelessService _svc;
    private readonly DataGridView _grid = new();

    public SecurityProfilesForm(WirelessService svc)
    {
        _svc = svc;
        Text = "Gestão de Perfis de Segurança";
        Size = new Size(600, 400);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9.5f);

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 40,
            Padding = new Padding(5)
        };

        var btnAdd = new Button
        {
            Text = "＋ Novo",
            Height = 30,
            BackColor = Color.DodgerBlue,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };

        btnAdd.Click += async (_, _) =>
        {
            using var frm = new SecurityProfileEditForm();
            if (frm.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    await _svc.CreateSecurityProfileAsync(frm.Profile);
                    await LoadDataAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        };

        var btnEdit = new Button
        {
            Text = "✎ Editar",
            Height = 30,
            BackColor = Color.Gray,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };

        btnEdit.Click += async (_, _) =>
        {
            if (_grid.SelectedRows.Count == 0) return;

            var id = _grid.SelectedRows[0].Cells[".id"].Value?.ToString();

            try
            {
                var allProfs = await _svc.GetSecurityProfilesAsync();
                var existingProf = allProfs.FirstOrDefault(p => p.Id == id);

                if (existingProf == null) return;

                using var frm = new SecurityProfileEditForm(existingProf);
                if (frm.ShowDialog() == DialogResult.OK)
                {
                    var ch = new Dictionary<string, object>
                    {
                        { "name", frm.Profile.Name },
                        { "comment", frm.Profile.Comment },
                        { "mode", frm.Profile.Mode },
                        { "authentication-types", frm.Profile.AuthenticationTypes },
                        { "unicast-ciphers", frm.Profile.UnicastCiphers },
                        { "group-ciphers", frm.Profile.GroupCiphers }
                    };

                    if (!string.IsNullOrWhiteSpace(frm.Profile.Wpa2PreSharedKey))
                    {
                        ch["wpa2-pre-shared-key"] = frm.Profile.Wpa2PreSharedKey;
                    }

                    await _svc.UpdateSecurityProfileAsync(id!, ch);
                    await LoadDataAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        };

        var btnDel = new Button
        {
            Text = "✕ Apagar",
            Height = 30,
            BackColor = Color.Firebrick,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };

        btnDel.Click += async (_, _) =>
        {
            if (_grid.SelectedRows.Count == 0) return;

            var id = _grid.SelectedRows[0].Cells[".id"].Value?.ToString();
            if (MessageBox.Show("Tem a certeza que deseja apagar este perfil?", "Aviso", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                try
                {
                    await _svc.DeleteSecurityProfileAsync(id!);
                    await LoadDataAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        };

        toolbar.Controls.AddRange(new Control[] { btnAdd, btnEdit, btnDel });

        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.BackgroundColor = Color.White;
        _grid.RowHeadersVisible = false;

        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = ".id", Visible = false });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "name", HeaderText = "Nome" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "mode", HeaderText = "Modo" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "auth", HeaderText = "Autenticação" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ciphers", HeaderText = "Cifras" });

        Controls.Add(_grid);
        Controls.Add(toolbar);

        Load += async (_, _) => await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            var p = await _svc.GetSecurityProfilesAsync();
            _grid.Rows.Clear();
            foreach (var x in p)
            {
                _grid.Rows.Add(x.Id, x.Name, x.Mode, x.AuthenticationTypes, x.UnicastCiphers);
            }
        }
        catch { }
    }
}

public class SecurityProfileEditForm : Form
{
    public SecurityProfile Profile { get; private set; } = new();

    private readonly TextBox _txtName = new();
    private readonly ComboBox _cboMode = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox _chkWpaPsk = new() { Text = "WPA PSK", AutoSize = true };
    private readonly CheckBox _chkWpa2Psk = new() { Text = "WPA2 PSK", AutoSize = true };
    private readonly CheckBox _chkAes = new() { Text = "aes ccm", AutoSize = true };
    private readonly CheckBox _chkTkip = new() { Text = "tkip", AutoSize = true };
    private readonly TextBox _txtPass = new();
    private readonly TextBox _txtComment = new();

    public SecurityProfileEditForm(SecurityProfile? existing = null)
    {
        Text = existing == null ? "Novo Perfil de Segurança" : "Editar Perfil de Segurança";
        Size = new Size(420, 390);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9.5f);

        int y = 20;

        _cboMode.Items.AddRange(new[] { "none", "dynamic-keys", "static-keys-required", "static-keys-optional" });

        Controls.Add(new Label { Text = "Nome:", Location = new Point(15, y + 3), AutoSize = true });
        _txtName.Location = new Point(160, y);
        _txtName.Width = 220;
        Controls.Add(_txtName);
        y += 35;

        Controls.Add(new Label { Text = "Mode:", Location = new Point(15, y + 3), AutoSize = true });
        _cboMode.Location = new Point(160, y);
        _cboMode.Width = 220;
        Controls.Add(_cboMode);
        y += 35;

        Controls.Add(new Label { Text = "Authentication Types:", Location = new Point(15, y + 3), AutoSize = true });
        _chkWpaPsk.Location = new Point(160, y);
        _chkWpa2Psk.Location = new Point(250, y);
        Controls.Add(_chkWpaPsk);
        Controls.Add(_chkWpa2Psk);
        y += 35;

        Controls.Add(new Label { Text = "Ciphers (Uni/Group):", Location = new Point(15, y + 3), AutoSize = true });
        _chkAes.Location = new Point(160, y);
        _chkTkip.Location = new Point(250, y);
        Controls.Add(_chkAes);
        Controls.Add(_chkTkip);
        y += 35;

        Controls.Add(new Label { Text = "WPA2 Pre-Shared Key:", Location = new Point(15, y + 3), AutoSize = true });
        _txtPass.Location = new Point(160, y);
        _txtPass.Width = 220;
        Controls.Add(_txtPass);
        y += 35;

        Controls.Add(new Label { Text = "Comentário:", Location = new Point(15, y + 3), AutoSize = true });
        _txtComment.Location = new Point(160, y);
        _txtComment.Width = 220;
        Controls.Add(_txtComment);
        y += 35;

        if (existing != null)
        {
            _txtName.Text = existing.Name;
            _txtComment.Text = existing.Comment;

            if (_cboMode.Items.Contains(existing.Mode))
            {
                _cboMode.SelectedItem = existing.Mode;
            }

            _chkWpaPsk.Checked = existing.AuthenticationTypes.Contains("wpa-psk");
            _chkWpa2Psk.Checked = existing.AuthenticationTypes.Contains("wpa2-psk");

            _chkAes.Checked = existing.UnicastCiphers.Contains("aes-ccm");
            _chkTkip.Checked = existing.UnicastCiphers.Contains("tkip");

            var info = new Label
            {
                Text = "Deixe a Pass em branco para não alterar.",
                Location = new Point(160, y - 5),
                AutoSize = true,
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 8f)
            };
            Controls.Add(info);
            y += 20;
        }
        else
        {
            _cboMode.SelectedItem = "dynamic-keys";
            _chkWpa2Psk.Checked = true;
            _chkAes.Checked = true;
        }

        var btnOk = new Button
        {
            Text = "Guardar",
            Location = new Point(160, y),
            Size = new Size(220, 30),
            BackColor = Color.DodgerBlue,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };

        btnOk.Click += (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(_txtName.Text))
            {
                MessageBox.Show("Nome obrigatório.");
                return;
            }

            if (existing == null && _cboMode.Text == "dynamic-keys" && _txtPass.Text.Length < 8)
            {
                MessageBox.Show("A password WPA2 tem de ter pelo menos 8 caracteres.");
                return;
            }

            var authTypes = new List<string>();
            if (_chkWpaPsk.Checked) authTypes.Add("wpa-psk");
            if (_chkWpa2Psk.Checked) authTypes.Add("wpa2-psk");

            var ciphers = new List<string>();
            if (_chkAes.Checked) ciphers.Add("aes-ccm");
            if (_chkTkip.Checked) ciphers.Add("tkip");

            Profile = new SecurityProfile
            {
                Name = _txtName.Text.Trim(),
                Wpa2PreSharedKey = _txtPass.Text.Trim(),
                Comment = _txtComment.Text,
                Mode = _cboMode.Text,
                AuthenticationTypes = string.Join(",", authTypes),
                UnicastCiphers = string.Join(",", ciphers),
                GroupCiphers = string.Join(",", ciphers)
            };

            DialogResult = DialogResult.OK;
            Close();
        };

        Controls.Add(btnOk);
        AcceptButton = btnOk;
    }
}

// ════════════════════════════════════════════════════════════════════
//  BRIDGE PANEL
// ════════════════════════════════════════════════════════════════════

public class BridgePanel : BasePanel
{
    private readonly BridgeService _svc;

    public BridgePanel(MikroTikClient client) : base(client)
    {
        _svc = new BridgeService(client);

        AddToolbarButton("＋ Nova Bridge", Color.FromArgb(0, 120, 215)).Click += BtnAdd_Click;
        AddToolbarButton("✎ Editar", Color.FromArgb(80, 80, 80)).Click += BtnEdit_Click;
        AddToolbarButton("✕ Apagar", Color.FromArgb(180, 30, 30)).Click += async (_, _) => await DeleteAsync();

        AddToolbarButton("🔌 Gerir Portas", Color.DarkCyan).Click += async (_, _) =>
        {
            using var frm = new BridgePortsForm(_svc, Client);
            frm.ShowDialog();
            await LoadDataAsync();
        };

        AddToolbarButton("↺ Atualizar", Color.FromArgb(0, 100, 180)).Click += async (_, _) => await LoadDataAsync();

        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = ".id", HeaderText = "ID", Visible = false });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "name", HeaderText = "Nome", FillWeight = 25 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "mac", HeaderText = "MAC", FillWeight = 30 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "vlan", HeaderText = "VLAN Filter", FillWeight = 20 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "running", HeaderText = "Running", FillWeight = 15 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "disabled", HeaderText = "Disabled", FillWeight = 10 });
    }

    protected override async Task LoadDataAsync()
    {
        try
        {
            var list = await _svc.GetBridgesAsync();
            Grid.Rows.Clear();
            foreach (var b in list)
            {
                Grid.Rows.Add(b.Id, b.Name, b.MacAddress, b.VlanFiltering ? "Sim" : "Não", b.Running ? "✔" : "✘", b.Disabled ? "Sim" : "Não");
            }
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private async void BtnAdd_Click(object? s, EventArgs e)
    {
        using var frm = new BridgeEditForm();
        if (frm.ShowDialog() == DialogResult.OK)
        {
            try
            {
                await _svc.CreateBridgeAsync(frm.Bridge);
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }
    }

    private async void BtnEdit_Click(object? s, EventArgs e)
    {
        var id = SelectedId();
        if (id == null)
        {
            MessageBox.Show("Seleciona uma bridge.");
            return;
        }

        var row = Grid.SelectedRows[0];
        bool currentVlan = row.Cells["vlan"].Value?.ToString() == "Sim";

        using var frm = new BridgeEditForm(new BridgeInterface { Name = row.Cells["name"].Value?.ToString() ?? "", VlanFiltering = currentVlan });
        if (frm.ShowDialog() == DialogResult.OK)
        {
            try
            {
                await _svc.UpdateBridgeAsync(id, new { name = frm.Bridge.Name, comment = frm.Bridge.Comment, @vlan_filtering = frm.Bridge.VlanFiltering ? "yes" : "no" });
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }
    }

    private async Task DeleteAsync()
    {
        var id = SelectedId();
        if (id == null) return;

        if (MessageBox.Show("Apagar bridge?", "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
        {
            try
            {
                await _svc.DeleteBridgeAsync(id);
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }
    }
}

public class BridgePortsForm : Form
{
    private readonly BridgeService _svc;
    private readonly MikroTikClient _client;
    private readonly DataGridView _grid = new();

    public BridgePortsForm(BridgeService svc, MikroTikClient client)
    {
        _svc = svc;
        _client = client;
        Text = "Gestão de Portas da Bridge";
        Size = new Size(500, 350);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9.5f);

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 40,
            Padding = new Padding(5)
        };

        var btnAdd = new Button
        {
            Text = "＋ Adicionar Porta",
            Height = 30,
            AutoSize = true,
            BackColor = Color.DodgerBlue,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };

        btnAdd.Click += async (_, _) =>
        {
            try
            {
                var iSvc = new InterfaceService(_client);
                var ifs = (await iSvc.GetAllAsync()).Select(i => i.Name).ToList();
                var brs = (await _svc.GetBridgesAsync()).Select(b => b.Name).ToList();

                if (brs.Count == 0)
                {
                    MessageBox.Show("Não existem Bridges criadas!");
                    return;
                }

                using var f = new BridgePortEditForm(ifs, brs);
                if (f.ShowDialog() == DialogResult.OK)
                {
                    await _svc.AddPortAsync(f.Port);
                    await LoadDataAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        };

        var btnEdit = new Button
        {
            Text = "✎ Editar",
            Height = 30,
            BackColor = Color.Gray,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };

        btnEdit.Click += async (_, _) =>
        {
            if (_grid.SelectedRows.Count == 0) return;
            var id = _grid.SelectedRows[0].Cells[".id"].Value?.ToString();
            if (id == null) return;

            try
            {
                var iSvc = new InterfaceService(_client);
                var ifs = (await iSvc.GetAllAsync()).Select(i => i.Name).ToList();
                var brs = (await _svc.GetBridgesAsync()).Select(b => b.Name).ToList();

                var existing = new BridgePort
                {
                    Interface = _grid.SelectedRows[0].Cells["interface"].Value?.ToString() ?? "",
                    Bridge = _grid.SelectedRows[0].Cells["bridge"].Value?.ToString() ?? ""
                };

                using var f = new BridgePortEditForm(ifs, brs, existing);
                if (f.ShowDialog() == DialogResult.OK)
                {
                    await _svc.UpdatePortAsync(id, new { @interface = f.Port.Interface, bridge = f.Port.Bridge });
                    await LoadDataAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        };

        var btnDel = new Button
        {
            Text = "✕ Remover",
            Height = 30,
            BackColor = Color.Firebrick,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };

        btnDel.Click += async (_, _) =>
        {
            if (_grid.SelectedRows.Count == 0) return;
            var id = _grid.SelectedRows[0].Cells[".id"].Value?.ToString();

            if (MessageBox.Show("Remover porta da bridge?", "Aviso", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                try
                {
                    await _svc.RemovePortAsync(id!);
                    await LoadDataAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        };

        toolbar.Controls.AddRange(new Control[] { btnAdd, btnEdit, btnDel });

        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.BackgroundColor = Color.White;
        _grid.RowHeadersVisible = false;

        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = ".id", Visible = false });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "interface", HeaderText = "Interface" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "bridge", HeaderText = "Bridge Destino" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "disabled", HeaderText = "Desativada" });

        Controls.Add(_grid);
        Controls.Add(toolbar);

        Load += async (_, _) => await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            var p = await _svc.GetPortsAsync();
            _grid.Rows.Clear();
            foreach (var x in p)
            {
                _grid.Rows.Add(x.Id, x.Interface, x.Bridge, x.Disabled ? "Sim" : "Não");
            }
        }
        catch { }
    }
}

public class BridgeEditForm : Form
{
    public BridgeInterface Bridge { get; private set; } = new();

    private readonly TextBox _txtName = new();
    private readonly TextBox _txtComment = new();
    private readonly CheckBox _chkVlan = new() { Text = "Ativar VLAN Filtering", AutoSize = true };

    public BridgeEditForm(BridgeInterface? existing = null)
    {
        Text = existing == null ? "Nova Bridge" : "Editar Bridge";
        Size = new Size(320, 210);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9.5f);

        int y = 20;

        Controls.Add(new Label { Text = "Nome:", Location = new Point(14, y + 3), AutoSize = true });
        _txtName.Location = new Point(100, y);
        _txtName.Width = 185;
        Controls.Add(_txtName);
        y += 38;

        Controls.Add(new Label { Text = "Comentário:", Location = new Point(14, y + 3), AutoSize = true });
        _txtComment.Location = new Point(100, y);
        _txtComment.Width = 185;
        Controls.Add(_txtComment);
        y += 38;

        _chkVlan.Location = new Point(100, y);
        Controls.Add(_chkVlan);
        y += 38;

        if (existing != null)
        {
            _txtName.Text = existing.Name;
            _txtComment.Text = existing.Comment;
            _chkVlan.Checked = existing.VlanFiltering;
        }

        var ok = new Button
        {
            Text = "Guardar",
            Location = new Point(100, y),
            Size = new Size(185, 28),
            BackColor = Color.DodgerBlue,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            DialogResult = DialogResult.OK
        };

        ok.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_txtName.Text))
            {
                MessageBox.Show("Nome obrigatório.");
                return;
            }

            Bridge = new BridgeInterface
            {
                Name = _txtName.Text.Trim(),
                Comment = _txtComment.Text,
                VlanFiltering = _chkVlan.Checked
            };
        };

        Controls.Add(ok);
        AcceptButton = ok;
    }
}

public class BridgePortEditForm : Form
{
    public BridgePort Port { get; private set; } = new();

    private readonly ComboBox _cboIface = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _cboBridge = new() { DropDownStyle = ComboBoxStyle.DropDownList };

    public BridgePortEditForm(List<string> interfaces, List<string> bridges, BridgePort? existing = null)
    {
        Text = existing == null ? "Adicionar Porta" : "Editar Porta";
        Size = new Size(340, 200);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9.5f);

        foreach (var i in interfaces)
        {
            _cboIface.Items.Add(i);
        }

        foreach (var b in bridges)
        {
            _cboBridge.Items.Add(b);
        }

        int y = 20;

        Controls.Add(new Label { Text = "Interface:", Location = new Point(14, y + 3), AutoSize = true });
        _cboIface.Location = new Point(130, y);
        _cboIface.Width = 170;
        Controls.Add(_cboIface);
        y += 40;

        Controls.Add(new Label { Text = "Bridge:", Location = new Point(14, y + 3), AutoSize = true });
        _cboBridge.Location = new Point(130, y);
        _cboBridge.Width = 170;
        Controls.Add(_cboBridge);
        y += 50;

        if (existing != null)
        {
            if (_cboIface.Items.Contains(existing.Interface)) _cboIface.SelectedItem = existing.Interface;
            if (_cboBridge.Items.Contains(existing.Bridge)) _cboBridge.SelectedItem = existing.Bridge;
        }
        else
        {
            if (_cboIface.Items.Count > 0) _cboIface.SelectedIndex = 0;
            if (_cboBridge.Items.Count > 0) _cboBridge.SelectedIndex = 0;
        }

        var ok = new Button
        {
            Text = "Guardar",
            Location = new Point(130, y),
            Size = new Size(170, 30),
            BackColor = Color.SeaGreen,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            DialogResult = DialogResult.OK
        };

        ok.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_cboIface.Text) || string.IsNullOrWhiteSpace(_cboBridge.Text))
            {
                MessageBox.Show("Obrigatório.");
                return;
            }

            Port = new BridgePort
            {
                Interface = _cboIface.Text,
                Bridge = _cboBridge.Text
            };
        };

        Controls.Add(ok);
        AcceptButton = ok;
    }
}

// ════════════════════════════════════════════════════════════════════
//  IP PANEL
// ════════════════════════════════════════════════════════════════════

public class IpPanel : BasePanel
{
    private readonly IpService _svc;

    public IpPanel(MikroTikClient client) : base(client)
    {
        _svc = new IpService(client);

        AddToolbarButton("＋ Adicionar", Color.DodgerBlue).Click += BtnAdd_Click;
        AddToolbarButton("✎ Editar", Color.Gray).Click += BtnEdit_Click;
        AddToolbarButton("✕ Apagar", Color.Firebrick).Click += async (_, _) => await DeleteAsync();
        AddToolbarButton("↺ Atualizar", Color.FromArgb(0, 100, 180)).Click += async (_, _) => await LoadDataAsync();

        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = ".id", Visible = false });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "address", HeaderText = "Endereço" });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "interface", HeaderText = "Interface" });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "dynamic", HeaderText = "Dinâmico" });
    }

    protected override async Task LoadDataAsync()
    {
        try
        {
            var list = await _svc.GetAddressesAsync();
            Grid.Rows.Clear();
            foreach (var a in list)
            {
                Grid.Rows.Add(a.Id, a.Address, a.Interface, a.Dynamic ? "Sim" : "Não");
            }
        }
        catch { }
    }

    private async void BtnAdd_Click(object? s, EventArgs e)
    {
        try
        {
            var iSvc = new InterfaceService(Client);
            var interfaces = (await iSvc.GetAllAsync()).Select(i => i.Name).ToList();

            if (interfaces.Count == 0)
            {
                MessageBox.Show("Não foram encontradas interfaces no router.", "Aviso");
                return;
            }

            using var frm = new IpAddressEditForm(interfaces);
            if (frm.ShowDialog() == DialogResult.OK)
            {
                await _svc.AddAddressAsync(frm.Address);
                await LoadDataAsync();
            }
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private async void BtnEdit_Click(object? s, EventArgs e)
    {
        if (Grid.SelectedRows.Count == 0) return;
        var row = Grid.SelectedRows[0];
        var id = row.Cells[".id"].Value?.ToString();
        if (id == null) return;

        if (row.Cells["dynamic"].Value?.ToString() == "Sim")
        {
            MessageBox.Show("Endereços IP dinâmicos não podem ser editados.");
            return;
        }

        try
        {
            var iSvc = new InterfaceService(Client);
            var interfaces = (await iSvc.GetAllAsync()).Select(i => i.Name).ToList();

            var existing = new IpAddress
            {
                Address = row.Cells["address"].Value?.ToString() ?? "",
                Interface = row.Cells["interface"].Value?.ToString() ?? ""
            };

            using var frm = new IpAddressEditForm(interfaces, existing);
            if (frm.ShowDialog() == DialogResult.OK)
            {
                await _svc.UpdateAddressAsync(id, new { address = frm.Address.Address, @interface = frm.Address.Interface, comment = frm.Address.Comment });
                await LoadDataAsync();
            }
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private async Task DeleteAsync()
    {
        var id = SelectedId();
        if (id != null && MessageBox.Show("Apagar IP?", "Confirma", MessageBoxButtons.YesNo) == DialogResult.Yes)
        {
            try
            {
                await _svc.DeleteAddressAsync(id);
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }
    }
}

public class IpAddressEditForm : Form
{
    public IpAddress Address { get; private set; } = new();

    private readonly TextBox _txtAddress = new();
    private readonly ComboBox _cboInterface = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _txtComment = new();

    public IpAddressEditForm(List<string> interfaces, IpAddress? existing = null)
    {
        Text = existing == null ? "Novo IP" : "Editar IP";
        Size = new Size(340, 210);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9.5f);

        foreach (var i in interfaces)
        {
            _cboInterface.Items.Add(i);
        }

        if (_cboInterface.Items.Count > 0)
        {
            _cboInterface.SelectedIndex = 0;
        }

        int y = 18;

        Controls.Add(new Label { Text = "IP (CIDR):", Location = new Point(14, y + 3), AutoSize = true });
        _txtAddress.Location = new Point(120, y);
        _txtAddress.Width = 185;
        Controls.Add(_txtAddress);
        y += 38;

        Controls.Add(new Label { Text = "Interface:", Location = new Point(14, y + 3), AutoSize = true });
        _cboInterface.Location = new Point(120, y);
        _cboInterface.Width = 185;
        Controls.Add(_cboInterface);
        y += 38;

        Controls.Add(new Label { Text = "Comentário:", Location = new Point(14, y + 3), AutoSize = true });
        _txtComment.Location = new Point(120, y);
        _txtComment.Width = 185;
        Controls.Add(_txtComment);
        y += 38;

        if (existing != null)
        {
            _txtAddress.Text = existing.Address;
            if (_cboInterface.Items.Contains(existing.Interface)) _cboInterface.SelectedItem = existing.Interface;
            _txtComment.Text = existing.Comment;
        }

        var ok = new Button
        {
            Text = "Guardar",
            Location = new Point(120, y),
            Size = new Size(185, 28),
            BackColor = Color.DodgerBlue,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            DialogResult = DialogResult.OK
        };

        ok.Click += (_, _) =>
        {
            Address = new IpAddress
            {
                Address = _txtAddress.Text.Trim(),
                Interface = _cboInterface.Text.Trim(),
                Comment = _txtComment.Text
            };
        };

        Controls.Add(ok);
        AcceptButton = ok;
    }
}

// ════════════════════════════════════════════════════════════════════
//  ROUTES PANEL
// ════════════════════════════════════════════════════════════════════

public class RoutesPanel : BasePanel
{
    private readonly RoutingService _svc;

    public RoutesPanel(MikroTikClient client) : base(client)
    {
        _svc = new RoutingService(client);

        AddToolbarButton("＋ Nova Rota", Color.DodgerBlue).Click += BtnAdd_Click;
        AddToolbarButton("✎ Editar", Color.Gray).Click += BtnEdit_Click;
        AddToolbarButton("✕ Apagar", Color.Firebrick).Click += async (_, _) => await DeleteAsync();
        AddToolbarButton("✔ Ativar", Color.SeaGreen).Click += async (_, _) => await ToggleAsync(true);
        AddToolbarButton("✕ Desativar", Color.Chocolate).Click += async (_, _) => await ToggleAsync(false);
        AddToolbarButton("↺ Atualizar", Color.FromArgb(0, 100, 180)).Click += async (_, _) => await LoadDataAsync();

        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = ".id", Visible = false });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "dst", HeaderText = "Destino" });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "gw", HeaderText = "Gateway" });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "dist", HeaderText = "Distância" });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "active", HeaderText = "Ativa" });
    }

    protected override async Task LoadDataAsync()
    {
        try
        {
            var list = await _svc.GetRoutesAsync();
            Grid.Rows.Clear();
            foreach (var r in list)
            {
                Grid.Rows.Add(r.Id, r.DstAddress, r.Gateway, r.Distance, r.Active ? "Sim" : "Não");
            }
        }
        catch { }
    }

    private async void BtnAdd_Click(object? s, EventArgs e)
    {
        using var frm = new RouteEditForm();
        if (frm.ShowDialog() == DialogResult.OK)
        {
            try
            {
                await _svc.AddRouteAsync(frm.Route);
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }
    }

    private async void BtnEdit_Click(object? s, EventArgs e)
    {
        if (Grid.SelectedRows.Count == 0) return;
        var row = Grid.SelectedRows[0];
        var id = row.Cells[".id"].Value?.ToString();
        if (id == null) return;

        var existing = new StaticRoute
        {
            DstAddress = row.Cells["dst"].Value?.ToString() ?? "",
            Gateway = row.Cells["gw"].Value?.ToString() ?? ""
        };

        using var frm = new RouteEditForm(existing);
        if (frm.ShowDialog() == DialogResult.OK)
        {
            try
            {
                await _svc.UpdateRouteAsync(id, new { @dst_address = frm.Route.DstAddress, gateway = frm.Route.Gateway });
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }
    }

    private async Task DeleteAsync()
    {
        var id = SelectedId();
        if (id != null && MessageBox.Show("Apagar Rota?", "Confirma", MessageBoxButtons.YesNo) == DialogResult.Yes)
        {
            try
            {
                await _svc.DeleteRouteAsync(id);
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }
    }

    private async Task ToggleAsync(bool enable)
    {
        var id = SelectedId();
        if (id == null) return;

        try
        {
            if (enable) await _svc.EnableRouteAsync(id);
            else await _svc.DisableRouteAsync(id);
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }
}

public class RouteEditForm : Form
{
    public StaticRoute Route { get; private set; } = new();

    private readonly TextBox _txtDst = new();
    private readonly TextBox _txtGw = new();

    public RouteEditForm(StaticRoute? existing = null)
    {
        Text = existing == null ? "Nova Rota" : "Editar Rota";
        Size = new Size(340, 180);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9.5f);

        Controls.Add(new Label { Text = "Destino:", Location = new Point(14, 23) });
        _txtDst.Location = new Point(100, 20);
        _txtDst.Width = 200;
        Controls.Add(_txtDst);

        Controls.Add(new Label { Text = "Gateway:", Location = new Point(14, 63) });
        _txtGw.Location = new Point(100, 60);
        _txtGw.Width = 200;
        Controls.Add(_txtGw);

        if (existing != null)
        {
            _txtDst.Text = existing.DstAddress;
            _txtGw.Text = existing.Gateway;
        }

        var ok = new Button
        {
            Text = "Guardar",
            Location = new Point(100, 100),
            Size = new Size(200, 30),
            BackColor = Color.DodgerBlue,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            DialogResult = DialogResult.OK
        };

        ok.Click += (_, _) =>
        {
            Route = new StaticRoute
            {
                DstAddress = _txtDst.Text.Trim(),
                Gateway = _txtGw.Text.Trim()
            };
        };

        Controls.Add(ok);
        AcceptButton = ok;
    }
}

// ════════════════════════════════════════════════════════════════════
//  DHCP PANEL
// ════════════════════════════════════════════════════════════════════

public class DhcpPanel : BasePanel
{
    private readonly DhcpService _svc;
    private readonly TabControl _innerTabs = new();

    private readonly DataGridView _gridPools = new();
    private readonly DataGridView _gridNetworks = new();

    private readonly Button _btnAdd;
    private readonly Button _btnEdit;
    private readonly Button _btnDel;
    private readonly Button _btnEnable;
    private readonly Button _btnDisable;

    public DhcpPanel(MikroTikClient client) : base(client)
    {
        _svc = new DhcpService(client);
        Controls.Remove(Grid);

        _innerTabs.Dock = DockStyle.Fill;

        var tabServers = new TabPage("Servidores DHCP");
        Grid.Dock = DockStyle.Fill;
        tabServers.Controls.Add(Grid);

        var tabPools = new TabPage("IP Pools");
        StyleGrid(_gridPools);
        _gridPools.Dock = DockStyle.Fill;
        tabPools.Controls.Add(_gridPools);

        var tabNetworks = new TabPage("Redes DHCP");
        StyleGrid(_gridNetworks);
        _gridNetworks.Dock = DockStyle.Fill;
        tabNetworks.Controls.Add(_gridNetworks);

        _innerTabs.TabPages.AddRange(new[] { tabServers, tabPools, tabNetworks });
        _innerTabs.SelectedIndexChanged += async (_, _) => await UpdateToolbarAndLoad();
        Controls.Add(_innerTabs);

        _btnAdd = AddToolbarButton("＋ Adicionar", Color.DodgerBlue);
        _btnAdd.Click += BtnAdd_Click;

        _btnEdit = AddToolbarButton("✎ Editar", Color.FromArgb(80, 80, 80));
        _btnEdit.Click += BtnEdit_Click;

        _btnDel = AddToolbarButton("✕ Apagar", Color.Firebrick);
        _btnDel.Click += BtnDel_Click;

        _btnEnable = AddToolbarButton("✔ Ativar", Color.FromArgb(0, 140, 80));
        _btnEnable.Click += async (_, _) => await ToggleAsync(true);

        _btnDisable = AddToolbarButton("✕ Desativar", Color.FromArgb(100, 60, 60));
        _btnDisable.Click += async (_, _) => await ToggleAsync(false);

        AddToolbarButton("↺ Atualizar", Color.FromArgb(0, 100, 180)).Click += async (_, _) => await LoadDataAsync();

        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = ".id", Visible = false });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "name", HeaderText = "Nome", FillWeight = 25 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "interface", HeaderText = "Interface", FillWeight = 25 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "address-pool", HeaderText = "Pool", FillWeight = 20 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "lease-time", HeaderText = "Lease Time", FillWeight = 15 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "disabled", HeaderText = "Disabled", FillWeight = 15 });

        _gridPools.Columns.Add(new DataGridViewTextBoxColumn { Name = ".id", Visible = false });
        _gridPools.Columns.Add(new DataGridViewTextBoxColumn { Name = "name", HeaderText = "Nome", FillWeight = 30 });
        _gridPools.Columns.Add(new DataGridViewTextBoxColumn { Name = "ranges", HeaderText = "Ranges", FillWeight = 70 });

        _gridNetworks.Columns.Add(new DataGridViewTextBoxColumn { Name = ".id", Visible = false });
        _gridNetworks.Columns.Add(new DataGridViewTextBoxColumn { Name = "address", HeaderText = "Rede (Address)", FillWeight = 25 });
        _gridNetworks.Columns.Add(new DataGridViewTextBoxColumn { Name = "gateway", HeaderText = "Gateway", FillWeight = 25 });
        _gridNetworks.Columns.Add(new DataGridViewTextBoxColumn { Name = "dns-server", HeaderText = "DNS Server", FillWeight = 25 });
        _gridNetworks.Columns.Add(new DataGridViewTextBoxColumn { Name = "comment", HeaderText = "Comentário", FillWeight = 25 });
    }

    private static void StyleGrid(DataGridView g)
    {
        g.ReadOnly = true;
        g.AllowUserToAddRows = false;
        g.AllowUserToDeleteRows = false;
        g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        g.BackgroundColor = Color.White;
        g.RowHeadersVisible = false;
        g.BorderStyle = BorderStyle.None;
        g.EnableHeadersVisualStyles = false;
        g.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(40, 40, 40),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };
    }

    private async Task UpdateToolbarAndLoad()
    {
        bool isServer = _innerTabs.SelectedIndex == 0;
        bool isPool = _innerTabs.SelectedIndex == 1;

        _btnEnable.Visible = isServer;
        _btnDisable.Visible = isServer;
        _btnEdit.Visible = !isPool;

        await LoadDataAsync();
    }

    protected override async Task LoadDataAsync()
    {
        try
        {
            if (_innerTabs.SelectedIndex == 0)
            {
                var list = await _svc.GetServersAsync();
                Grid.Rows.Clear();
                foreach (var d in list)
                    Grid.Rows.Add(d.Id, d.Name, d.Interface, d.AddressPool, d.LeaseTime, d.Disabled ? "Sim" : "Não");
            }
            else if (_innerTabs.SelectedIndex == 1)
            {
                var list = await _svc.GetPoolsAsync();
                _gridPools.Rows.Clear();
                foreach (var p in list)
                    _gridPools.Rows.Add(p.Id, p.Name, p.Ranges);
            }
            else
            {
                var list = await _svc.GetNetworksAsync();
                _gridNetworks.Rows.Clear();
                foreach (var n in list)
                    _gridNetworks.Rows.Add(n.Id, n.Address, n.Gateway, n.DnsServer, n.Comment);
            }
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private async void BtnAdd_Click(object? s, EventArgs e)
    {
        try
        {
            if (_innerTabs.SelectedIndex == 0)
            {
                var pools = await _svc.GetPoolsAsync();
                if (pools.Count == 0 && MessageBox.Show("Não existem Pools.\nDeseja criar uma Pool primeiro?", "Aviso", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    _innerTabs.SelectedIndex = 1;
                    return;
                }

                var iSvc = new InterfaceService(Client);
                var interfaces = (await iSvc.GetAllAsync()).Select(i => i.Name).ToList();

                using var frm = new DhcpServerEditForm(pools, interfaces);
                if (frm.ShowDialog() == DialogResult.OK)
                {
                    await _svc.CreateServerAsync(frm.Server);
                    await LoadDataAsync();
                }
            }
            else if (_innerTabs.SelectedIndex == 1)
            {
                using var frm = new DhcpPoolEditForm();
                if (frm.ShowDialog() == DialogResult.OK)
                {
                    await _svc.CreatePoolAsync(frm.Pool);
                    await LoadDataAsync();
                }
            }
            else
            {
                using var frm = new DhcpNetworkEditForm();
                if (frm.ShowDialog() == DialogResult.OK)
                {
                    await _svc.CreateNetworkAsync(frm.Network);
                    await LoadDataAsync();
                }
            }
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private async void BtnEdit_Click(object? s, EventArgs e)
    {
        try
        {
            if (_innerTabs.SelectedIndex == 0)
            {
                if (Grid.SelectedRows.Count == 0) { MessageBox.Show("Selecione um servidor."); return; }
                var row = Grid.SelectedRows[0];
                var id = row.Cells[".id"].Value?.ToString();
                if (id == null) return;

                var pools = await _svc.GetPoolsAsync();
                var iSvc = new InterfaceService(Client);
                var interfaces = (await iSvc.GetAllAsync()).Select(i => i.Name).ToList();

                var existing = new DhcpServer
                {
                    Name = row.Cells["name"].Value?.ToString() ?? "",
                    Interface = row.Cells["interface"].Value?.ToString() ?? "",
                    AddressPool = row.Cells["address-pool"].Value?.ToString() ?? "",
                    LeaseTime = row.Cells["lease-time"].Value?.ToString() ?? "10m"
                };

                using var frm = new DhcpServerEditForm(pools, interfaces, existing);
                if (frm.ShowDialog() == DialogResult.OK)
                {
                    await _svc.UpdateServerAsync(id, new
                    {
                        name = frm.Server.Name,
                        @interface = frm.Server.Interface,
                        @address_pool = frm.Server.AddressPool,
                        @lease_time = frm.Server.LeaseTime
                    });
                    await LoadDataAsync();
                }
            }
            else if (_innerTabs.SelectedIndex == 2)
            {
                if (_gridNetworks.SelectedRows.Count == 0) { MessageBox.Show("Selecione uma rede."); return; }
                var row = _gridNetworks.SelectedRows[0];
                var id = row.Cells[".id"].Value?.ToString();
                if (id == null) return;

                var existing = new DhcpNetwork
                {
                    Address = row.Cells["address"].Value?.ToString() ?? "",
                    Gateway = row.Cells["gateway"].Value?.ToString() ?? "",
                    DnsServer = row.Cells["dns-server"].Value?.ToString() ?? "",
                    Comment = row.Cells["comment"].Value?.ToString() ?? ""
                };

                using var frm = new DhcpNetworkEditForm(existing);
                if (frm.ShowDialog() == DialogResult.OK)
                {
                    await _svc.UpdateNetworkAsync(id, new
                    {
                        address = frm.Network.Address,
                        gateway = frm.Network.Gateway,
                        @dns_server = frm.Network.DnsServer,
                        comment = frm.Network.Comment
                    });
                    await LoadDataAsync();
                }
            }
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private async void BtnDel_Click(object? s, EventArgs e)
    {
        try
        {
            if (_innerTabs.SelectedIndex == 0)
            {
                var id = SelectedId();
                if (id == null) { MessageBox.Show("Seleciona um servidor."); return; }
                if (MessageBox.Show("Apagar servidor DHCP?", "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    await _svc.DeleteServerAsync(id);
                    await LoadDataAsync();
                }
            }
            else if (_innerTabs.SelectedIndex == 1)
            {
                if (_gridPools.SelectedRows.Count == 0) { MessageBox.Show("Seleciona uma Pool."); return; }
                var id = _gridPools.SelectedRows[0].Cells[".id"].Value?.ToString();
                if (id == null) return;

                if (MessageBox.Show("Apagar esta Pool?\nSe estiver em uso, dará erro.", "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    await _svc.DeletePoolAsync(id);
                    await LoadDataAsync();
                }
            }
            else
            {
                if (_gridNetworks.SelectedRows.Count == 0) { MessageBox.Show("Seleciona uma Rede."); return; }
                var id = _gridNetworks.SelectedRows[0].Cells[".id"].Value?.ToString();
                if (id == null) return;

                if (MessageBox.Show("Apagar esta Rede?", "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    await _svc.DeleteNetworkAsync(id);
                    await LoadDataAsync();
                }
            }
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private async Task ToggleAsync(bool enable)
    {
        if (_innerTabs.SelectedIndex != 0) return;
        var id = SelectedId();
        if (id == null) { MessageBox.Show("Seleciona um servidor."); return; }

        try
        {
            if (enable) await _svc.EnableServerAsync(id);
            else await _svc.DisableServerAsync(id);
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }
}

public class PoolsPanel : BasePanel
{
    private readonly DhcpService _svc;

    public PoolsPanel(MikroTikClient client) : base(client)
    {
        _svc = new DhcpService(client);

        AddToolbarButton("＋ Nova Pool", Color.DodgerBlue).Click += BtnAdd_Click;
        AddToolbarButton("✕ Apagar", Color.Firebrick).Click += async (_, _) => await DeleteAsync();
        AddToolbarButton("↺ Atualizar", Color.FromArgb(0, 100, 180)).Click += async (_, _) => await LoadDataAsync();

        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = ".id", Visible = false });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "name", HeaderText = "Nome" });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ranges", HeaderText = "Ranges" });
    }

    protected override async Task LoadDataAsync()
    {
        try
        {
            var list = await _svc.GetPoolsAsync();
            Grid.Rows.Clear();
            foreach (var p in list)
            {
                Grid.Rows.Add(p.Id, p.Name, p.Ranges);
            }
        }
        catch { }
    }

    private async void BtnAdd_Click(object? s, EventArgs e)
    {
        using var frm = new DhcpPoolEditForm();
        if (frm.ShowDialog() == DialogResult.OK)
        {
            await _svc.CreatePoolAsync(frm.Pool);
            await LoadDataAsync();
        }
    }

    private async Task DeleteAsync()
    {
        var id = SelectedId();
        if (id != null && MessageBox.Show("Apagar Pool?", "Confirma", MessageBoxButtons.YesNo) == DialogResult.Yes)
        {
            await _svc.DeletePoolAsync(id);
            await LoadDataAsync();
        }
    }
}

public class DhcpServerEditForm : Form
{
    public DhcpServer Server { get; private set; } = new();

    private readonly TextBox _txtName = new();
    private readonly ComboBox _cboIface = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _cboPool = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _txtLease = new();

    public DhcpServerEditForm(List<DhcpPool> pools, List<string> interfaces, DhcpServer? existing = null)
    {
        Text = existing == null ? "Novo Servidor DHCP" : "Editar Servidor DHCP";
        Size = new Size(340, 230);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        Font = new Font("Segoe UI", 9.5f);

        foreach (var p in pools) _cboPool.Items.Add(p.Name);
        if (_cboPool.Items.Count > 0) _cboPool.SelectedIndex = 0;

        foreach (var i in interfaces) _cboIface.Items.Add(i);
        if (_cboIface.Items.Count > 0) _cboIface.SelectedIndex = 0;

        int y = 18;
        Controls.Add(new Label { Text = "Nome:", Location = new Point(14, y + 3), AutoSize = true });
        _txtName.Location = new Point(120, y); _txtName.Width = 185; Controls.Add(_txtName); y += 38;

        Controls.Add(new Label { Text = "Interface:", Location = new Point(14, y + 3), AutoSize = true });
        _cboIface.Location = new Point(120, y); _cboIface.Width = 185; Controls.Add(_cboIface); y += 38;

        Controls.Add(new Label { Text = "Pool:", Location = new Point(14, y + 3), AutoSize = true });
        _cboPool.Location = new Point(120, y); _cboPool.Width = 185; Controls.Add(_cboPool); y += 38;

        Controls.Add(new Label { Text = "Lease Time:", Location = new Point(14, y + 3), AutoSize = true });
        _txtLease.Location = new Point(120, y); _txtLease.Width = 185; Controls.Add(_txtLease); y += 38;

        if (existing != null)
        {
            _txtName.Text = existing.Name;
            if (_cboIface.Items.Contains(existing.Interface)) _cboIface.SelectedItem = existing.Interface;
            if (_cboPool.Items.Contains(existing.AddressPool)) _cboPool.SelectedItem = existing.AddressPool;
            _txtLease.Text = existing.LeaseTime;
        }
        else
        {
            _txtLease.Text = "10m";
        }

        var btn = new Button
        {
            Text = "Guardar",
            Location = new Point(120, y),
            Size = new Size(185, 28),
            BackColor = Color.DodgerBlue,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btn.FlatAppearance.BorderSize = 0;

        btn.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_txtName.Text) || string.IsNullOrWhiteSpace(_cboIface.Text) || string.IsNullOrWhiteSpace(_cboPool.Text))
            {
                MessageBox.Show("Nome, Interface e Pool são obrigatórios.");
                return;
            }

            Server = new DhcpServer
            {
                Name = _txtName.Text.Trim(),
                Interface = _cboIface.Text.Trim(),
                AddressPool = _cboPool.Text,
                LeaseTime = _txtLease.Text.Trim()
            };
            DialogResult = DialogResult.OK;
            Close();
        };

        Controls.Add(btn);
        AcceptButton = btn;
    }
}

public class DhcpPoolEditForm : Form
{
    public DhcpPool Pool { get; private set; } = new();
    private readonly TextBox _txtName = new();
    private readonly TextBox _txtRanges = new();

    public DhcpPoolEditForm()
    {
        Text = "Nova Pool";
        Size = new Size(340, 180);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        Font = new Font("Segoe UI", 9.5f);

        int y = 18;
        Controls.Add(new Label { Text = "Nome:", Location = new Point(14, y + 3), AutoSize = true });
        _txtName.Location = new Point(100, y); _txtName.Width = 200; Controls.Add(_txtName); y += 38;

        Controls.Add(new Label { Text = "Ranges:", Location = new Point(14, y + 3), AutoSize = true });
        _txtRanges.Location = new Point(100, y); _txtRanges.Width = 200; Controls.Add(_txtRanges); y += 38;

        var btn = new Button
        {
            Text = "Guardar",
            Location = new Point(100, y),
            Size = new Size(200, 28),
            BackColor = Color.DodgerBlue,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btn.FlatAppearance.BorderSize = 0;

        btn.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_txtName.Text) || string.IsNullOrWhiteSpace(_txtRanges.Text))
            {
                MessageBox.Show("Preencha todos os campos.");
                return;
            }

            Pool = new DhcpPool { Name = _txtName.Text.Trim(), Ranges = _txtRanges.Text.Trim() };
            DialogResult = DialogResult.OK;
            Close();
        };

        Controls.Add(btn);
        AcceptButton = btn;
    }
}

public class DhcpNetworkEditForm : Form
{
    public DhcpNetwork Network { get; private set; } = new();
    private readonly TextBox _txtAddress = new();
    private readonly TextBox _txtGateway = new();
    private readonly TextBox _txtDns = new();
    private readonly TextBox _txtComment = new();

    public DhcpNetworkEditForm(DhcpNetwork? existing = null)
    {
        Text = existing == null ? "Nova Rede DHCP" : "Editar Rede DHCP";
        Size = new Size(360, 260);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        Font = new Font("Segoe UI", 9.5f);

        int y = 18;
        Controls.Add(new Label { Text = "Rede (CIDR):", Location = new Point(14, y + 3), AutoSize = true });
        _txtAddress.Location = new Point(120, y); _txtAddress.Width = 200; Controls.Add(_txtAddress); y += 38;

        Controls.Add(new Label { Text = "Gateway:", Location = new Point(14, y + 3), AutoSize = true });
        _txtGateway.Location = new Point(120, y); _txtGateway.Width = 200; Controls.Add(_txtGateway); y += 38;

        Controls.Add(new Label { Text = "Servidor DNS:", Location = new Point(14, y + 3), AutoSize = true });
        _txtDns.Location = new Point(120, y); _txtDns.Width = 200; Controls.Add(_txtDns); y += 38;

        Controls.Add(new Label { Text = "Comentário:", Location = new Point(14, y + 3), AutoSize = true });
        _txtComment.Location = new Point(120, y); _txtComment.Width = 200; Controls.Add(_txtComment); y += 38;

        if (existing != null)
        {
            _txtAddress.Text = existing.Address;
            _txtGateway.Text = existing.Gateway;
            _txtDns.Text = existing.DnsServer;
            _txtComment.Text = existing.Comment;
        }

        var btn = new Button
        {
            Text = "Guardar",
            Location = new Point(120, y),
            Size = new Size(200, 28),
            BackColor = Color.DodgerBlue,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btn.FlatAppearance.BorderSize = 0;

        btn.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_txtAddress.Text))
            {
                MessageBox.Show("A rede (CIDR) é obrigatória.");
                return;
            }

            Network = new DhcpNetwork
            {
                Address = _txtAddress.Text.Trim(),
                Gateway = _txtGateway.Text.Trim(),
                DnsServer = _txtDns.Text.Trim(),
                Comment = _txtComment.Text.Trim()
            };
            DialogResult = DialogResult.OK;
            Close();
        };

        Controls.Add(btn);
        AcceptButton = btn;
    }
}

// ════════════════════════════════════════════════════════════════════
//  DNS PANEL
// ════════════════════════════════════════════════════════════════════

public class DnsPanel : BasePanel
{
    private readonly DnsService _svc;

    private readonly TextBox _txtServers = new();
    private readonly CheckBox _chkAllow = new() { Text = "Permitir pedidos remotos", AutoSize = true };

    public DnsPanel(MikroTikClient client) : base(client)
    {
        _svc = new DnsService(client);
        Controls.Remove(Grid);

        var pnl = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20) };

        var lblServers = new Label
        {
            Text = "Servidores Upstream DNS (ex: 8.8.8.8, 1.1.1.1):",
            Location = new Point(20, 20),
            AutoSize = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };

        _txtServers.Location = new Point(20, 45);
        _txtServers.Width = 350;
        _chkAllow.Location = new Point(20, 80);

        var btnSave = new Button
        {
            Text = "💾 Guardar Definições",
            Location = new Point(20, 120),
            Width = 160,
            Height = 32,
            BackColor = Color.DodgerBlue,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnSave.Click += async (_, _) =>
        {
            try
            {
                await _svc.UpdateSettingsAsync(_txtServers.Text, _chkAllow.Checked);
                MessageBox.Show("Configuração guardada!");
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        };

        var btnFlush = new Button
        {
            Text = "🗑 Limpar Cache",
            Location = new Point(190, 120),
            Width = 150,
            Height = 32,
            BackColor = Color.Chocolate,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnFlush.Click += async (_, _) =>
        {
            try
            {
                await _svc.FlushCacheAsync();
                MessageBox.Show("Cache limpa!");
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        };

        var btnStatic = new Button
        {
            Text = "🌐 Gerir DNS Estático",
            Location = new Point(20, 170),
            Width = 320,
            Height = 35,
            BackColor = Color.SeaGreen,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnStatic.Click += (_, _) =>
        {
            using var f = new DnsStaticForm(_svc);
            f.ShowDialog();
        };

        pnl.Controls.AddRange(new Control[] { lblServers, _txtServers, _chkAllow, btnSave, btnFlush, btnStatic });
        Controls.Add(pnl);
    }

    protected override async Task LoadDataAsync()
    {
        try
        {
            var s = await _svc.GetSettingsAsync();
            if (s != null)
            {
                _txtServers.Text = s.Servers;
                _chkAllow.Checked = s.AllowRemoteRequests;
            }
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }
}

public class DnsStaticForm : Form
{
    private readonly DnsService _svc;
    private readonly DataGridView _grid = new();

    public DnsStaticForm(DnsService svc)
    {
        _svc = svc;
        Text = "Gestão de DNS Estático";
        Size = new Size(500, 350);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9.5f);

        var tbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 40,
            Padding = new Padding(5)
        };

        var bA = new Button
        {
            Text = "＋ Novo Domínio",
            Height = 30,
            BackColor = Color.DodgerBlue,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            AutoSize = true
        };
        bA.Click += async (_, _) =>
        {
            using var f = new DnsStaticEditForm();
            if (f.ShowDialog() == DialogResult.OK)
            {
                await _svc.AddStaticEntryAsync(f.Entry);
                await LoadData();
            }
        };

        var bD = new Button
        {
            Text = "✕ Remover",
            Height = 30,
            BackColor = Color.Firebrick,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        bD.Click += async (_, _) =>
        {
            if (_grid.SelectedRows.Count == 0) return;
            var id = _grid.SelectedRows[0].Cells[".id"].Value?.ToString();

            if (MessageBox.Show("Remover?", "Aviso", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                await _svc.DeleteStaticEntryAsync(id!);
                await LoadData();
            }
        };

        tbar.Controls.AddRange(new Control[] { bA, bD });

        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.BackgroundColor = Color.White;
        _grid.RowHeadersVisible = false;

        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = ".id", Visible = false });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "name", HeaderText = "Nome (Domínio)" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "address", HeaderText = "IP Destino" });

        Controls.Add(_grid);
        Controls.Add(tbar);

        Load += async (_, _) => await LoadData();
    }

    private async Task LoadData()
    {
        try
        {
            var l = await _svc.GetStaticEntriesAsync();
            _grid.Rows.Clear();
            foreach (var e in l)
            {
                _grid.Rows.Add(e.Id, e.Name, e.Address);
            }
        }
        catch { }
    }
}

public class DnsStaticEditForm : Form
{
    public DnsStaticEntry Entry { get; private set; } = new();

    public DnsStaticEditForm()
    {
        Text = "DNS Estático";
        Size = new Size(320, 180);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        Font = new Font("Segoe UI", 9f);

        var tN = new TextBox { Location = new Point(100, 20), Width = 180 };
        Controls.Add(new Label { Text = "Domínio:", Location = new Point(20, 23) });

        var tI = new TextBox { Location = new Point(100, 60), Width = 180 };
        Controls.Add(new Label { Text = "IP Destino:", Location = new Point(20, 63) });

        var b = new Button
        {
            Text = "Adicionar",
            Location = new Point(100, 100),
            Width = 180,
            BackColor = Color.DodgerBlue,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            DialogResult = DialogResult.OK
        };

        b.Click += (_, _) =>
        {
            Entry = new DnsStaticEntry
            {
                Name = tN.Text,
                Address = tI.Text
            };
        };

        Controls.AddRange(new Control[] { tN, tI, b });
        AcceptButton = b;
    }
}

// ════════════════════════════════════════════════════════════════════
//  SYSTEM PANEL
// ════════════════════════════════════════════════════════════════════

public class SystemPanel : BasePanel
{
    private readonly FlowLayoutPanel _contentPanel;

    public SystemPanel(MikroTikClient client) : base(client)
    {
        Controls.Remove(Grid);

        _contentPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            Padding = new Padding(20),
            AutoScroll = true
        };

        AddToolbarButton("↻ Reboot Router", Color.FromArgb(180, 30, 30)).Click += async (_, _) =>
        {
            if (MessageBox.Show("Tem a certeza que deseja reiniciar o router?", "Reboot", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                try
                {
                    await Client.RebootAsync();
                    MessageBox.Show("Reboot iniciado com sucesso.");
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                }
            }
        };

        AddToolbarButton("↺ Atualizar Recursos", Color.FromArgb(0, 100, 180)).Click += async (_, _) => await LoadDataAsync();
        Controls.Add(_contentPanel);
        _contentPanel.BringToFront();
    }

    protected override async Task LoadDataAsync()
    {
        try
        {
            var res = await Client.GetSingleAsync<SystemResource>("system/resource");
            if (res != null)
            {
                _contentPanel.Controls.Clear();
                _contentPanel.Controls.Add(new Label { Text = $"Board Name: {res.BoardName}", AutoSize = true, Font = new Font("Segoe UI", 12f, FontStyle.Bold), Margin = new Padding(0, 0, 0, 10) });
                _contentPanel.Controls.Add(new Label { Text = $"Version: {res.Version}", AutoSize = true, Font = new Font("Segoe UI", 10f), Margin = new Padding(0, 0, 0, 5) });
                _contentPanel.Controls.Add(new Label { Text = $"Uptime: {res.Uptime}", AutoSize = true, Font = new Font("Segoe UI", 10f), Margin = new Padding(0, 0, 0, 5) });
                _contentPanel.Controls.Add(new Label { Text = $"CPU Load: {res.CpuLoad}%", AutoSize = true, Font = new Font("Segoe UI", 10f), Margin = new Padding(0, 0, 0, 5) });
                _contentPanel.Controls.Add(new Label { Text = $"Free Memory: {res.FreeMemory / 1024 / 1024} MB / {res.TotalMemory / 1024 / 1024} MB", AutoSize = true, Font = new Font("Segoe UI", 10f) });
            }
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }
}

public class MikroTikCloudSettings
{
    [JsonProperty("dns-name")]
    public string DnsName { get; set; } = "";
}

// ════════════════════════════════════════════════════════════════════
//  WIREGUARD PANEL
// ════════════════════════════════════════════════════════════════════

public class WireGuardPanel : BasePanel
{
    private readonly WireGuardService _svc;

    public WireGuardPanel(MikroTikClient client) : base(client)
    {
        _svc = new WireGuardService(client);

        AddToolbarButton("⚡ Configuração Rápida", Color.DarkOrange).Click += async (_, _) =>
        {
            using var frm = new VpnQuickSetupForm(Client);
            if (frm.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    await _svc.SetupFullVpnAsync(frm.InterfaceName, frm.ListenPort, frm.Mtu, frm.NetworkIp, frm.PeerPublicKey, frm.PeerAllowedIp);
                    var ifs = await _svc.GetInterfacesAsync();
                    var serverIf = ifs.FirstOrDefault(i => i.Name == frm.InterfaceName);

                    if (!string.IsNullOrWhiteSpace(frm.ClientPrivateKey))
                    {
                        string cfg = WireGuardService.GenerateClientConfig(new WireGuardPeer(), serverIf?.PublicKey ?? "", frm.Endpoint, frm.ListenPort, frm.ClientPrivateKey, frm.PeerAllowedIp, "8.8.8.8");
                        using var sFrm = new VpnSuccessForm(cfg);
                        sFrm.ShowDialog();
                    }
                    await LoadDataAsync();
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                }
            }
        };

        AddToolbarButton("＋ Nova", Color.DodgerBlue).Click += async (_, _) =>
        {
            using var frm = new WgInterfaceEditForm();
            if (frm.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    await _svc.CreateInterfaceAsync(frm.WgInterface);

                    if (!string.IsNullOrWhiteSpace(frm.NetworkIp))
                    {
                        var ipSvc = new IpService(Client);
                        await ipSvc.AddAddressAsync(new IpAddress { Address = frm.NetworkIp, Interface = frm.WgInterface.Name, Comment = "VPN Network" });

                        await Client.PutAsync<NatRule>("ip/firewall/nat", new NatRule
                        {
                            Chain = "srcnat",
                            Action = "masquerade",
                            SrcAddress = frm.NetworkIp,
                            DstAddress = "0.0.0.0/0",
                            Comment = "VPN Masquerade (" + frm.WgInterface.Name + ")"
                        });
                    }

                    await LoadDataAsync();
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                }
            }
        };

        AddToolbarButton("✎ Editar", Color.Gray).Click += async (_, _) =>
        {
            if (Grid.SelectedRows.Count == 0) return;
            var id = Grid.SelectedRows[0].Cells[".id"].Value?.ToString();
            var existing = new WireGuardInterface
            {
                Name = Grid.SelectedRows[0].Cells["name"].Value?.ToString() ?? "",
                ListenPort = Grid.SelectedRows[0].Cells["port"].Value?.ToString() ?? ""
            };

            using var frm = new WgInterfaceEditForm(existing);
            if (frm.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    await _svc.UpdateInterfaceAsync(id!, new { listen_port = frm.WgInterface.ListenPort });
                    await LoadDataAsync();
                }
                catch (Exception ex) { ShowError(ex); }
            }
        };

        AddToolbarButton("✕ Apagar", Color.Firebrick).Click += async (_, _) =>
        {
            var id = SelectedId();
            if (id == null) return;
            if (MessageBox.Show("Apagar Interface e TODOS os Peers?", "Aviso", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                await _svc.DeleteInterfaceAsync(id);
                await LoadDataAsync();
            }
        };

        AddToolbarButton("👤 Gerir Peers", Color.SeaGreen).Click += async (_, _) =>
        {
            using var frm = new WireGuardPeersForm(_svc, Client);
            frm.ShowDialog();
            await LoadDataAsync();
        };

        AddToolbarButton("↺ Atualizar", Color.FromArgb(0, 100, 180)).Click += async (_, _) => await LoadDataAsync();

        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = ".id", Visible = false });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "name", HeaderText = "Nome", FillWeight = 20 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "port", HeaderText = "Porta", FillWeight = 15 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "pub", HeaderText = "Public Key", FillWeight = 50 });
    }

    protected override async Task LoadDataAsync()
    {
        try
        {
            var list = await _svc.GetInterfacesAsync();
            Grid.Rows.Clear();
            foreach (var i in list)
            {
                Grid.Rows.Add(i.Id, i.Name, i.ListenPort, i.PublicKey);
            }
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }
}

public class WireGuardPeersForm : Form
{
    private readonly WireGuardService _svc;
    private readonly MikroTikClient _client;
    private readonly DataGridView _grid = new();

    public WireGuardPeersForm(WireGuardService svc, MikroTikClient client)
    {
        _svc = svc;
        _client = client;
        Text = "Gestão de Peers WireGuard";
        Size = new Size(600, 400);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9.5f);

        var tbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 40,
            Padding = new Padding(5)
        };

        var bAdd = new Button
        {
            Text = "＋ Novo",
            Height = 30,
            BackColor = Color.DodgerBlue,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        bAdd.Click += async (_, _) =>
        {
            var ifs = await _svc.GetInterfacesAsync();
            if (ifs.Count == 0) return;

            var ipSvc = new IpService(client);
            var allIps = await ipSvc.GetAddressesAsync();
            var interfaceIps = new Dictionary<string, string>();
            foreach (var i in ifs)
            {
                var ipObj = allIps.FirstOrDefault(a => a.Interface == i.Name);
                interfaceIps[i.Name] = ipObj != null ? ipObj.Address : "";
            }

            using var f = new WgPeerEditForm(ifs.Select(i => i.Name).ToList(), _client, interfaceIps);
            if (f.ShowDialog() == DialogResult.OK)
            {
                await _svc.AddPeerAsync(f.Peer);
                if (!string.IsNullOrWhiteSpace(f.GeneratedPrivateKey))
                {
                    var sIf = ifs.FirstOrDefault(i => i.Name == f.Peer.Interface);
                    string cfg = WireGuardService.GenerateClientConfig(f.Peer, sIf?.PublicKey ?? "", f.Endpoint, sIf?.ListenPort ?? "51820", f.GeneratedPrivateKey, f.Peer.AllowedAddress, "8.8.8.8");
                    using var s = new VpnSuccessForm(cfg);
                    s.ShowDialog();
                }
                await LoadData();
            }
        };

        var bEdit = new Button
        {
            Text = "✎ Editar",
            Height = 30,
            BackColor = Color.Gray,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        bEdit.Click += async (_, _) =>
        {
            if (_grid.SelectedRows.Count == 0) return;
            var row = _grid.SelectedRows[0];
            var id = row.Cells[".id"].Value?.ToString();
            if (id == null) return;

            var ifs = await _svc.GetInterfacesAsync();
            var ipSvc = new IpService(client);
            var allIps = await ipSvc.GetAddressesAsync();
            var interfaceIps = new Dictionary<string, string>();
            foreach (var i in ifs)
            {
                var ipObj = allIps.FirstOrDefault(a => a.Interface == i.Name);
                interfaceIps[i.Name] = ipObj != null ? ipObj.Address : "";
            }

            var exi = new WireGuardPeer
            {
                Interface = row.Cells["interface"].Value?.ToString() ?? "",
                PublicKey = row.Cells["public-key"].Value?.ToString() ?? "",
                AllowedAddress = row.Cells["allowed-address"].Value?.ToString() ?? "",
                Comment = row.Cells["comment"].Value?.ToString() ?? ""
            };

            using var f = new WgPeerEditForm(ifs.Select(i => i.Name).ToList(), _client, interfaceIps, exi);
            if (f.ShowDialog() == DialogResult.OK)
            {
                await _svc.UpdatePeerAsync(id, new { @interface = f.Peer.Interface, @allowed_address = f.Peer.AllowedAddress, @comment = f.Peer.Comment });
                await LoadData();
            }
        };

        var bDel = new Button
        {
            Text = "✕ Apagar",
            Height = 30,
            BackColor = Color.Firebrick,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        bDel.Click += async (_, _) =>
        {
            if (_grid.SelectedRows.Count == 0) return;
            var id = _grid.SelectedRows[0].Cells[".id"].Value?.ToString();

            if (MessageBox.Show("Apagar?", "Aviso", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                await _svc.DeletePeerAsync(id!);
                await LoadData();
            }
        };

        var bExp = new Button
        {
            Text = "📄 Ver QR",
            Height = 30,
            BackColor = Color.SlateBlue,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        bExp.Click += async (_, _) =>
        {
            if (_grid.SelectedRows.Count == 0) return;
            var iface = _grid.SelectedRows[0].Cells["interface"].Value?.ToString();
            var ip = _grid.SelectedRows[0].Cells["allowed-address"].Value?.ToString();
            var pubKey = _grid.SelectedRows[0].Cells["public-key"].Value?.ToString();
            var ifs = await _svc.GetInterfacesAsync();

            using var f = new WgClientConfigForm(client, ifs, iface, ip, pubKey);
            f.ShowDialog();
        };

        tbar.Controls.AddRange(new Control[] { bAdd, bEdit, bDel, bExp });

        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.BackgroundColor = Color.White;
        _grid.RowHeadersVisible = false;

        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = ".id", Visible = false });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "interface", HeaderText = "Interface" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "public-key", HeaderText = "Public Key" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "allowed-address", HeaderText = "IP Permitido" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "comment", HeaderText = "Comentário" });

        Controls.Add(_grid);
        Controls.Add(tbar);

        Load += async (_, _) => await LoadData();
    }

    private async Task LoadData()
    {
        try
        {
            var l = await _svc.GetPeersAsync();
            _grid.Rows.Clear();
            foreach (var p in l)
            {
                _grid.Rows.Add(p.Id, p.Interface, p.PublicKey, p.AllowedAddress, p.Comment);
            }
        }
        catch { }
    }
}

public class VpnQuickSetupForm : Form
{
    public string InterfaceName => _txtName.Text.Trim();
    public string ListenPort => _txtPort.Text.Trim();
    public string Mtu => _txtMtu.Text.Trim();
    public string NetworkIp => _txtNetwork.Text.Trim();
    public string PeerPublicKey => _txtPeerPubKey.Text.Trim();
    public string PeerAllowedIp => _txtPeerIp.Text.Trim();
    public string Endpoint => _txtEndpoint.Text.Trim();
    public string ClientPrivateKey { get; private set; } = "";

    private readonly TextBox _txtName = new();
    private readonly TextBox _txtPort = new();
    private readonly TextBox _txtMtu = new();
    private readonly TextBox _txtNetwork = new();
    private readonly TextBox _txtPeerPubKey = new();
    private readonly TextBox _txtPeerIp = new();
    private readonly TextBox _txtEndpoint = new();

    public VpnQuickSetupForm(MikroTikClient client)
    {
        Text = "Setup VPN";
        Size = new Size(480, 420);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        Font = new Font("Segoe UI", 9f);

        int y = 20;

        Controls.Add(new Label { Text = "Nome:", Location = new Point(20, y + 3), AutoSize = true });
        _txtName.Location = new Point(150, y);
        _txtName.Width = 200;
        Controls.Add(_txtName);
        y += 35;

        Controls.Add(new Label { Text = "Porta:", Location = new Point(20, y + 3), AutoSize = true });
        _txtPort.Location = new Point(150, y);
        _txtPort.Width = 200;
        Controls.Add(_txtPort);
        y += 35;

        Controls.Add(new Label { Text = "MTU:", Location = new Point(20, y + 3), AutoSize = true });
        _txtMtu.Location = new Point(150, y);
        _txtMtu.Width = 200;
        Controls.Add(_txtMtu);
        y += 35;

        Controls.Add(new Label { Text = "Rede VPN (Ex: 10.10.10.1/24):", Location = new Point(20, y + 3), AutoSize = true });
        _txtNetwork.Location = new Point(150, y);
        _txtNetwork.Width = 200;
        Controls.Add(_txtNetwork);
        y += 35;

        Controls.Add(new Label { Text = "IP Peer (Ex: 10.10.10.2/32):", Location = new Point(20, y + 3), AutoSize = true });
        _txtPeerIp.Location = new Point(150, y);
        _txtPeerIp.Width = 200;
        Controls.Add(_txtPeerIp);
        y += 35;

        Controls.Add(new Label { Text = "Public Key Peer:", Location = new Point(20, y + 3), AutoSize = true });
        _txtPeerPubKey.Location = new Point(150, y);
        _txtPeerPubKey.Width = 200;
        Controls.Add(_txtPeerPubKey);
        y += 35;

        Controls.Add(new Label { Text = "Endpoint:", Location = new Point(20, y + 3), AutoSize = true });
        _txtEndpoint.Location = new Point(150, y);
        _txtEndpoint.Width = 200;
        Controls.Add(_txtEndpoint);

        var btnDdns = new Button
        {
            Text = "☁️ DDNS",
            Location = new Point(360, y),
            Size = new Size(80, 25),
            BackColor = Color.LightGray,
            FlatStyle = FlatStyle.Flat
        };
        btnDdns.FlatAppearance.BorderSize = 0;
        btnDdns.Click += async (s, e) =>
        {
            try
            {
                var cloud = await client.GetSingleAsync<MikroTikCloudSettings>("ip/cloud");
                if (cloud != null && !string.IsNullOrEmpty(cloud.DnsName)) _txtEndpoint.Text = cloud.DnsName;
                else MessageBox.Show("DDNS não está ativado no router (IP > Cloud).", "Aviso");
            }
            catch { MessageBox.Show("Erro ao obter DDNS.", "Erro"); }
        };
        Controls.Add(btnDdns);
        y += 35;

        var btnK = new Button
        {
            Text = "🔑 Gerar Chave",
            Location = new Point(150, y),
            Width = 200,
            BackColor = Color.SeaGreen,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnK.Click += (s, e) =>
        {
            var k = WireGuardKeyGen.Generate();
            ClientPrivateKey = k.PrivateKey;
            _txtPeerPubKey.Text = k.PublicKey;
        };
        Controls.Add(btnK);
        y += 40;

        var btnO = new Button
        {
            Text = "Executar",
            Location = new Point(150, y),
            Width = 200,
            Height = 35,
            BackColor = Color.DodgerBlue,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            DialogResult = DialogResult.OK
        };
        btnO.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(ClientPrivateKey))
            {
                WireGuardKeyStore.Save(client.Host, _txtPeerPubKey.Text, ClientPrivateKey);
            }
        };

        Controls.Add(btnO);

        _txtName.Text = "wg-vpn";
        _txtPort.Text = "51820";
        _txtMtu.Text = "1420";
        _txtEndpoint.Text = client.Host;

        AcceptButton = btnO;
    }
}

public class VpnSuccessForm : Form
{
    public VpnSuccessForm(string data)
    {
        Text = "Configuração Gerada";
        Size = new Size(540, 480);
        StartPosition = FormStartPosition.CenterParent;

        var t = new TextBox
        {
            Location = new Point(20, 40),
            Size = new Size(240, 320),
            Multiline = true,
            ReadOnly = true,
            Text = data,
            Font = new Font("Consolas", 8f)
        };

        var p = new PictureBox
        {
            Location = new Point(270, 40),
            Size = new Size(230, 230),
            SizeMode = PictureBoxSizeMode.Zoom,
            BorderStyle = BorderStyle.FixedSingle
        };

        Task.Run(async () =>
        {
            try
            {
                using var h = new System.Net.Http.HttpClient();
                var b = await h.GetByteArrayAsync($"https://api.qrserver.com/v1/create-qr-code/?size=250x250&data={Uri.EscapeDataString(data)}");
                p.Invoke(new Action(() =>
                {
                    using var m = new System.IO.MemoryStream(b);
                    p.Image = new Bitmap(m);
                }));
            }
            catch { }
        });

        var bS = new Button
        {
            Text = "💾 Guardar .conf",
            Location = new Point(20, 380),
            Width = 200,
            Height = 35,
            BackColor = Color.SeaGreen,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        bS.Click += (s, e) =>
        {
            var d = new SaveFileDialog { Filter = "Config|*.conf" };
            if (d.ShowDialog() == DialogResult.OK)
            {
                File.WriteAllText(d.FileName, data);
            }
        };

        Controls.AddRange(new Control[] { t, p, bS });
    }
}

public class WgInterfaceEditForm : Form
{
    public WireGuardInterface WgInterface { get; private set; } = new();
    public string NetworkIp => _txtNetwork.Text.Trim();

    private readonly TextBox _txtName = new();
    private readonly TextBox _txtPort = new();
    private readonly TextBox _txtNetwork = new();
    private readonly TextBox _txtComment = new();

    public WgInterfaceEditForm(WireGuardInterface? existing = null)
    {
        Text = existing == null ? "Nova Interface" : "Editar Interface";
        Size = new Size(340, existing == null ? 290 : 160);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        Font = new Font("Segoe UI", 9.5f);

        int y = 20;

        Controls.Add(new Label { Text = "Nome:", Location = new Point(20, y + 3), AutoSize = true });
        _txtName.Location = new Point(120, y);
        _txtName.Width = 185;
        Controls.Add(_txtName);
        y += 38;

        Controls.Add(new Label { Text = "Porta:", Location = new Point(20, y + 3), AutoSize = true });
        _txtPort.Location = new Point(120, y);
        _txtPort.Width = 185;
        _txtPort.Text = "13231";
        Controls.Add(_txtPort);
        y += 38;

        if (existing != null)
        {
            _txtName.Text = existing.Name;
            _txtName.Enabled = false;
            _txtPort.Text = existing.ListenPort;
        }
        else
        {
            Controls.Add(new Label { Text = "Rede (CIDR):", Location = new Point(20, y + 3), AutoSize = true });
            _txtNetwork.Location = new Point(120, y);
            _txtNetwork.Width = 185;
            Controls.Add(_txtNetwork);
            y += 24;

            var hint = new Label { Text = "Ex: 10.253.0.1/24 (Cria IP e regra NAT)", Location = new Point(120, y), AutoSize = true, ForeColor = Color.Gray, Font = new Font("Segoe UI", 8f) };
            Controls.Add(hint);
            y += 22;

            Controls.Add(new Label { Text = "Comentário:", Location = new Point(20, y + 3), AutoSize = true });
            _txtComment.Location = new Point(120, y);
            _txtComment.Width = 185;
            Controls.Add(_txtComment);
            y += 38;
        }

        var b = new Button
        {
            Text = existing == null ? "Criar Interface" : "Guardar",
            Location = new Point(120, y),
            Size = new Size(185, 30),
            BackColor = Color.DodgerBlue,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        b.FlatAppearance.BorderSize = 0;

        b.Click += (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(_txtName.Text))
            {
                MessageBox.Show("Nome obrigatório.");
                return;
            }

            WgInterface = new WireGuardInterface
            {
                Name = _txtName.Text.Trim(),
                ListenPort = _txtPort.Text.Trim(),
                Comment = _txtComment.Text.Trim()
            };

            DialogResult = DialogResult.OK;
            Close();
        };

        Controls.Add(b);
        AcceptButton = b;
    }
}

public class WgPeerEditForm : Form
{
    public WireGuardPeer Peer { get; private set; } = new();
    public string GeneratedPrivateKey { get; private set; } = "";
    public string Endpoint => _txtEndpoint.Text.Trim();

    private readonly TextBox _tNo = new();
    private readonly TextBox _tPu = new();
    private readonly TextBox _tIp = new();
    private readonly ComboBox _cI = new();
    private readonly TextBox _txtEndpoint = new();

    public WgPeerEditForm(List<string> ifs, MikroTikClient client, Dictionary<string, string> interfaceIps, WireGuardPeer? existing = null)
    {
        Text = existing == null ? "Novo Peer (Cliente)" : "Editar Peer (Cliente)";
        Size = new Size(500, 420);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        Font = new Font("Segoe UI", 9.5f);

        int y = 20;

        Controls.Add(new Label { Text = "Nome:", Location = new Point(20, y + 3), AutoSize = true });
        _tNo.Location = new Point(180, y);
        _tNo.Width = 200;
        Controls.Add(_tNo);
        y += 40;

        Controls.Add(new Label { Text = "Public Key:", Location = new Point(20, y + 3), AutoSize = true });
        _tPu.Location = new Point(180, y);
        _tPu.Width = 200;
        Controls.Add(_tPu);
        y += 40;

        var bK = new Button
        {
            Text = "🔑 Gerar",
            Location = new Point(180, y),
            Width = 200,
            BackColor = Color.SeaGreen,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        bK.FlatAppearance.BorderSize = 0;
        bK.Click += (s, e) =>
        {
            var k = WireGuardKeyGen.Generate();
            GeneratedPrivateKey = k.PrivateKey;
            _tPu.Text = k.PublicKey;
        };
        Controls.Add(bK);
        y += 45;

        Controls.Add(new Label { Text = "IP Cliente:", Location = new Point(20, y + 3), AutoSize = true });
        _tIp.Location = new Point(180, y);
        _tIp.Width = 200;
        Controls.Add(_tIp);
        y += 26;

        var hintLbl = new Label
        {
            Text = "💡 Indique o IP do cliente com /32.",
            Location = new Point(180, y),
            AutoSize = true,
            ForeColor = Color.Gray,
            Font = new Font("Segoe UI", 8f)
        };
        Controls.Add(hintLbl);
        y += 35;

        Controls.Add(new Label { Text = "Interface:", Location = new Point(20, y + 3), AutoSize = true });
        _cI.Location = new Point(180, y);
        _cI.Width = 200;
        foreach (var i in ifs) _cI.Items.Add(i);
        _cI.DropDownStyle = ComboBoxStyle.DropDownList;

        _cI.SelectedIndexChanged += (s, e) =>
        {
            string selectedIf = _cI.Text;
            if (interfaceIps.TryGetValue(selectedIf, out string ipCidr) && !string.IsNullOrWhiteSpace(ipCidr))
            {
                string baseIp = ipCidr.Split('/')[0];
                int lastDot = baseIp.LastIndexOf('.');
                string baseNet = lastDot > 0 ? baseIp.Substring(0, lastDot + 1) : "10.0.0.";
                hintLbl.Text = $"💡 IP Servidor: {ipCidr}\nEx: {baseNet}2/32 (Tem de pertencer à rede)";
            }
            else
            {
                hintLbl.Text = $"💡 Indique o IP do cliente com /32.\n(A interface '{selectedIf}' não tem IP configurado)";
            }
        };

        if (_cI.Items.Count > 0) _cI.SelectedIndex = 0;
        Controls.Add(_cI);
        y += 40;

        Controls.Add(new Label { Text = "Endpoint Servidor:", Location = new Point(20, y + 3), AutoSize = true });
        _txtEndpoint.Location = new Point(180, y);
        _txtEndpoint.Width = 200;
        _txtEndpoint.Text = client.Host;
        Controls.Add(_txtEndpoint);

        var btnDdns = new Button
        {
            Text = "☁️ DDNS",
            Location = new Point(390, y),
            Width = 80,
            Height = 25,
            BackColor = Color.LightGray,
            FlatStyle = FlatStyle.Flat
        };
        btnDdns.FlatAppearance.BorderSize = 0;
        btnDdns.Click += async (s, e) =>
        {
            try
            {
                var cloud = await client.GetSingleAsync<MikroTikCloudSettings>("ip/cloud");
                if (cloud != null && !string.IsNullOrEmpty(cloud.DnsName)) _txtEndpoint.Text = cloud.DnsName;
                else MessageBox.Show("DDNS inativo no router.");
            }
            catch { MessageBox.Show("Erro ao obter DDNS."); }
        };
        Controls.Add(btnDdns);
        y += 40;

        if (existing != null)
        {
            _tNo.Text = existing.Comment;
            _tPu.Text = existing.PublicKey;
            _tPu.ReadOnly = true;
            _tIp.Text = existing.AllowedAddress;
            if (_cI.Items.Contains(existing.Interface)) _cI.SelectedItem = existing.Interface;
            bK.Enabled = false;
        }
        else
        {
            _tIp.Text = "10.253.0.2/32";
        }

        var bO = new Button
        {
            Text = existing == null ? "Adicionar Peer" : "Guardar",
            Location = new Point(180, y),
            Width = 200,
            BackColor = Color.DodgerBlue,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        bO.FlatAppearance.BorderSize = 0;
        bO.Click += (s, e) =>
        {
            if (!string.IsNullOrWhiteSpace(GeneratedPrivateKey))
            {
                WireGuardKeyStore.Save(client.Host, _tPu.Text, GeneratedPrivateKey);
            }

            Peer = new WireGuardPeer
            {
                Interface = _cI.Text,
                PublicKey = _tPu.Text,
                AllowedAddress = _tIp.Text,
                Comment = _tNo.Text
            };
            DialogResult = DialogResult.OK;
            Close();
        };

        Controls.Add(bO);
        AcceptButton = bO;
    }
}

public class WgClientConfigForm : Form
{
    public WgClientConfigForm(MikroTikClient client, List<WireGuardInterface> ifs, string? sI, string? sIp, string? peerPubKey)
    {
        Text = "Configuração QR";
        Size = new Size(560, 350);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;

        var cbo = new ComboBox
        {
            Location = new Point(180, 20),
            Width = 250,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        foreach (var i in ifs) cbo.Items.Add(i.Name);
        if (sI != null && cbo.Items.Contains(sI)) cbo.SelectedItem = sI;
        else if (cbo.Items.Count > 0) cbo.SelectedIndex = 0;

        var tK = new TextBox { Location = new Point(180, 60), Width = 250 };

        if (peerPubKey != null)
        {
            tK.Text = WireGuardKeyStore.GetPrivateKey(client.Host, peerPubKey) ?? "";
        }

        var tE = new TextBox { Location = new Point(180, 100), Width = 250 };
        tE.Text = client.Host;

        Controls.Add(new Label { Text = "Interface:", Location = new Point(20, 23), AutoSize = true });
        Controls.Add(new Label { Text = "Chave Privada Cliente:", Location = new Point(20, 63), AutoSize = true });
        Controls.Add(new Label { Text = "Endpoint Servidor:", Location = new Point(20, 103), AutoSize = true });

        var btnDdns = new Button
        {
            Text = "☁️ DDNS",
            Location = new Point(440, 100),
            Width = 80,
            Height = 25,
            BackColor = Color.LightGray,
            FlatStyle = FlatStyle.Flat
        };
        btnDdns.FlatAppearance.BorderSize = 0;
        btnDdns.Click += async (s, e) =>
        {
            try
            {
                var cloud = await client.GetSingleAsync<MikroTikCloudSettings>("ip/cloud");
                if (cloud != null && !string.IsNullOrEmpty(cloud.DnsName)) tE.Text = cloud.DnsName;
                else MessageBox.Show("DDNS não está ativado no router.");
            }
            catch { MessageBox.Show("Erro ao obter DDNS."); }
        };
        Controls.Add(btnDdns);

        var btn = new Button
        {
            Text = "Gerar QR Code",
            Location = new Point(180, 150),
            Width = 250,
            Height = 40,
            BackColor = Color.DodgerBlue,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btn.Click += (s, e) =>
        {
            var i = ifs.FirstOrDefault(x => x.Name == cbo.Text);
            if (i == null || string.IsNullOrWhiteSpace(tK.Text)) return;

            string c = WireGuardService.GenerateClientConfig(new WireGuardPeer(), i.PublicKey, tE.Text, i.ListenPort, tK.Text, sIp ?? "");
            using var f = new VpnSuccessForm(c);
            f.ShowDialog();
            Close();
        };

        Controls.AddRange(new Control[] { cbo, tK, tE, btn });
    }
}