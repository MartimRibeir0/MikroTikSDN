using MikroTikSDN.Api;
using MikroTikSDN.Models;
using MikroTikSDN.Services;

namespace MikroTikSDN.Forms;

// ════════════════════════════════════════════════════════════════════
//  BASE PANEL — todos os painéis herdam deste
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
    private readonly Button _btnRefresh;
    private readonly Button _btnEnable;
    private readonly Button _btnDisable;

    public InterfacesPanel(MikroTikClient client) : base(client)
    {
        _svc = new InterfaceService(client);

        Toolbar.Controls.Add(_chkWirelessOnly);
        _btnEnable  = AddToolbarButton("✔ Ativar",   Color.FromArgb(0, 140, 80));
        _btnDisable = AddToolbarButton("✕ Desativar", Color.FromArgb(160, 40, 40));
        _btnRefresh = AddToolbarButton("↺ Atualizar", Color.FromArgb(0, 100, 180));

        _chkWirelessOnly.CheckedChanged += (_, _) => ApplyFilter();
        _btnRefresh.Click += async (_, _) => await LoadDataAsync();
        _btnEnable.Click  += async (_, _) => await ToggleAsync(enable: true);
        _btnDisable.Click += async (_, _) => await ToggleAsync(enable: false);

        // Colunas
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = ".id",        HeaderText = "ID",         Visible = false });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "name",       HeaderText = "Nome",       FillWeight = 25 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "type",       HeaderText = "Tipo",       FillWeight = 20 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "mac-address",HeaderText = "MAC",        FillWeight = 25 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "mtu",        HeaderText = "MTU",        FillWeight = 10 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "running",    HeaderText = "A correr",   FillWeight = 10 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "disabled",   HeaderText = "Desativada", FillWeight = 10 });
    }

    protected override async Task LoadDataAsync()
    {
        try
        {
            _all = await _svc.GetAllAsync();
            ApplyFilter();
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void ApplyFilter()
    {
        Grid.Rows.Clear();
        var list = _chkWirelessOnly.Checked
            ? _all.Where(i => i.Type.Contains("wireless", StringComparison.OrdinalIgnoreCase)).ToList()
            : _all;

        foreach (var i in list)
            Grid.Rows.Add(i.Id, i.Name, i.Type, i.MacAddress, i.Mtu,
                i.Running ? "✔" : "✘", i.Disabled ? "Sim" : "Não");
    }

    private async Task ToggleAsync(bool enable)
    {
        var id = SelectedId();
        if (id == null) { MessageBox.Show("Seleciona uma interface."); return; }
        try
        {
            if (enable) await _svc.EnableAsync(id);
            else        await _svc.DisableAsync(id);
            await LoadDataAsync();
        }
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

        // Substituir o Grid simples por innerTabs (interfaces + perfis)
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

        // Toolbar
        AddToolbarButton("✔ Ativar",   Color.FromArgb(0,140,80)).Click  += async (_, _) => await ToggleIfAsync(true);
        AddToolbarButton("✕ Desativar",Color.FromArgb(160,40,40)).Click += async (_, _) => await ToggleIfAsync(false);
        AddToolbarButton("↺ Atualizar",Color.FromArgb(0,100,180)).Click += async (_, _) => await LoadDataAsync();

        // Colunas interfaces
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name=".id",      HeaderText="ID",        Visible=false });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name="name",     HeaderText="Nome",      FillWeight=15 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name="ssid",     HeaderText="SSID",      FillWeight=20 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name="band",     HeaderText="Banda",     FillWeight=15 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name="freq",     HeaderText="Freq.",     FillWeight=10 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name="sec",      HeaderText="Segurança", FillWeight=20 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name="running",  HeaderText="Ativo",     FillWeight=10 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name="disabled", HeaderText="Disabled",  FillWeight=10 });

        // Colunas perfis
        _gridProfiles.Columns.Add(new DataGridViewTextBoxColumn { Name=".id",   HeaderText="ID",   Visible=false });
        _gridProfiles.Columns.Add(new DataGridViewTextBoxColumn { Name="name",  HeaderText="Nome", FillWeight=25 });
        _gridProfiles.Columns.Add(new DataGridViewTextBoxColumn { Name="mode",  HeaderText="Modo", FillWeight=25 });
        _gridProfiles.Columns.Add(new DataGridViewTextBoxColumn { Name="auth",  HeaderText="Auth", FillWeight=25 });
    }

    private static void StyleGrid(DataGridView g)
    {
        g.ReadOnly = true; g.AllowUserToAddRows = false; g.AllowUserToDeleteRows = false;
        g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        g.BackgroundColor = Color.White; g.RowHeadersVisible = false;
        g.BorderStyle = BorderStyle.None; g.EnableHeadersVisualStyles = false;
        g.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        { BackColor = Color.FromArgb(40,40,40), ForeColor = Color.White,
          Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
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
                    Grid.Rows.Add(i.Id, i.Name, i.Ssid, i.Band, i.Frequency,
                        i.SecurityProfile, i.Running ? "✔" : "✘", i.Disabled ? "Sim" : "Não");
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
        try
        {
            if (enable) await _svc.EnableInterfaceAsync(id);
            else        await _svc.DisableInterfaceAsync(id);
            await LoadDataAsync();
        }
        catch (Exception ex) { ShowError(ex); }
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

        AddToolbarButton("＋ Nova Bridge", Color.FromArgb(0,120,215)).Click  += BtnAdd_Click;
        AddToolbarButton("✎ Editar",       Color.FromArgb(80,80,80)).Click   += BtnEdit_Click;
        AddToolbarButton("✕ Apagar",       Color.FromArgb(180,30,30)).Click  += async (_, _) => await DeleteAsync();
        AddToolbarButton("↺ Atualizar",    Color.FromArgb(0,100,180)).Click  += async (_, _) => await LoadDataAsync();

        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name=".id",      HeaderText="ID",        Visible=false });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name="name",     HeaderText="Nome",      FillWeight=25 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name="mac",      HeaderText="MAC",       FillWeight=30 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name="vlan",     HeaderText="VLAN Filter",FillWeight=20 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name="running",  HeaderText="Running",   FillWeight=15 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name="disabled", HeaderText="Disabled",  FillWeight=10 });
    }

    protected override async Task LoadDataAsync()
    {
        try
        {
            var list = await _svc.GetBridgesAsync();
            Grid.Rows.Clear();
            foreach (var b in list)
                Grid.Rows.Add(b.Id, b.Name, b.MacAddress,
                    b.VlanFiltering ? "Sim" : "Não", b.Running ? "✔" : "✘", b.Disabled ? "Sim" : "Não");
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
        Size = new Size(320, 180); FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9.5f);

        Controls.Add(new Label { Text = "Nome:",    Location = new Point(14, 20), AutoSize = true });
        _txtName.Location = new Point(100, 17); _txtName.Width = 185; Controls.Add(_txtName);

        Controls.Add(new Label { Text = "Comentário:", Location = new Point(14, 58), AutoSize = true });
        _txtComment.Location = new Point(100, 55); _txtComment.Width = 185; Controls.Add(_txtComment);

        if (existing != null) { _txtName.Text = existing.Name; _txtComment.Text = existing.Comment; }

        var ok = new Button { Text="Guardar", Location=new Point(100,95), Size=new Size(85,28),
            BackColor=Color.FromArgb(0,120,215), ForeColor=Color.White, FlatStyle=FlatStyle.Flat };
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
//  IP PANEL
// ════════════════════════════════════════════════════════════════════

public class IpPanel : BasePanel
{
    private readonly IpService _svc;

    public IpPanel(MikroTikClient client) : base(client)
    {
        _svc = new IpService(client);
        AddToolbarButton("＋ Adicionar",  Color.FromArgb(0,120,215)).Click += BtnAdd_Click;
        AddToolbarButton("✕ Apagar",      Color.FromArgb(180,30,30)).Click += async (_, _) => await DeleteAsync();
        AddToolbarButton("↺ Atualizar",   Color.FromArgb(0,100,180)).Click += async (_, _) => await LoadDataAsync();

        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name=".id",       HeaderText="ID",        Visible=false });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name="address",   HeaderText="Endereço",  FillWeight=30 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name="network",   HeaderText="Rede",      FillWeight=25 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name="interface", HeaderText="Interface",  FillWeight=25 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name="dynamic",   HeaderText="Dinâmico",  FillWeight=10 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name="disabled",  HeaderText="Disabled",  FillWeight=10 });
    }

    protected override async Task LoadDataAsync()
    {
        try
        {
            var list = await _svc.GetAddressesAsync();
            Grid.Rows.Clear();
            foreach (var a in list)
                Grid.Rows.Add(a.Id, a.Address, a.Network, a.Interface,
                    a.Dynamic ? "Sim" : "Não", a.Disabled ? "Sim" : "Não");
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
        void Row(string lbl, TextBox tb) {
            Controls.Add(new Label { Text=lbl, Location=new Point(14,y+3), AutoSize=true });
            tb.Location = new Point(120,y); tb.Width = 185; Controls.Add(tb); y += 38;
        }
        Row("Endereço (CIDR):", _txtAddress);    // ex: 192.168.1.1/24
        Row("Interface:", _txtInterface);
        Row("Comentário:", _txtComment);

        var hint = new Label { Text="ex: 192.168.1.1/24", Location=new Point(120,y-26),
            AutoSize=true, ForeColor=Color.Gray, Font=new Font("Segoe UI",8f) };
        Controls.Add(hint);

        var ok = new Button { Text="Guardar", Location=new Point(120,y+8), Size=new Size(85,28),
            BackColor=Color.FromArgb(0,120,215), ForeColor=Color.White, FlatStyle=FlatStyle.Flat };
        ok.FlatAppearance.BorderSize = 0;
        ok.Click += (_, _) => {
            if (string.IsNullOrWhiteSpace(_txtAddress.Text) || string.IsNullOrWhiteSpace(_txtInterface.Text))
            { MessageBox.Show("Endereço e Interface são obrigatórios."); return; }
            Address = new IpAddress { Address=_txtAddress.Text.Trim(), Interface=_txtInterface.Text.Trim(), Comment=_txtComment.Text };
            DialogResult = DialogResult.OK; Close();
        };
        Controls.Add(ok); AcceptButton = ok;
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
        AddToolbarButton("＋ Nova Rota",  Color.FromArgb(0,120,215)).Click  += BtnAdd_Click;
        AddToolbarButton("✕ Apagar",      Color.FromArgb(180,30,30)).Click  += async (_, _) => await DeleteAsync();
        AddToolbarButton("✔ Ativar",      Color.FromArgb(0,140,80)).Click   += async (_, _) => await ToggleAsync(true);
        AddToolbarButton("✕ Desativar",   Color.FromArgb(100,60,60)).Click  += async (_, _) => await ToggleAsync(false);
        AddToolbarButton("↺ Atualizar",   Color.FromArgb(0,100,180)).Click  += async (_, _) => await LoadDataAsync();

        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name=".id",         HeaderText="ID",       Visible=false });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name="dst-address", HeaderText="Destino",  FillWeight=30 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name="gateway",     HeaderText="Gateway",  FillWeight=25 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name="distance",    HeaderText="Distância",FillWeight=15 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name="active",      HeaderText="Ativa",    FillWeight=15 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name="dynamic",     HeaderText="Dinâmica", FillWeight=15 });
    }

    protected override async Task LoadDataAsync()
    {
        try
        {
            var list = await _svc.GetRoutesAsync();
            Grid.Rows.Clear();
            foreach (var r in list)
                Grid.Rows.Add(r.Id, r.DstAddress, r.Gateway, r.Distance,
                    r.Active ? "✔" : "✘", r.Dynamic ? "Sim" : "Não");
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
        try {
            if (enable) await _svc.EnableRouteAsync(id); else await _svc.DisableRouteAsync(id);
            await LoadDataAsync();
        } catch (Exception ex) { ShowError(ex); }
    }
}

public class RouteEditForm : Form
{
    public StaticRoute Route { get; private set; } = new();
    private readonly TextBox _txtDst = new(), _txtGw = new(), _txtComment = new();
    private readonly NumericUpDown _numDist = new() { Minimum=1, Maximum=255, Value=1 };

    public RouteEditForm()
    {
        Text = "Nova Rota Estática"; Size = new Size(340, 230);
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;
        StartPosition = FormStartPosition.CenterParent; Font = new Font("Segoe UI", 9.5f);

        int y = 18;
        void Row(string lbl, Control ctrl) {
            Controls.Add(new Label { Text=lbl, Location=new Point(14,y+3), AutoSize=true });
            ctrl.Location = new Point(140,y); ((Control)ctrl).Width = 165; Controls.Add(ctrl); y += 38;
        }
        Row("Destino (CIDR):", _txtDst);
        Row("Gateway:", _txtGw);
        Row("Distância:", _numDist);
        Row("Comentário:", _txtComment);

        var ok = new Button { Text="Guardar", Location=new Point(140,y+5), Size=new Size(85,28),
            BackColor=Color.FromArgb(0,120,215), ForeColor=Color.White, FlatStyle=FlatStyle.Flat };
        ok.FlatAppearance.BorderSize = 0;
        ok.Click += (_, _) => {
            if (string.IsNullOrWhiteSpace(_txtDst.Text) || string.IsNullOrWhiteSpace(_txtGw.Text))
            { MessageBox.Show("Destino e Gateway são obrigatórios."); return; }
            Route = new StaticRoute { DstAddress=_txtDst.Text.Trim(), Gateway=_txtGw.Text.Trim(),
                Distance=_numDist.Value.ToString(), Comment=_txtComment.Text };
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

    public DhcpPanel(MikroTikClient client) : base(client)
    {
        _svc = new DhcpService(client);
        AddToolbarButton("＋ Novo Servidor", Color.FromArgb(0,120,215)).Click += BtnAdd_Click;
        AddToolbarButton("✕ Apagar",         Color.FromArgb(180,30,30)).Click += async (_, _) => await DeleteAsync();
        AddToolbarButton("✔ Ativar",         Color.FromArgb(0,140,80)).Click  += async (_, _) => await ToggleAsync(true);
        AddToolbarButton("✕ Desativar",      Color.FromArgb(100,60,60)).Click += async (_, _) => await ToggleAsync(false);
        AddToolbarButton("↺ Atualizar",      Color.FromArgb(0,100,180)).Click += async (_, _) => await LoadDataAsync();

        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name=".id",          HeaderText="ID",         Visible=false });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name="name",         HeaderText="Nome",       FillWeight=25 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name="interface",    HeaderText="Interface",  FillWeight=25 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name="address-pool", HeaderText="Pool",       FillWeight=20 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name="lease-time",   HeaderText="Lease Time", FillWeight=15 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name="disabled",     HeaderText="Disabled",   FillWeight=15 });
    }

    protected override async Task LoadDataAsync()
    {
        try
        {
            var list = await _svc.GetServersAsync();
            Grid.Rows.Clear();
            foreach (var d in list)
                Grid.Rows.Add(d.Id, d.Name, d.Interface, d.AddressPool, d.LeaseTime, d.Disabled ? "Sim" : "Não");
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private async void BtnAdd_Click(object? s, EventArgs e)
    {
        using var frm = new DhcpServerEditForm();
        if (frm.ShowDialog() != DialogResult.OK) return;
        try { await _svc.CreateServerAsync(frm.Server); await LoadDataAsync(); }
        catch (Exception ex) { ShowError(ex); }
    }

    private async Task DeleteAsync()
    {
        var id = SelectedId(); if (id == null) { MessageBox.Show("Seleciona um servidor DHCP."); return; }
        if (MessageBox.Show("Apagar este servidor DHCP?", "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        try { await _svc.DeleteServerAsync(id); await LoadDataAsync(); }
        catch (Exception ex) { ShowError(ex); }
    }

    private async Task ToggleAsync(bool enable)
    {
        var id = SelectedId(); if (id == null) { MessageBox.Show("Seleciona um servidor DHCP."); return; }
        try {
            if (enable) await _svc.EnableServerAsync(id); else await _svc.DisableServerAsync(id);
            await LoadDataAsync();
        } catch (Exception ex) { ShowError(ex); }
    }
}

public class DhcpServerEditForm : Form
{
    public DhcpServer Server { get; private set; } = new();
    private readonly TextBox _txtName=new(), _txtInterface=new(), _txtPool=new(), _txtLease=new();

    public DhcpServerEditForm()
    {
        Text="Novo Servidor DHCP"; Size=new Size(340,230);
        FormBorderStyle=FormBorderStyle.FixedDialog; MaximizeBox=false;
        StartPosition=FormStartPosition.CenterParent; Font=new Font("Segoe UI",9.5f);
        _txtLease.Text = "10m";

        int y=18;
        void Row(string lbl, TextBox tb) {
            Controls.Add(new Label { Text=lbl, Location=new Point(14,y+3), AutoSize=true });
            tb.Location=new Point(120,y); tb.Width=185; Controls.Add(tb); y+=38;
        }
        Row("Nome:", _txtName);
        Row("Interface:", _txtInterface);
        Row("Pool:", _txtPool);
        Row("Lease Time:", _txtLease);

        var ok=new Button { Text="Guardar", Location=new Point(120,y+5), Size=new Size(85,28),
            BackColor=Color.FromArgb(0,120,215), ForeColor=Color.White, FlatStyle=FlatStyle.Flat };
        ok.FlatAppearance.BorderSize=0;
        ok.Click+=(_, _) => {
            if (string.IsNullOrWhiteSpace(_txtName.Text)||string.IsNullOrWhiteSpace(_txtInterface.Text))
            { MessageBox.Show("Nome e Interface são obrigatórios."); return; }
            Server=new DhcpServer { Name=_txtName.Text.Trim(), Interface=_txtInterface.Text.Trim(),
                AddressPool=_txtPool.Text.Trim(), LeaseTime=_txtLease.Text.Trim() };
            DialogResult=DialogResult.OK; Close();
        };
        Controls.Add(ok); AcceptButton=ok;
    }
}

// ════════════════════════════════════════════════════════════════════
//  DNS PANEL
// ════════════════════════════════════════════════════════════════════

public class DnsPanel : BasePanel
{
    private readonly DnsService _svc;
    private readonly TextBox _txtServers = new();
    private readonly CheckBox _chkAllow  = new() { Text = "Permitir pedidos remotos", AutoSize=true };
    private DnsSettings? _current;

    public DnsPanel(MikroTikClient client) : base(client)
    {
        _svc = new DnsService(client);
        Controls.Remove(Grid);   // DNS não tem lista — substitui por form

        var form = new Panel { Dock=DockStyle.Fill, Padding=new Padding(20) };

        int y=20;
        void Row(string lbl, Control ctrl) {
            form.Controls.Add(new Label { Text=lbl, Location=new Point(0,y+3), AutoSize=true, Font=new Font("Segoe UI",10f) });
            ctrl.Location=new Point(220,y); ctrl.Width=300; form.Controls.Add(ctrl); y+=40;
        }
        Row("Servidores DNS:", _txtServers);         // ex: 1.1.1.1,8.8.8.8
        _chkAllow.Location=new Point(220,y); form.Controls.Add(_chkAllow); y+=40;

        var btnSave = new Button { Text="💾 Guardar", Location=new Point(220,y+10), Size=new Size(110,32),
            BackColor=Color.FromArgb(0,120,215), ForeColor=Color.White, FlatStyle=FlatStyle.Flat };
        btnSave.FlatAppearance.BorderSize=0;
        btnSave.Click += BtnSave_Click;

        var btnFlush = new Button { Text="🗑 Limpar Cache", Location=new Point(340,y+10), Size=new Size(130,32),
            BackColor=Color.FromArgb(150,80,0), ForeColor=Color.White, FlatStyle=FlatStyle.Flat };
        btnFlush.FlatAppearance.BorderSize=0;
        btnFlush.Click += async (_, _) => {
            try { await _svc.FlushCacheAsync(); MessageBox.Show("Cache DNS limpa."); }
            catch (Exception ex) { ShowError(ex); }
        };

        form.Controls.Add(btnSave);
        form.Controls.Add(btnFlush);
        Controls.Add(form);
    }

    protected override async Task LoadDataAsync()
    {
        try
        {
            _current = await _svc.GetSettingsAsync();
            if (_current != null)
            {
                _txtServers.Text  = _current.Servers;
                _chkAllow.Checked = _current.AllowRemoteRequests;
            }
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private async void BtnSave_Click(object? s, EventArgs e)
    {
        try
        {
            await Client.PostAsync("ip/dns/set", new Dictionary<string, object>
            {
                ["servers"] = _txtServers.Text.Trim(),
                ["allow-remote-requests"] = _chkAllow.Checked ? "yes" : "no"
            });
            MessageBox.Show("Definições DNS guardadas.", "Sucesso", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { ShowError(ex); }
    }
}

// ════════════════════════════════════════════════════════════════════
//  WIREGUARD PANEL (2ª Parte)
// ════════════════════════════════════════════════════════════════════

public class WireGuardPanel : BasePanel
{
    private readonly WireGuardService _svc;
    private readonly TabControl _innerTabs = new();
    private readonly DataGridView _gridPeers = new();
    private List<WireGuardInterface> _interfaces = new();

    public WireGuardPanel(MikroTikClient client) : base(client)
    {
        _svc = new WireGuardService(client);
        Controls.Remove(Grid);

        _innerTabs.Dock = DockStyle.Fill;

        // Tab interfaces WG
        var tabIf = new TabPage("Interfaces WireGuard");
        Grid.Dock = DockStyle.Fill;
        tabIf.Controls.Add(Grid);

        // Tab peers
        var tabPeers = new TabPage("Peers");
        StyleGrid(_gridPeers);
        _gridPeers.Dock = DockStyle.Fill;
        tabPeers.Controls.Add(_gridPeers);

        _innerTabs.TabPages.AddRange(new[] { tabIf, tabPeers });
        _innerTabs.SelectedIndexChanged += async (_, _) => await LoadDataAsync();
        Controls.Add(_innerTabs);

        // Toolbar buttons
        AddToolbarButton("＋ Nova Interface", Color.FromArgb(0,120,215)).Click += BtnAddIf_Click;
        AddToolbarButton("＋ Novo Peer",       Color.FromArgb(0,140,80)).Click  += BtnAddPeer_Click;
        AddToolbarButton("✕ Apagar",           Color.FromArgb(180,30,30)).Click += async (_, _) => await DeleteAsync();
        AddToolbarButton("📄 Exportar Config", Color.FromArgb(80,80,140)).Click += BtnExport_Click;
        AddToolbarButton("↺ Atualizar",        Color.FromArgb(0,100,180)).Click += async (_, _) => await LoadDataAsync();

        // Colunas interfaces
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name=".id",         HeaderText="ID",          Visible=false });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name="name",        HeaderText="Nome",        FillWeight=20 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name="listen-port", HeaderText="Porta",       FillWeight=15 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name="public-key",  HeaderText="Chave Pública",FillWeight=50 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name="running",     HeaderText="Ativo",       FillWeight=10 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name="disabled",    HeaderText="Disabled",    FillWeight=10 });

        // Colunas peers
        _gridPeers.Columns.Add(new DataGridViewTextBoxColumn { Name=".id",             HeaderText="ID",           Visible=false });
        _gridPeers.Columns.Add(new DataGridViewTextBoxColumn { Name="interface",       HeaderText="Interface",    FillWeight=15 });
        _gridPeers.Columns.Add(new DataGridViewTextBoxColumn { Name="public-key",      HeaderText="Chave Pública",FillWeight=35 });
        _gridPeers.Columns.Add(new DataGridViewTextBoxColumn { Name="allowed-address", HeaderText="Endereços Permitidos",FillWeight=25 });
        _gridPeers.Columns.Add(new DataGridViewTextBoxColumn { Name="comment",         HeaderText="Comentário",   FillWeight=25 });
    }

    private static void StyleGrid(DataGridView g)
    {
        g.ReadOnly=true; g.AllowUserToAddRows=false; g.AllowUserToDeleteRows=false;
        g.SelectionMode=DataGridViewSelectionMode.FullRowSelect;
        g.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;
        g.BackgroundColor=Color.White; g.RowHeadersVisible=false; g.BorderStyle=BorderStyle.None;
        g.EnableHeadersVisualStyles=false;
        g.ColumnHeadersDefaultCellStyle=new DataGridViewCellStyle {
            BackColor=Color.FromArgb(40,40,40), ForeColor=Color.White,
            Font=new Font("Segoe UI",9f,FontStyle.Bold) };
    }

    protected override async Task LoadDataAsync()
    {
        try
        {
            if (_innerTabs.SelectedIndex == 0)
            {
                _interfaces = await _svc.GetInterfacesAsync();
                Grid.Rows.Clear();
                foreach (var i in _interfaces)
                    Grid.Rows.Add(i.Id, i.Name, i.ListenPort, i.PublicKey,
                        i.Running?"✔":"✘", i.Disabled?"Sim":"Não");
            }
            else
            {
                var peers = await _svc.GetPeersAsync();
                _gridPeers.Rows.Clear();
                foreach (var p in peers)
                    _gridPeers.Rows.Add(p.Id, p.Interface, p.PublicKey, p.AllowedAddress, p.Comment);
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

    private async void BtnAddPeer_Click(object? s, EventArgs e)
    {
        using var frm = new WgPeerEditForm(_interfaces.Select(i=>i.Name).ToList());
        if (frm.ShowDialog() != DialogResult.OK) return;
        try { await _svc.AddPeerAsync(frm.Peer); await LoadDataAsync(); }
        catch (Exception ex) { ShowError(ex); }
    }

    private async Task DeleteAsync()
    {
        if (_innerTabs.SelectedIndex == 0)
        {
            var id = SelectedId(); if (id==null) { MessageBox.Show("Seleciona uma interface WireGuard."); return; }
            if (MessageBox.Show("Apagar interface WireGuard?","Confirmar",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes) return;
            try { await _svc.DeleteInterfaceAsync(id); await LoadDataAsync(); }
            catch (Exception ex) { ShowError(ex); }
        }
        else
        {
            if (_gridPeers.SelectedRows.Count==0) { MessageBox.Show("Seleciona um peer."); return; }
            var id = _gridPeers.SelectedRows[0].Cells[".id"].Value?.ToString();
            if (id==null) return;
            if (MessageBox.Show("Apagar este peer?","Confirmar",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes) return;
            try { await _svc.DeletePeerAsync(id); await LoadDataAsync(); }
            catch (Exception ex) { ShowError(ex); }
        }
    }

    private void BtnExport_Click(object? s, EventArgs e)
    {
        using var frm = new WgClientConfigForm(_interfaces);
        frm.ShowDialog();
    }
}

// ── Formulários WireGuard ─────────────────────────────────────────

public class WgInterfaceEditForm : Form
{
    public WireGuardInterface WgInterface { get; private set; } = new();
    private readonly TextBox _txtName=new(), _txtPort=new(), _txtComment=new();

    public WgInterfaceEditForm()
    {
        Text="Nova Interface WireGuard"; Size=new Size(340,210);
        FormBorderStyle=FormBorderStyle.FixedDialog; MaximizeBox=false;
        StartPosition=FormStartPosition.CenterParent; Font=new Font("Segoe UI",9.5f);
        _txtPort.Text="13231";

        int y=18;
        void Row(string lbl, TextBox tb) {
            Controls.Add(new Label { Text=lbl, Location=new Point(14,y+3), AutoSize=true });
            tb.Location=new Point(120,y); tb.Width=185; Controls.Add(tb); y+=38;
        }
        Row("Nome:", _txtName);
        Row("Porta:", _txtPort);
        Row("Comentário:", _txtComment);

        Controls.Add(new Label { Text="💡 A chave privada é gerada automaticamente pelo RouterOS.",
            Location=new Point(14,y), AutoSize=true, ForeColor=Color.Gray, Font=new Font("Segoe UI",8f) });

        var ok=new Button { Text="Criar", Location=new Point(120,y+22), Size=new Size(85,28),
            BackColor=Color.FromArgb(0,120,215), ForeColor=Color.White, FlatStyle=FlatStyle.Flat };
        ok.FlatAppearance.BorderSize=0;
        ok.Click+=(_, _) => {
            if (string.IsNullOrWhiteSpace(_txtName.Text)) { MessageBox.Show("Nome obrigatório."); return; }
            WgInterface=new WireGuardInterface { Name=_txtName.Text.Trim(), ListenPort=_txtPort.Text.Trim(), Comment=_txtComment.Text };
            DialogResult=DialogResult.OK; Close();
        };
        Controls.Add(ok); AcceptButton=ok;
    }
}

public class WgPeerEditForm : Form
{
    public WireGuardPeer Peer { get; private set; } = new();
    private readonly ComboBox _cboInterface=new();
    private readonly TextBox _txtPublicKey=new(), _txtAllowedAddr=new(), _txtComment=new();

    public WgPeerEditForm(List<string> interfaces)
    {
        Text="Novo Peer WireGuard"; Size=new Size(400,250);
        FormBorderStyle=FormBorderStyle.FixedDialog; MaximizeBox=false;
        StartPosition=FormStartPosition.CenterParent; Font=new Font("Segoe UI",9.5f);
        _cboInterface.DropDownStyle=ComboBoxStyle.DropDownList;
        interfaces.ForEach(i => _cboInterface.Items.Add(i));
        if (_cboInterface.Items.Count>0) _cboInterface.SelectedIndex=0;

        int y=18;
        void Row(string lbl, Control ctrl) {
            Controls.Add(new Label { Text=lbl, Location=new Point(14,y+3), AutoSize=true });
            ctrl.Location=new Point(140,y); ((Control)ctrl).Width=220; Controls.Add(ctrl); y+=38;
        }
        Row("Interface:", _cboInterface);
        Row("Chave Pública:", _txtPublicKey);
        Row("Endereços (CIDR):", _txtAllowedAddr);
        Row("Comentário:", _txtComment);

        var ok=new Button { Text="Adicionar", Location=new Point(140,y+8), Size=new Size(100,28),
            BackColor=Color.FromArgb(0,120,215), ForeColor=Color.White, FlatStyle=FlatStyle.Flat };
        ok.FlatAppearance.BorderSize=0;
        ok.Click+=(_, _) => {
            if (string.IsNullOrWhiteSpace(_txtPublicKey.Text)||string.IsNullOrWhiteSpace(_txtAllowedAddr.Text))
            { MessageBox.Show("Chave Pública e Endereços são obrigatórios."); return; }
            Peer=new WireGuardPeer { Interface=_cboInterface.Text, PublicKey=_txtPublicKey.Text.Trim(),
                AllowedAddress=_txtAllowedAddr.Text.Trim(), Comment=_txtComment.Text };
            DialogResult=DialogResult.OK; Close();
        };
        Controls.Add(ok); AcceptButton=ok;
    }
}

public class WgClientConfigForm : Form
{
    public WgClientConfigForm(List<WireGuardInterface> interfaces)
    {
        Text="Gerar Configuração Cliente WireGuard"; Size=new Size(560,520);
        FormBorderStyle=FormBorderStyle.Sizable; StartPosition=FormStartPosition.CenterParent;
        Font=new Font("Segoe UI",9.5f);

        var cboIf=new ComboBox { Location=new Point(160,14), Width=200, DropDownStyle=ComboBoxStyle.DropDownList };
        interfaces.ForEach(i => cboIf.Items.Add(i.Name));
        if (cboIf.Items.Count>0) cboIf.SelectedIndex=0;

        var fields = new[] {
            ("Interface WG Servidor:", (Control)cboIf),
        };

        var txtClientPrivKey = new TextBox { Location=new Point(160,52), Width=340, ScrollBars=ScrollBars.Horizontal };
        var txtClientAddr    = new TextBox { Location=new Point(160,90), Width=200 };
        var txtEndpoint      = new TextBox { Location=new Point(160,128), Width=200 };
        var txtPort          = new TextBox { Location=new Point(160,166), Width=80, Text="13231" };
        var txtDns           = new TextBox { Location=new Point(160,204), Width=200, Text="1.1.1.1" };

        int y=14;
        void Lbl(string t, int ly) => Controls.Add(new Label { Text=t, Location=new Point(10,ly+3), AutoSize=true });
        Lbl("Interface do Servidor:", y); Controls.Add(cboIf);
        y+=38; Lbl("Chave Privada do Cliente:", y); Controls.Add(txtClientPrivKey);
        y+=38; Lbl("Endereço do Cliente:", y); Controls.Add(txtClientAddr);
        y+=38; Lbl("Endpoint (IP público):", y); Controls.Add(txtEndpoint);
        y+=38; Lbl("Porta:", y); Controls.Add(txtPort);
        y+=38; Lbl("DNS:", y); Controls.Add(txtDns);
        y+=38;

        var txtOutput=new TextBox { Location=new Point(10,y+10), Size=new Size(520,170),
            Multiline=true, ReadOnly=true, Font=new Font("Consolas",9f), ScrollBars=ScrollBars.Vertical };
        Controls.Add(txtOutput);

        var btnGen=new Button { Text="⚙ Gerar Config", Location=new Point(10,y), Size=new Size(130,28),
            BackColor=Color.FromArgb(0,120,215), ForeColor=Color.White, FlatStyle=FlatStyle.Flat };
        btnGen.FlatAppearance.BorderSize=0;
        btnGen.Click+=(_, _) => {
            var iface = interfaces.FirstOrDefault(i=>i.Name==cboIf.Text);
            if (iface==null) return;
            var cfg = WireGuardService.GenerateClientConfig(
                new WireGuardPeer(), iface.PublicKey, txtEndpoint.Text,
                txtPort.Text, txtClientPrivKey.Text, txtClientAddr.Text, txtDns.Text);
            txtOutput.Text = cfg;
        };
        Controls.Add(btnGen);

        var btnSave=new Button { Text="💾 Guardar .conf", Location=new Point(150,y), Size=new Size(130,28),
            BackColor=Color.FromArgb(0,140,60), ForeColor=Color.White, FlatStyle=FlatStyle.Flat };
        btnSave.FlatAppearance.BorderSize=0;
        btnSave.Click+=(_, _) => {
            if (string.IsNullOrWhiteSpace(txtOutput.Text)) return;
            using var dlg=new SaveFileDialog { Filter="WireGuard Config|*.conf", FileName="wg-client.conf" };
            if (dlg.ShowDialog()==DialogResult.OK) File.WriteAllText(dlg.FileName, txtOutput.Text);
        };
        Controls.Add(btnSave);
    }
}
