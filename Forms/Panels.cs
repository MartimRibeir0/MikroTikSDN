using MikroTikSDN.Api;
using MikroTikSDN.Models;
using MikroTikSDN.Services;
using System.Security.Cryptography;
using Chaos.NaCl; // Requer o pacote NuGet Chaos.NaCl.Standard

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

    protected void ShowError(Exception ex) =>
        MessageBox.Show(ex.Message, "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);

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
    private List<MikrotikInterface> _all = new();
    private readonly CheckBox _chkWirelessOnly = new() { Text = "Apenas Wireless", AutoSize = true, Margin = new Padding(0, 8, 10, 0) };

    public InterfacesPanel(MikroTikClient client) : base(client)
    {
        _svc = new InterfaceService(client);

        Toolbar.Controls.Add(_chkWirelessOnly);
        AddToolbarButton("✔ Ativar", Color.FromArgb(0, 140, 80)).Click += async (_, _) => await ToggleAsync(true);
        AddToolbarButton("✕ Desativar", Color.FromArgb(160, 40, 40)).Click += async (_, _) => await ToggleAsync(false);
        AddToolbarButton("↺ Atualizar", Color.FromArgb(0, 100, 180)).Click += async (_, _) => await LoadDataAsync();

        _chkWirelessOnly.CheckedChanged += (_, _) => ApplyFilter();

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
        try { _all = await _svc.GetAllAsync(); ApplyFilter(); }
        catch (Exception ex) { ShowError(ex); }
    }

    private void ApplyFilter()
    {
        Grid.Rows.Clear();
        var list = _chkWirelessOnly.Checked
            ? _all.Where(i => i.Type.Contains("wireless", StringComparison.OrdinalIgnoreCase)).ToList()
            : _all;

        foreach (var i in list)
            Grid.Rows.Add(i.Id, i.Name, i.Type, i.MacAddress, i.Mtu, i.Running ? "✔" : "✘", i.Disabled ? "Sim" : "Não");
    }

    private async Task ToggleAsync(bool enable)
    {
        var id = SelectedId();
        if (id == null) { MessageBox.Show("Seleciona uma interface."); return; }
        try { if (enable) await _svc.EnableAsync(id); else await _svc.DisableAsync(id); await LoadDataAsync(); }
        catch (Exception ex) { ShowError(ex); }
    }
}

// ════════════════════════════════════════════════════════════════════
//  WIRELESS PANEL
// ════════════════════════════════════════════════════════════════════

public class WirelessPanel : BasePanel
{
    private readonly WirelessService _svc;
    private List<WirelessInterface> _list = new();
    private readonly TabControl _innerTabs = new();
    private readonly DataGridView _gridProfiles = new();

    public WirelessPanel(MikroTikClient client) : base(client)
    {
        _svc = new WirelessService(client);
        Controls.Remove(Grid);

        _innerTabs.Dock = DockStyle.Fill;
        var tabIf = new TabPage("Interfaces Wireless");
        Grid.Dock = DockStyle.Fill;
        tabIf.Controls.Add(Grid);

        var tabProf = new TabPage("Perfis de Segurança");
        StyleGrid(_gridProfiles);
        _gridProfiles.Dock = DockStyle.Fill;
        tabProf.Controls.Add(_gridProfiles);

        _innerTabs.TabPages.AddRange(new[] { tabIf, tabProf });
        _innerTabs.SelectedIndexChanged += async (_, _) => await LoadDataAsync();
        Controls.Add(_innerTabs);

        AddToolbarButton("✔ Ativar", Color.FromArgb(0, 140, 80)).Click += async (_, _) => await ToggleIfAsync(true);
        AddToolbarButton("✕ Desativar", Color.FromArgb(160, 40, 40)).Click += async (_, _) => await ToggleIfAsync(false);
        AddToolbarButton("↺ Atualizar", Color.FromArgb(0, 100, 180)).Click += async (_, _) => await LoadDataAsync();

        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = ".id", HeaderText = "ID", Visible = false });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "name", HeaderText = "Nome", FillWeight = 15 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ssid", HeaderText = "SSID", FillWeight = 20 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "disabled", HeaderText = "Disabled", FillWeight = 10 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "sec", HeaderText = "Segurança", FillWeight = 20 });

        _gridProfiles.Columns.Add(new DataGridViewTextBoxColumn { Name = ".id", HeaderText = "ID", Visible = false });
        _gridProfiles.Columns.Add(new DataGridViewTextBoxColumn { Name = "name", HeaderText = "Nome", FillWeight = 25 });
        _gridProfiles.Columns.Add(new DataGridViewTextBoxColumn { Name = "mode", HeaderText = "Modo", FillWeight = 25 });
        _gridProfiles.Columns.Add(new DataGridViewTextBoxColumn { Name = "auth", HeaderText = "Auth", FillWeight = 25 });
    }

    private static void StyleGrid(DataGridView g)
    {
        g.ReadOnly = true; g.AllowUserToAddRows = false; g.AllowUserToDeleteRows = false;
        g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        g.BackgroundColor = Color.White; g.RowHeadersVisible = false;
        g.BorderStyle = BorderStyle.None; g.EnableHeadersVisualStyles = false;
        g.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(40, 40, 40), ForeColor = Color.White, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
    }

    protected override async Task LoadDataAsync()
    {
        try
        {
            if (_innerTabs.SelectedIndex == 0)
            {
                _list = await _svc.GetInterfacesAsync();
                Grid.Rows.Clear();
                foreach (var i in _list)
                    Grid.Rows.Add(i.Id, i.Name, i.Ssid, i.Disabled ? "Sim" : "Não", i.SecurityProfile);
            }
            else
            {
                var profiles = await _svc.GetSecurityProfilesAsync();
                _gridProfiles.Rows.Clear();
                foreach (var p in profiles)
                    _gridProfiles.Rows.Add(p.Id, p.Name, p.Mode, p.AuthenticationTypes);
            }
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private async Task ToggleIfAsync(bool enable)
    {
        var id = SelectedId();
        if (id == null) { MessageBox.Show("Seleciona uma interface wireless."); return; }
        try { if (enable) await _svc.EnableInterfaceAsync(id); else await _svc.DisableInterfaceAsync(id); await LoadDataAsync(); }
        catch (Exception ex) { ShowError(ex); }
    }
}

// ════════════════════════════════════════════════════════════════════
//  BRIDGE PANEL E FORM
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
                Grid.Rows.Add(b.Id, b.Name, b.MacAddress, b.VlanFiltering ? "Sim" : "Não", b.Running ? "✔" : "✘", b.Disabled ? "Sim" : "Não");
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private async void BtnAdd_Click(object? s, EventArgs e)
    {
        using var frm = new BridgeEditForm();
        if (frm.ShowDialog() != DialogResult.OK) return;
        try { await _svc.CreateBridgeAsync(frm.Bridge); await LoadDataAsync(); }
        catch (Exception ex) { ShowError(ex); }
    }

    private async void BtnEdit_Click(object? s, EventArgs e)
    {
        var id = SelectedId(); if (id == null) { MessageBox.Show("Seleciona uma bridge."); return; }
        var row = Grid.SelectedRows[0];
        var existing = new BridgeInterface { Id = id, Name = row.Cells["name"].Value?.ToString() ?? "" };
        using var frm = new BridgeEditForm(existing);
        if (frm.ShowDialog() != DialogResult.OK) return;
        try { await _svc.UpdateBridgeAsync(id, new { name = frm.Bridge.Name, comment = frm.Bridge.Comment }); await LoadDataAsync(); }
        catch (Exception ex) { ShowError(ex); }
    }

    private async Task DeleteAsync()
    {
        var id = SelectedId(); if (id == null) { MessageBox.Show("Seleciona uma bridge."); return; }
        if (MessageBox.Show("Apagar esta bridge?", "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        try { await _svc.DeleteBridgeAsync(id); await LoadDataAsync(); }
        catch (Exception ex) { ShowError(ex); }
    }
}

public class BridgeEditForm : Form
{
    public BridgeInterface Bridge { get; private set; } = new();
    private readonly TextBox _txtName = new();
    private readonly TextBox _txtComment = new();

    public BridgeEditForm(BridgeInterface? existing = null)
    {
        Text = existing == null ? "Nova Bridge" : "Editar Bridge";
        Size = new Size(320, 180);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9.5f);

        Controls.Add(new Label { Text = "Nome:", Location = new Point(14, 20), AutoSize = true });
        _txtName.Location = new Point(100, 17); _txtName.Width = 185; Controls.Add(_txtName);

        Controls.Add(new Label { Text = "Comentário:", Location = new Point(14, 58), AutoSize = true });
        _txtComment.Location = new Point(100, 55); _txtComment.Width = 185; Controls.Add(_txtComment);

        if (existing != null) { _txtName.Text = existing.Name; _txtComment.Text = existing.Comment; }

        var ok = new Button
        {
            Text = "Guardar",
            Location = new Point(100, 95),
            Size = new Size(85, 28),
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        ok.FlatAppearance.BorderSize = 0;
        ok.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_txtName.Text)) { MessageBox.Show("Nome obrigatório."); return; }
            Bridge = new BridgeInterface { Name = _txtName.Text.Trim(), Comment = _txtComment.Text };
            DialogResult = DialogResult.OK; Close();
        };
        Controls.Add(ok);
        AcceptButton = ok;
    }
}

// ════════════════════════════════════════════════════════════════════
//  IP PANEL E FORM
// ════════════════════════════════════════════════════════════════════

public class IpPanel : BasePanel
{
    private readonly IpService _svc;

    public IpPanel(MikroTikClient client) : base(client)
    {
        _svc = new IpService(client);
        AddToolbarButton("＋ Adicionar", Color.FromArgb(0, 120, 215)).Click += BtnAdd_Click;
        AddToolbarButton("✕ Apagar", Color.FromArgb(180, 30, 30)).Click += async (_, _) => await DeleteAsync();
        AddToolbarButton("↺ Atualizar", Color.FromArgb(0, 100, 180)).Click += async (_, _) => await LoadDataAsync();

        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = ".id", HeaderText = "ID", Visible = false });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "address", HeaderText = "Endereço", FillWeight = 30 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "network", HeaderText = "Rede", FillWeight = 25 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "interface", HeaderText = "Interface", FillWeight = 25 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "dynamic", HeaderText = "Dinâmico", FillWeight = 10 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "disabled", HeaderText = "Disabled", FillWeight = 10 });
    }

    protected override async Task LoadDataAsync()
    {
        try
        {
            var list = await _svc.GetAddressesAsync();
            Grid.Rows.Clear();
            foreach (var a in list)
                Grid.Rows.Add(a.Id, a.Address, a.Network, a.Interface, a.Dynamic ? "Sim" : "Não", a.Disabled ? "Sim" : "Não");
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private async void BtnAdd_Click(object? s, EventArgs e)
    {
        using var frm = new IpAddressEditForm();
        if (frm.ShowDialog() != DialogResult.OK) return;
        try { await _svc.AddAddressAsync(frm.Address); await LoadDataAsync(); }
        catch (Exception ex) { ShowError(ex); }
    }

    private async Task DeleteAsync()
    {
        var id = SelectedId(); if (id == null) { MessageBox.Show("Seleciona um endereço."); return; }
        if (MessageBox.Show("Apagar este endereço IP?", "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        try { await _svc.DeleteAddressAsync(id); await LoadDataAsync(); }
        catch (Exception ex) { ShowError(ex); }
    }
}

public class IpAddressEditForm : Form
{
    public IpAddress Address { get; private set; } = new();
    private readonly TextBox _txtAddress = new();
    private readonly TextBox _txtInterface = new();
    private readonly TextBox _txtComment = new();

    public IpAddressEditForm()
    {
        Text = "Adicionar Endereço IP"; Size = new Size(340, 210);
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;
        StartPosition = FormStartPosition.CenterParent; Font = new Font("Segoe UI", 9.5f);

        int y = 18;
        void Row(string lbl, TextBox tb)
        {
            Controls.Add(new Label { Text = lbl, Location = new Point(14, y + 3), AutoSize = true });
            tb.Location = new Point(120, y); tb.Width = 185; Controls.Add(tb); y += 38;
        }
        Row("Endereço (CIDR):", _txtAddress);
        Row("Interface:", _txtInterface);
        Row("Comentário:", _txtComment);

        var hint = new Label
        {
            Text = "ex: 192.168.1.1/24",
            Location = new Point(120, y - 26),
            AutoSize = true,
            ForeColor = Color.Gray,
            Font = new Font("Segoe UI", 8f)
        };
        Controls.Add(hint);

        var ok = new Button
        {
            Text = "Guardar",
            Location = new Point(120, y + 8),
            Size = new Size(85, 28),
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        ok.FlatAppearance.BorderSize = 0;
        ok.Click += (_, _) => {
            if (string.IsNullOrWhiteSpace(_txtAddress.Text) || string.IsNullOrWhiteSpace(_txtInterface.Text))
            { MessageBox.Show("Endereço e Interface são obrigatórios."); return; }
            Address = new IpAddress { Address = _txtAddress.Text.Trim(), Interface = _txtInterface.Text.Trim(), Comment = _txtComment.Text };
            DialogResult = DialogResult.OK; Close();
        };
        Controls.Add(ok); AcceptButton = ok;
    }
}

// ════════════════════════════════════════════════════════════════════
//  ROUTES PANEL E FORM
// ════════════════════════════════════════════════════════════════════

public class RoutesPanel : BasePanel
{
    private readonly RoutingService _svc;

    public RoutesPanel(MikroTikClient client) : base(client)
    {
        _svc = new RoutingService(client);
        AddToolbarButton("＋ Nova Rota", Color.FromArgb(0, 120, 215)).Click += BtnAdd_Click;
        AddToolbarButton("✕ Apagar", Color.FromArgb(180, 30, 30)).Click += async (_, _) => await DeleteAsync();
        AddToolbarButton("✔ Ativar", Color.FromArgb(0, 140, 80)).Click += async (_, _) => await ToggleAsync(true);
        AddToolbarButton("✕ Desativar", Color.FromArgb(100, 60, 60)).Click += async (_, _) => await ToggleAsync(false);
        AddToolbarButton("↺ Atualizar", Color.FromArgb(0, 100, 180)).Click += async (_, _) => await LoadDataAsync();

        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = ".id", HeaderText = "ID", Visible = false });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "dst-address", HeaderText = "Destino", FillWeight = 30 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "gateway", HeaderText = "Gateway", FillWeight = 25 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "distance", HeaderText = "Distância", FillWeight = 15 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "active", HeaderText = "Ativa", FillWeight = 15 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "dynamic", HeaderText = "Dinâmica", FillWeight = 15 });
    }

    protected override async Task LoadDataAsync()
    {
        try
        {
            var list = await _svc.GetRoutesAsync();
            Grid.Rows.Clear();
            foreach (var r in list)
                Grid.Rows.Add(r.Id, r.DstAddress, r.Gateway, r.Distance, r.Active ? "✔" : "✘", r.Dynamic ? "Sim" : "Não");
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private async void BtnAdd_Click(object? s, EventArgs e)
    {
        using var frm = new RouteEditForm();
        if (frm.ShowDialog() != DialogResult.OK) return;
        try { await _svc.AddRouteAsync(frm.Route); await LoadDataAsync(); }
        catch (Exception ex) { ShowError(ex); }
    }

    private async Task DeleteAsync()
    {
        var id = SelectedId(); if (id == null) { MessageBox.Show("Seleciona uma rota."); return; }
        if (MessageBox.Show("Apagar esta rota?", "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        try { await _svc.DeleteRouteAsync(id); await LoadDataAsync(); }
        catch (Exception ex) { ShowError(ex); }
    }

    private async Task ToggleAsync(bool enable)
    {
        var id = SelectedId(); if (id == null) { MessageBox.Show("Seleciona uma rota."); return; }
        try
        {
            if (enable) await _svc.EnableRouteAsync(id); else await _svc.DisableRouteAsync(id);
            await LoadDataAsync();
        }
        catch (Exception ex) { ShowError(ex); }
    }
}

public class RouteEditForm : Form
{
    public StaticRoute Route { get; private set; } = new();
    private readonly TextBox _txtDst = new(), _txtGw = new(), _txtComment = new();
    private readonly NumericUpDown _numDist = new() { Minimum = 1, Maximum = 255, Value = 1 };

    public RouteEditForm()
    {
        Text = "Nova Rota Estática"; Size = new Size(340, 230);
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;
        StartPosition = FormStartPosition.CenterParent; Font = new Font("Segoe UI", 9.5f);

        int y = 18;
        void Row(string lbl, Control ctrl)
        {
            Controls.Add(new Label { Text = lbl, Location = new Point(14, y + 3), AutoSize = true });
            ctrl.Location = new Point(140, y); ((Control)ctrl).Width = 165; Controls.Add(ctrl); y += 38;
        }
        Row("Destino (CIDR):", _txtDst);
        Row("Gateway:", _txtGw);
        Row("Distância:", _numDist);
        Row("Comentário:", _txtComment);

        var ok = new Button
        {
            Text = "Guardar",
            Location = new Point(140, y + 5),
            Size = new Size(85, 28),
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        ok.FlatAppearance.BorderSize = 0;
        ok.Click += (_, _) => {
            if (string.IsNullOrWhiteSpace(_txtDst.Text) || string.IsNullOrWhiteSpace(_txtGw.Text))
            { MessageBox.Show("Destino e Gateway são obrigatórios."); return; }
            Route = new StaticRoute
            {
                DstAddress = _txtDst.Text.Trim(),
                Gateway = _txtGw.Text.Trim(),
                Distance = _numDist.Value.ToString(),
                Comment = _txtComment.Text
            };
            DialogResult = DialogResult.OK; Close();
        };
        Controls.Add(ok); AcceptButton = ok;
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

        _innerTabs.TabPages.AddRange(new[] { tabServers, tabPools });
        _innerTabs.SelectedIndexChanged += async (_, _) => await UpdateToolbarAndLoad();
        Controls.Add(_innerTabs);

        AddToolbarButton("＋ Novo Servidor", Color.FromArgb(0, 120, 215)).Click += BtnAddServer_Click;
        AddToolbarButton("＋ Nova Pool", Color.FromArgb(0, 140, 80)).Click += BtnAddPool_Click;
        AddToolbarButton("✕ Apagar", Color.FromArgb(180, 30, 30)).Click += async (_, _) => await DeleteAsync();

        _btnEnable = AddToolbarButton("✔ Ativar", Color.FromArgb(0, 140, 80));
        _btnDisable = AddToolbarButton("✕ Desativar", Color.FromArgb(100, 60, 60));

        _btnEnable.Click += async (_, _) => await ToggleAsync(true);
        _btnDisable.Click += async (_, _) => await ToggleAsync(false);

        AddToolbarButton("↺ Atualizar", Color.FromArgb(0, 100, 180)).Click += async (_, _) => await LoadDataAsync();

        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = ".id", HeaderText = "ID", Visible = false });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "name", HeaderText = "Nome", FillWeight = 25 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "interface", HeaderText = "Interface", FillWeight = 25 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "address-pool", HeaderText = "Pool", FillWeight = 20 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "lease-time", HeaderText = "Lease Time", FillWeight = 15 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "disabled", HeaderText = "Disabled", FillWeight = 15 });

        _gridPools.Columns.Add(new DataGridViewTextBoxColumn { Name = ".id", HeaderText = "ID", Visible = false });
        _gridPools.Columns.Add(new DataGridViewTextBoxColumn { Name = "name", HeaderText = "Nome", FillWeight = 30 });
        _gridPools.Columns.Add(new DataGridViewTextBoxColumn { Name = "ranges", HeaderText = "Ranges", FillWeight = 70 });
    }

    private static void StyleGrid(DataGridView g)
    {
        g.ReadOnly = true; g.AllowUserToAddRows = false; g.AllowUserToDeleteRows = false;
        g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        g.BackgroundColor = Color.White; g.RowHeadersVisible = false;
        g.BorderStyle = BorderStyle.None; g.EnableHeadersVisualStyles = false;
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
        _btnEnable.Visible = isServer;
        _btnDisable.Visible = isServer;
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
            else
            {
                var list = await _svc.GetPoolsAsync();
                _gridPools.Rows.Clear();
                foreach (var p in list)
                    _gridPools.Rows.Add(p.Id, p.Name, p.Ranges);
            }
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private async void BtnAddServer_Click(object? s, EventArgs e)
    {
        try
        {
            var pools = await _svc.GetPoolsAsync();
            if (pools.Count == 0 && MessageBox.Show("Não existem Pools.\nDeseja criar uma Pool primeiro?", "Aviso", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                _innerTabs.SelectedIndex = 1;
                return;
            }

            using var frm = new DhcpServerEditForm(pools);
            if (frm.ShowDialog() != DialogResult.OK) return;
            await _svc.CreateServerAsync(frm.Server);
            await LoadDataAsync();
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private async void BtnAddPool_Click(object? s, EventArgs e)
    {
        using var frm = new DhcpPoolEditForm();
        if (frm.ShowDialog() != DialogResult.OK) return;
        try { await _svc.CreatePoolAsync(frm.Pool); await LoadDataAsync(); if (_innerTabs.SelectedIndex != 1) _innerTabs.SelectedIndex = 1; }
        catch (Exception ex) { ShowError(ex); }
    }

    private async Task DeleteAsync()
    {
        if (_innerTabs.SelectedIndex == 0)
        {
            var id = SelectedId();
            if (id == null) { MessageBox.Show("Seleciona um servidor."); return; }
            if (MessageBox.Show("Apagar servidor DHCP?", "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                try { await _svc.DeleteServerAsync(id); await LoadDataAsync(); } catch (Exception ex) { ShowError(ex); }
            }
        }
        else
        {
            var id = SelectedId();
            if (id == null) { MessageBox.Show("Seleciona uma Pool."); return; }

            if (MessageBox.Show("Apagar esta Pool?\nSe estiver em uso, dará erro.", "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                try { await _svc.DeletePoolAsync(id); await LoadDataAsync(); } catch (Exception ex) { ShowError(ex); }
            }
        }
    }

    private async Task ToggleAsync(bool enable)
    {
        if (_innerTabs.SelectedIndex != 0) return;
        var id = SelectedId(); if (id == null) { MessageBox.Show("Seleciona um servidor."); return; }
        try
        {
            if (enable) await _svc.EnableServerAsync(id); else await _svc.DisableServerAsync(id);
            await LoadDataAsync();
        }
        catch (Exception ex) { ShowError(ex); }
    }
}

public class DhcpServerEditForm : Form
{
    public DhcpServer Server { get; private set; } = new();
    private readonly TextBox _txtName = new();
    private readonly TextBox _txtInterface = new();
    private readonly ComboBox _cboPool = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _txtLease = new();

    public DhcpServerEditForm(List<DhcpPool> pools)
    {
        Text = "Novo Servidor DHCP";
        Size = new Size(340, 230);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9.5f);
        _txtLease.Text = "10m";

        foreach (var p in pools) _cboPool.Items.Add(p.Name);
        if (_cboPool.Items.Count > 0) _cboPool.SelectedIndex = 0;

        int y = 18;
        void Row(string lbl, Control ctrl)
        {
            Controls.Add(new Label { Text = lbl, Location = new Point(14, y + 3), AutoSize = true });
            ctrl.Location = new Point(120, y);
            ctrl.Width = 185;
            Controls.Add(ctrl);
            y += 38;
        }

        Row("Nome:", _txtName);
        Row("Interface:", _txtInterface);
        Row("Pool:", _cboPool);
        Row("Lease Time:", _txtLease);

        var ok = new Button
        {
            Text = "Guardar",
            Location = new Point(120, y + 5),
            Size = new Size(85, 28),
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        ok.FlatAppearance.BorderSize = 0;

        ok.Click += (_, _) => {
            if (string.IsNullOrWhiteSpace(_txtName.Text) || string.IsNullOrWhiteSpace(_txtInterface.Text) || string.IsNullOrWhiteSpace(_cboPool.Text))
            { MessageBox.Show("Nome, Interface e Pool são obrigatórios."); return; }

            Server = new DhcpServer
            {
                Name = _txtName.Text.Trim(),
                Interface = _txtInterface.Text.Trim(),
                AddressPool = _cboPool.Text,
                LeaseTime = _txtLease.Text.Trim()
            };
            DialogResult = DialogResult.OK;
            Close();
        };
        Controls.Add(ok);
        AcceptButton = ok;
    }
}

public class DhcpPoolEditForm : Form
{
    public DhcpPool Pool { get; private set; } = new();
    private readonly TextBox _txtName = new();
    private readonly TextBox _txtRanges = new();

    public DhcpPoolEditForm()
    {
        Text = "Nova Pool de IPs"; Size = new Size(340, 200);
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;
        StartPosition = FormStartPosition.CenterParent; Font = new Font("Segoe UI", 9.5f);

        int y = 18;
        void Row(string lbl, TextBox tb)
        {
            Controls.Add(new Label { Text = lbl, Location = new Point(14, y + 3), AutoSize = true });
            tb.Location = new Point(120, y); tb.Width = 185; Controls.Add(tb); y += 38;
        }
        Row("Nome:", _txtName);
        Row("Ranges:", _txtRanges);

        Controls.Add(new Label
        {
            Text = "Ex: 192.168.88.10-192.168.88.254",
            Location = new Point(120, y - 5),
            AutoSize = true,
            ForeColor = Color.Gray,
            Font = new Font("Segoe UI", 8f)
        });

        var ok = new Button { Text = "Guardar", Location = new Point(120, y + 20), Size = new Size(85, 28), BackColor = Color.FromArgb(0, 120, 215), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        ok.FlatAppearance.BorderSize = 0;
        ok.Click += (_, _) => {
            if (string.IsNullOrWhiteSpace(_txtName.Text) || string.IsNullOrWhiteSpace(_txtRanges.Text)) { MessageBox.Show("Obrigatório."); return; }
            Pool = new DhcpPool { Name = _txtName.Text.Trim(), Ranges = _txtRanges.Text.Trim() };
            DialogResult = DialogResult.OK; Close();
        };
        Controls.Add(ok); AcceptButton = ok;
    }
}

// ════════════════════════════════════════════════════════════════════
//  POOLS PANEL
// ════════════════════════════════════════════════════════════════════

public class PoolsPanel : BasePanel
{
    private readonly DhcpService _svc;

    public PoolsPanel(MikroTikClient client) : base(client)
    {
        _svc = new DhcpService(client);
        AddToolbarButton("＋ Nova Pool", Color.FromArgb(0, 120, 215)).Click += BtnAdd_Click;
        AddToolbarButton("✕ Apagar", Color.FromArgb(180, 30, 30)).Click += async (_, _) => await DeleteAsync();
        AddToolbarButton("↺ Atualizar", Color.FromArgb(0, 100, 180)).Click += async (_, _) => await LoadDataAsync();

        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = ".id", HeaderText = "ID", Visible = false });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "name", HeaderText = "Nome", FillWeight = 30 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ranges", HeaderText = "Ranges", FillWeight = 70 });
    }

    protected override async Task LoadDataAsync()
    {
        try
        {
            var list = await _svc.GetPoolsAsync();
            Grid.Rows.Clear();
            foreach (var p in list)
                Grid.Rows.Add(p.Id, p.Name, p.Ranges);
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private async void BtnAdd_Click(object? s, EventArgs e)
    {
        using var frm = new DhcpPoolEditForm();
        if (frm.ShowDialog() != DialogResult.OK) return;
        try { await _svc.CreatePoolAsync(frm.Pool); await LoadDataAsync(); }
        catch (Exception ex) { ShowError(ex); }
    }

    private async Task DeleteAsync()
    {
        var id = SelectedId();
        if (id == null) { MessageBox.Show("Seleciona uma Pool."); return; }

        if (MessageBox.Show("Apagar esta Pool?\nSe estiver em uso, dará erro.", "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

        try { await _svc.DeletePoolAsync(id); await LoadDataAsync(); }
        catch (Exception ex) { ShowError(ex); }
    }
}

// ════════════════════════════════════════════════════════════════════
//  DNS PANEL (COM DNS ESTÁTICO INCLUÍDO)
// ════════════════════════════════════════════════════════════════════

public class DnsPanel : BasePanel
{
    private readonly DnsService _svc;
    private readonly TextBox _txtServers = new();
    private readonly CheckBox _chkAllow = new() { Text = "Permitir pedidos remotos", AutoSize = true };
    private readonly DataGridView _gridStatic = new();

    public DnsPanel(MikroTikClient client) : base(client)
    {
        _svc = new DnsService(client);
        Controls.Remove(Grid);

        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 160 };

        var pnlTop = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20) };
        var lblServers = new Label { Text = "Servidores Upstream DNS (ex: 8.8.8.8, 1.1.1.1):", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        _txtServers.Location = new Point(20, 38); _txtServers.Width = 350;
        _chkAllow.Location = new Point(20, 70);
        var btnSave = new Button { Text = "💾 Guardar Definições", Location = new Point(20, 105), Width = 160, Height = 32, BackColor = Color.DodgerBlue, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        btnSave.Click += async (_, _) => { try { await _svc.UpdateSettingsAsync(_txtServers.Text, _chkAllow.Checked); MessageBox.Show("Configuração guardada!"); } catch (Exception ex) { ShowError(ex); } };
        var btnFlush = new Button { Text = "🗑 Limpar Cache", Location = new Point(190, 105), Width = 150, Height = 32, BackColor = Color.Chocolate, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        btnFlush.Click += async (_, _) => { try { await _svc.FlushCacheAsync(); MessageBox.Show("Cache limpa!"); } catch (Exception ex) { ShowError(ex); } };
        pnlTop.Controls.AddRange(new Control[] { lblServers, _txtServers, _chkAllow, btnSave, btnFlush });

        var pnlBottom = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
        var lblStatic = new Label { Text = "Entradas DNS Estático (Domínios Locais):", Dock = DockStyle.Top, Height = 30, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        var barStatic = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40 };
        var btnAddS = new Button { Text = "＋ Novo Domínio", Width = 130, Height = 28, BackColor = Color.SeaGreen, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        btnAddS.Click += async (_, _) => { using var f = new DnsStaticEditForm(); if (f.ShowDialog() == DialogResult.OK) { try { await _svc.AddStaticEntryAsync(f.Entry); await LoadDataAsync(); } catch (Exception ex) { ShowError(ex); } } };
        var btnDelS = new Button { Text = "✕ Remover Selecionado", Width = 160, Height = 28, BackColor = Color.Firebrick, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        btnDelS.Click += async (_, _) => { if (_gridStatic.SelectedRows.Count == 0) return; var id = _gridStatic.SelectedRows[0].Cells[".id"].Value?.ToString(); if (id != null) { await _svc.DeleteStaticEntryAsync(id); await LoadDataAsync(); } };
        barStatic.Controls.AddRange(new[] { btnAddS, btnDelS });

        _gridStatic.Dock = DockStyle.Fill; _gridStatic.ReadOnly = true; _gridStatic.AllowUserToAddRows = false; _gridStatic.RowHeadersVisible = false; _gridStatic.SelectionMode = DataGridViewSelectionMode.FullRowSelect; _gridStatic.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; _gridStatic.BackgroundColor = Color.White;
        _gridStatic.Columns.AddRange(new DataGridViewColumn[] { new DataGridViewTextBoxColumn { Name = ".id", Visible = false }, new DataGridViewTextBoxColumn { Name = "name", HeaderText = "Nome de Domínio" }, new DataGridViewTextBoxColumn { Name = "address", HeaderText = "Endereço IP" }, new DataGridViewTextBoxColumn { Name = "comment", HeaderText = "Comentário" } });

        pnlBottom.Controls.Add(_gridStatic); pnlBottom.Controls.Add(barStatic); pnlBottom.Controls.Add(lblStatic);

        split.Panel1.Controls.Add(pnlTop); split.Panel2.Controls.Add(pnlBottom);
        Controls.Add(split);
    }

    protected override async Task LoadDataAsync()
    {
        try
        {
            var s = await _svc.GetSettingsAsync(); if (s != null) { _txtServers.Text = s.Servers; _chkAllow.Checked = s.AllowRemoteRequests; }
            var list = await _svc.GetStaticEntriesAsync(); _gridStatic.Rows.Clear();
            foreach (var e in list) _gridStatic.Rows.Add(e.Id, e.Name, e.Address, e.Comment);
        }
        catch (Exception ex) { ShowError(ex); }
    }
}

public class DnsStaticEditForm : Form
{
    public DnsStaticEntry Entry { get; private set; } = new();
    private readonly TextBox _tNom = new(), _tIp = new(), _tCom = new();
    public DnsStaticEditForm()
    {
        Text = "DNS Estático"; Size = new Size(360, 240); StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.FixedDialog; Font = new Font("Segoe UI", 9f);
        void R(string l, Control c, int y) { Controls.Add(new Label { Text = l, Location = new Point(20, y + 3), AutoSize = true }); c.Location = new Point(130, y); c.Width = 180; Controls.Add(c); }
        R("Domínio:", _tNom, 20); R("IP destino:", _tIp, 60); R("Comentário:", _tCom, 100);
        var b = new Button { Text = "Adicionar", Location = new Point(130, 150), Width = 180, Height = 30, BackColor = Color.DodgerBlue, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.OK };
        b.Click += (s, e) => Entry = new DnsStaticEntry { Name = _tNom.Text, Address = _tIp.Text, Comment = _tCom.Text };
        Controls.Add(b); AcceptButton = b;
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
                try { await Client.RebootAsync(); MessageBox.Show("Reboot iniciado com sucesso."); }
                catch (Exception ex) { ShowError(ex); }
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
        catch (Exception ex) { ShowError(ex); }
    }
}

// ════════════════════════════════════════════════════════════════════
//  WIREGUARD PANEL - ATUALIZADO FINAL
// ════════════════════════════════════════════════════════════════════

public class WireGuardPanel : BasePanel
{
    private readonly WireGuardService _svc;
    private readonly TabControl _innerTabs = new();
    private readonly DataGridView _gridPeers = new();
    private List<WireGuardInterface> _interfaces = new();

    private readonly Button _btnQuickSetup;
    private readonly Button _btnAddIf;
    private readonly Button _btnAddPeer;
    private readonly Button _btnExportConfig;

    public WireGuardPanel(MikroTikClient client) : base(client)
    {
        _svc = new WireGuardService(client);
        Controls.Remove(Grid);

        _innerTabs.Dock = DockStyle.Fill;
        var tabIf = new TabPage("Interfaces WireGuard"); Grid.Dock = DockStyle.Fill; tabIf.Controls.Add(Grid);
        var tabPeers = new TabPage("Peers"); StyleGrid(_gridPeers); _gridPeers.Dock = DockStyle.Fill; tabPeers.Controls.Add(_gridPeers);
        _innerTabs.TabPages.AddRange(new[] { tabIf, tabPeers });

        _innerTabs.SelectedIndexChanged += async (_, _) => {
            bool isIfTab = _innerTabs.SelectedIndex == 0;
            _btnQuickSetup.Visible = isIfTab;
            _btnAddIf.Visible = isIfTab;
            _btnAddPeer.Visible = !isIfTab;
            _btnExportConfig.Visible = !isIfTab;
            await LoadDataAsync();
        };
        Controls.Add(_innerTabs);
        _innerTabs.BringToFront();

        _btnQuickSetup = AddToolbarButton("⚡ Configuração Rápida (SDN)", Color.DarkOrange);
        _btnQuickSetup.Click += async (_, _) => {
            using var frm = new VpnQuickSetupForm();
            if (frm.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    await _svc.SetupFullVpnAsync(frm.InterfaceName, frm.ListenPort, frm.Mtu, frm.NetworkIp, frm.PeerPublicKey, frm.PeerAllowedIp);

                    var interfaces = await _svc.GetInterfacesAsync();
                    var serverIf = interfaces.FirstOrDefault(i => i.Name == frm.InterfaceName);
                    string serverPubKey = serverIf?.PublicKey ?? "ERRO_AO_OBTER_CHAVE";

                    if (!string.IsNullOrWhiteSpace(frm.ClientPrivateKey))
                    {
                        string configData = WireGuardService.GenerateClientConfig(
                            new WireGuardPeer(), serverPubKey, Client.Host, frm.ListenPort,
                            frm.ClientPrivateKey, frm.PeerAllowedIp, "8.8.8.8"
                        );

                        using var successFrm = new VpnSuccessForm(configData);
                        successFrm.ShowDialog();
                    }
                    else
                    {
                        MessageBox.Show("VPN Configurada com sucesso! (Sem geração de .conf porque inseriu chaves externas manualmente)", "Configuração Concluída");
                    }

                    await LoadDataAsync();
                }
                catch (Exception ex) { ShowError(ex); }
            }
        };

        _btnAddIf = AddToolbarButton("＋ Nova Interface", Color.FromArgb(0, 120, 215));
        _btnAddIf.Click += async (_, _) => {
            using var frm = new WgInterfaceEditForm();
            if (frm.ShowDialog() == DialogResult.OK) { try { await _svc.CreateInterfaceAsync(frm.WgInterface); await LoadDataAsync(); } catch (Exception ex) { ShowError(ex); } }
        };

        _btnAddPeer = AddToolbarButton("＋ Novo Peer", Color.FromArgb(0, 140, 80));
        _btnAddPeer.Visible = false;
        _btnAddPeer.Click += async (_, _) => {
            var interfacesParaPeer = await _svc.GetInterfacesAsync();
            if (interfacesParaPeer.Count == 0)
            {
                MessageBox.Show("Não existem interfaces WireGuard. Crie uma interface primeiro.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var frm = new WgPeerEditForm(interfacesParaPeer.Select(i => i.Name).ToList());
            if (frm.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    await _svc.AddPeerAsync(frm.Peer);

                    if (!string.IsNullOrWhiteSpace(frm.GeneratedPrivateKey))
                    {
                        var serverIf = interfacesParaPeer.FirstOrDefault(i => i.Name == frm.Peer.Interface);
                        string serverPubKey = serverIf?.PublicKey ?? "ERRO";
                        string configData = WireGuardService.GenerateClientConfig(
                            frm.Peer, serverPubKey, Client.Host, serverIf?.ListenPort ?? "51820",
                            frm.GeneratedPrivateKey, frm.Peer.AllowedAddress, "8.8.8.8"
                        );

                        using var successFrm = new VpnSuccessForm(configData);
                        successFrm.ShowDialog();
                    }

                    await LoadDataAsync();
                }
                catch (Exception ex) { ShowError(ex); }
            }
        };

        AddToolbarButton("✕ Apagar", Color.FromArgb(180, 30, 30)).Click += async (_, _) => {
            if (_innerTabs.SelectedIndex == 0)
            {
                var id = SelectedId();
                if (id == null) { MessageBox.Show("Seleciona uma interface WireGuard."); return; }
                if (MessageBox.Show("Aviso: Isto vai apagar a Interface e TODOS os Peers ligados a ela.\nDeseja continuar?", "Apagar Interface", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                try { await _svc.DeleteInterfaceAsync(id); await LoadDataAsync(); } catch (Exception ex) { ShowError(ex); }
            }
            else
            {
                if (_gridPeers.SelectedRows.Count == 0) { MessageBox.Show("Seleciona um peer."); return; }
                var id = _gridPeers.SelectedRows[0].Cells[".id"].Value?.ToString();
                if (id == null) return;
                if (MessageBox.Show("Apagar este peer?", "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                try { await _svc.DeletePeerAsync(id); await LoadDataAsync(); } catch (Exception ex) { ShowError(ex); }
            }
        };

        _btnExportConfig = AddToolbarButton("📄 Ver QR / Config", Color.FromArgb(80, 80, 140));
        _btnExportConfig.Visible = false;
        _btnExportConfig.Click += async (_, _) => {
            if (_gridPeers.SelectedRows.Count == 0) { MessageBox.Show("Selecione um Peer na lista."); return; }

            var ifaceName = _gridPeers.SelectedRows[0].Cells["interface"].Value?.ToString();
            var allowedIp = _gridPeers.SelectedRows[0].Cells["allowed-address"].Value?.ToString();

            var interfacesParaPeer = await _svc.GetInterfacesAsync();
            using var frm = new WgClientConfigForm(interfacesParaPeer, ifaceName, allowedIp);
            frm.ShowDialog();
        };

        AddToolbarButton("↺ Atualizar", Color.FromArgb(0, 100, 180)).Click += async (_, _) => await LoadDataAsync();

        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = ".id", HeaderText = "ID", Visible = false });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "name", HeaderText = "Nome", FillWeight = 20 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "listen-port", HeaderText = "Porta", FillWeight = 15 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "public-key", HeaderText = "Chave Pública", FillWeight = 50 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "running", HeaderText = "Ativo", FillWeight = 10 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "disabled", HeaderText = "Disabled", FillWeight = 10 });

        _gridPeers.Columns.Add(new DataGridViewTextBoxColumn { Name = ".id", HeaderText = "ID", Visible = false });
        _gridPeers.Columns.Add(new DataGridViewTextBoxColumn { Name = "interface", HeaderText = "Interface", FillWeight = 15 });
        _gridPeers.Columns.Add(new DataGridViewTextBoxColumn { Name = "public-key", HeaderText = "Chave Pública", FillWeight = 35 });
        _gridPeers.Columns.Add(new DataGridViewTextBoxColumn { Name = "allowed-address", HeaderText = "Endereços Permitidos", FillWeight = 25 });
        _gridPeers.Columns.Add(new DataGridViewTextBoxColumn { Name = "comment", HeaderText = "Nome / Comentário", FillWeight = 25 });
    }

    private static void StyleGrid(DataGridView g)
    {
        g.ReadOnly = true; g.AllowUserToAddRows = false; g.AllowUserToDeleteRows = false; g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; g.BackgroundColor = Color.White; g.RowHeadersVisible = false; g.BorderStyle = BorderStyle.None; g.EnableHeadersVisualStyles = false;
        g.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(40, 40, 40), ForeColor = Color.White, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
    }

    protected override async Task LoadDataAsync()
    {
        try
        {
            if (_innerTabs.SelectedIndex == 0)
            {
                _interfaces = await _svc.GetInterfacesAsync();
                Grid.Rows.Clear();
                foreach (var i in _interfaces) Grid.Rows.Add(i.Id, i.Name, i.ListenPort, i.PublicKey, i.Running ? "✔" : "✘", i.Disabled ? "Sim" : "Não");
            }
            else
            {
                var peers = await _svc.GetPeersAsync();
                _gridPeers.Rows.Clear();
                foreach (var p in peers) _gridPeers.Rows.Add(p.Id, p.Interface, p.PublicKey, p.AllowedAddress, p.Comment);
            }
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private async void BtnAddIf_Click(object? s, EventArgs e)
    {
        using var frm = new WgInterfaceEditForm();
        if (frm.ShowDialog() != DialogResult.OK) return;
        try { await _svc.CreateInterfaceAsync(frm.WgInterface); await LoadDataAsync(); }
        catch (Exception ex) { ShowError(ex); }
    }
}

// ── Formulários Auxiliares Edit/Setup ────────────────────────────────

public class VpnQuickSetupForm : Form
{
    public string InterfaceName => _txtName.Text.Trim();
    public string ListenPort => _txtPort.Text.Trim();
    public string Mtu => _txtMtu.Text.Trim();
    public string NetworkIp => _txtNetwork.Text.Trim();
    public string PeerPublicKey => _txtPeerPubKey.Text.Trim();
    public string PeerAllowedIp => _txtPeerIp.Text.Trim();
    public string ClientPrivateKey { get; private set; } = "";

    private readonly TextBox _txtName = new();
    private readonly TextBox _txtPort = new();
    private readonly TextBox _txtMtu = new();
    private readonly TextBox _txtNetwork = new();
    private readonly TextBox _txtPeerPubKey = new();
    private readonly TextBox _txtPeerIp = new();

    public VpnQuickSetupForm()
    {
        Text = "Configuração Rápida WireGuard SDN";
        Size = new Size(420, 360);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; StartPosition = FormStartPosition.CenterParent; Font = new Font("Segoe UI", 9.5f);

        int y = 18;
        void Row(string lbl, Control ctrl)
        {
            Controls.Add(new Label { Text = lbl, Location = new Point(14, y + 3), AutoSize = true });
            ctrl.Location = new Point(160, y); ((Control)ctrl).Width = 220; Controls.Add(ctrl); y += 35;
        }

        Row("Nome da Interface:", _txtName);
        Row("Listen Port:", _txtPort);
        Row("MTU:", _txtMtu);
        Row("Rede VPN (CIDR):", _txtNetwork);
        Row("IP do Peer:", _txtPeerIp);
        Row("Pub. Key do Peer:", _txtPeerPubKey);

        var btnAuto = new Button { Text = "🔑 Gerar Par de Chaves", Location = new Point(14, y + 10), Size = new Size(366, 30), BackColor = Color.FromArgb(40, 160, 100), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        btnAuto.FlatAppearance.BorderSize = 0;
        btnAuto.Click += (_, _) => {
            var keys = WireGuardKeyGen.Generate();
            ClientPrivateKey = keys.PrivateKey;
            _txtPeerPubKey.Text = keys.PublicKey;
            MessageBox.Show("Chaves geradas matematicamente!\n\nA Chave Pública do Peer foi preenchida acima. A Chave Privada do Cliente será usada internamente para gerar o QR Code se avançar.", "Sucesso", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
        Controls.Add(btnAuto);

        y += 50;
        var btnOk = new Button { Text = "Executar Setup", Location = new Point(14, y), Size = new Size(366, 30), BackColor = Color.FromArgb(0, 120, 215), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        btnOk.FlatAppearance.BorderSize = 0;
        btnOk.Click += (_, _) => {
            if (string.IsNullOrWhiteSpace(_txtName.Text) || string.IsNullOrWhiteSpace(_txtPeerPubKey.Text)) { MessageBox.Show("Preencha os campos obrigatórios (ou utilize o gerador de chaves)."); return; }
            DialogResult = DialogResult.OK; Close();
        };
        Controls.Add(btnOk); AcceptButton = btnOk;
    }
}

public class VpnSuccessForm : Form
{
    public VpnSuccessForm(string configData)
    {
        Text = "VPN Pronta! Configuração do Cliente";
        Size = new Size(540, 480);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9.5f);

        var lbl = new Label { Text = "O seu servidor está pronto. Importe a configuração abaixo no seu cliente:", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 10f, FontStyle.Bold) };
        Controls.Add(lbl);

        var txtOutput = new TextBox { Location = new Point(20, 45), Size = new Size(240, 320), Multiline = true, ReadOnly = true, Font = new Font("Consolas", 9f), ScrollBars = ScrollBars.Vertical, Text = configData };
        Controls.Add(txtOutput);

        var pbQr = new PictureBox { Location = new Point(270, 45), Size = new Size(230, 230), SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.FixedSingle };
        Controls.Add(pbQr);

        System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12;
        Task.Run(async () => {
            try
            {
                using var http = new System.Net.Http.HttpClient();
                string url = $"https://api.qrserver.com/v1/create-qr-code/?size=250x250&data={Uri.EscapeDataString(configData)}";
                var imageBytes = await http.GetByteArrayAsync(url);
                pbQr.Invoke(new Action(() => {
                    using var ms = new System.IO.MemoryStream(imageBytes);
                    pbQr.Image = new Bitmap(ms);
                }));
            }
            catch { }
        });

        var btnSave = new Button { Text = "💾 Guardar Ficheiro .conf", Location = new Point(20, 380), Size = new Size(200, 35), BackColor = Color.FromArgb(0, 140, 60), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        btnSave.FlatAppearance.BorderSize = 0;
        btnSave.Click += (_, _) => {
            using var dlg = new SaveFileDialog { Filter = "WireGuard Config|*.conf", FileName = "wg-client.conf" };
            if (dlg.ShowDialog() == DialogResult.OK) File.WriteAllText(dlg.FileName, configData);
        };
        Controls.Add(btnSave);

        var btnClose = new Button { Text = "Fechar", Location = new Point(400, 380), Size = new Size(100, 35), FlatStyle = FlatStyle.Flat };
        btnClose.Click += (_, _) => Close();
        Controls.Add(btnClose);
    }
}

public class WgInterfaceEditForm : Form
{
    public WireGuardInterface WgInterface { get; private set; } = new();
    private readonly TextBox _txtName = new(), _txtPort = new(), _txtComment = new();

    public WgInterfaceEditForm()
    {
        Text = "Nova Interface WireGuard"; Size = new Size(340, 210);
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;
        StartPosition = FormStartPosition.CenterParent; Font = new Font("Segoe UI", 9.5f);
        _txtPort.Text = "13231";

        int y = 18;
        void Row(string lbl, TextBox tb)
        {
            Controls.Add(new Label { Text = lbl, Location = new Point(14, y + 3), AutoSize = true });
            tb.Location = new Point(120, y); tb.Width = 185; Controls.Add(tb); y += 38;
        }
        Row("Nome:", _txtName); Row("Porta:", _txtPort); Row("Comentário:", _txtComment);

        Controls.Add(new Label { Text = "💡 A chave privada é gerada automaticamente pelo RouterOS.", Location = new Point(14, y), AutoSize = true, ForeColor = Color.Gray, Font = new Font("Segoe UI", 8f) });

        var ok = new Button { Text = "Criar", Location = new Point(120, y + 22), Size = new Size(85, 28), BackColor = Color.FromArgb(0, 120, 215), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        ok.FlatAppearance.BorderSize = 0;
        ok.Click += (_, _) => {
            if (string.IsNullOrWhiteSpace(_txtName.Text)) { MessageBox.Show("Nome obrigatório."); return; }
            WgInterface = new WireGuardInterface { Name = _txtName.Text.Trim(), ListenPort = _txtPort.Text.Trim(), Comment = _txtComment.Text };
            DialogResult = DialogResult.OK; Close();
        };
        Controls.Add(ok); AcceptButton = ok;
    }
}

public class WgPeerEditForm : Form
{
    public WireGuardPeer Peer { get; private set; } = new();
    public string GeneratedPrivateKey { get; private set; } = "";

    private readonly ComboBox _cboInterface = new();
    private readonly TextBox _txtPublicKey = new(), _txtAllowedAddr = new(), _txtName = new();

    public WgPeerEditForm(List<string> interfaces)
    {
        Text = "Novo Peer (Cliente)"; Size = new Size(420, 320); StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; Font = new Font("Segoe UI", 9.5f);

        _cboInterface.DropDownStyle = ComboBoxStyle.DropDownList;
        interfaces.ForEach(i => _cboInterface.Items.Add(i));
        if (_cboInterface.Items.Count > 0) _cboInterface.SelectedIndex = 0;

        int y = 20;
        void Row(string lbl, Control ctrl)
        {
            Controls.Add(new Label { Text = lbl, Location = new Point(20, y + 3), AutoSize = true });
            ctrl.Location = new Point(180, y); ctrl.Width = 200; Controls.Add(ctrl); y += 40;
        }

        Row("Nome (Opcional):", _txtName);
        Row("Chave Pública do Cliente:", _txtPublicKey);

        var btnGen = new Button { Text = "🔑 Gerar", Location = new Point(180, y), Width = 200, BackColor = Color.FromArgb(40, 160, 100), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        btnGen.FlatAppearance.BorderSize = 0;
        btnGen.Click += (s, e) => {
            var keys = WireGuardKeyGen.Generate();
            GeneratedPrivateKey = keys.PrivateKey;
            _txtPublicKey.Text = keys.PublicKey;
            MessageBox.Show($"Chaves geradas internamente.\n\nO ficheiro e QR Code serão disponibilizados ao clicar em Adicionar.", "Sucesso", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
        Controls.Add(btnGen);
        y += 40;

        Row("Allowed Address (IP/32):", _txtAllowedAddr);
        Row("Interface do Servidor:", _cboInterface);

        var btnOk = new Button { Text = "Adicionar Peer", Location = new Point(180, y + 10), Width = 200, BackColor = Color.FromArgb(0, 120, 215), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        btnOk.FlatAppearance.BorderSize = 0;
        btnOk.Click += (s, e) => {
            if (string.IsNullOrWhiteSpace(_txtPublicKey.Text) || string.IsNullOrWhiteSpace(_txtAllowedAddr.Text)) return;
            Peer = new WireGuardPeer
            {
                Interface = _cboInterface.Text,
                PublicKey = _txtPublicKey.Text,
                AllowedAddress = _txtAllowedAddr.Text,
                Comment = _txtName.Text
            };
            DialogResult = DialogResult.OK; Close();
        };
        Controls.Add(btnOk); AcceptButton = btnOk;
    }
}

public class WgClientConfigForm : Form
{
    public WgClientConfigForm(List<WireGuardInterface> interfaces, string? prefillInterface = null, string? prefillIp = null)
    {
        Text = "Exportar Configuração para Peer Existente"; Size = new Size(560, 360);
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; StartPosition = FormStartPosition.CenterParent; Font = new Font("Segoe UI", 9.5f);

        var cboIf = new ComboBox { Location = new Point(220, 14), Width = 300, DropDownStyle = ComboBoxStyle.DropDownList };
        interfaces.ForEach(i => cboIf.Items.Add(i.Name));

        if (prefillInterface != null && cboIf.Items.Contains(prefillInterface))
            cboIf.SelectedItem = prefillInterface;
        else if (cboIf.Items.Count > 0) cboIf.SelectedIndex = 0;

        var txtClientPrivKey = new TextBox { Location = new Point(220, 52), Width = 300 };
        var txtClientAddr = new TextBox { Location = new Point(220, 90), Width = 300 };
        var txtEndpoint = new TextBox { Location = new Point(220, 128), Width = 300 };
        var txtDns = new TextBox { Location = new Point(220, 166), Width = 300, Text = "8.8.8.8" };

        if (prefillIp != null) txtClientAddr.Text = prefillIp;

        int y = 14;
        void Lbl(string t, int ly) => Controls.Add(new Label { Text = t, Location = new Point(20, ly + 3), AutoSize = true });
        Lbl("Interface associada:", y); Controls.Add(cboIf);
        y += 38; Lbl("Cole a Chave Privada do Cliente:", y); Controls.Add(txtClientPrivKey);
        y += 38; Lbl("Endereço (Allowed IP):", y); Controls.Add(txtClientAddr);
        y += 38; Lbl("Endpoint do Servidor (IP Público):", y); Controls.Add(txtEndpoint);
        y += 38; Lbl("DNS do Cliente:", y); Controls.Add(txtDns);

        var lblAviso = new Label { Text = "⚠️ O MikroTik não guarda a Chave Privada. Se a perdeu, gere um Peer novo.", Location = new Point(20, y + 45), AutoSize = true, ForeColor = Color.Gray, MaximumSize = new Size(500, 0) };
        Controls.Add(lblAviso);

        var btnGen = new Button { Text = "Gerar QR / Ficheiro .conf", Location = new Point(220, y + 95), Size = new Size(300, 32), BackColor = Color.FromArgb(0, 120, 215), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        btnGen.FlatAppearance.BorderSize = 0;
        btnGen.Click += (_, _) => {
            if (string.IsNullOrWhiteSpace(txtClientPrivKey.Text) || string.IsNullOrWhiteSpace(txtEndpoint.Text))
            { MessageBox.Show("Preencha a Chave Privada e o Endpoint do Servidor!"); return; }

            var iface = interfaces.FirstOrDefault(i => i.Name == cboIf.Text);
            if (iface == null) return;

            string cfg = WireGuardService.GenerateClientConfig(new WireGuardPeer(), iface.PublicKey, txtEndpoint.Text, iface.ListenPort, txtClientPrivKey.Text, txtClientAddr.Text, txtDns.Text);

            using var successFrm = new VpnSuccessForm(cfg);
            successFrm.ShowDialog();
            Close();
        };
        Controls.Add(btnGen);
    }
}