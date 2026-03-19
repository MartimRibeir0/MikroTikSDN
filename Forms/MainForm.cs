using MikroTikSDN.Models;
using MikroTikSDN.Services;

namespace MikroTikSDN.Forms;

public partial class MainForm : Form
{
    // ── Dispositivo atualmente selecionado
    private RouterDevice? _selectedDevice;

    // ── Controlos principais
    private readonly ComboBox _cboDevices = new();
    private readonly Button _btnAddDevice = new();
    private readonly Button _btnEditDevice = new();
    private readonly Button _btnRemoveDevice = new();
    private readonly Button _btnTestConn = new();
    private readonly Label _lblStatus = new();
    private readonly TabControl _tabs = new();

    // ── Tabs (cada uma é um painel de funcionalidades)
    private TabPage _tabInterfaces = new();
    private TabPage _tabWireless  = new();
    private TabPage _tabBridge    = new();
    private TabPage _tabIp        = new();
    private TabPage _tabRoutes    = new();
    private TabPage _tabDhcp      = new();
    private TabPage _tabDns       = new();
    private TabPage _tabWireGuard = new();

    public MainForm()
    {
        Text = "MikroTik SDN Controller";
        Size = new Size(1100, 700);
        MinimumSize = new Size(900, 600);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);

        BuildUI();
        DeviceManager.Load();
        RefreshDeviceList();
    }

    // ════════════════════════════════════════════════════════════════
    //  Construção da UI
    // ════════════════════════════════════════════════════════════════

    private void BuildUI()
    {
        // ── Painel superior (seleção de dispositivo)
        var topPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 52,
            BackColor = Color.FromArgb(30, 30, 30),
            Padding = new Padding(8, 8, 8, 6)
        };

        var lblDevice = new Label
        {
            Text = "Dispositivo:", ForeColor = Color.White,
            AutoSize = true, Location = new Point(10, 15)
        };

        _cboDevices.Location = new Point(90, 11);
        _cboDevices.Width = 220;
        _cboDevices.DropDownStyle = ComboBoxStyle.DropDownList;
        _cboDevices.SelectedIndexChanged += CboDevices_Changed;

        StyleButton(_btnAddDevice,    "＋ Adicionar", 320, 9,  Color.FromArgb(0, 120, 215));
        StyleButton(_btnEditDevice,   "✎ Editar",    425, 9,  Color.FromArgb(80, 80, 80));
        StyleButton(_btnRemoveDevice, "✕ Remover",   510, 9,  Color.FromArgb(180, 30, 30));
        StyleButton(_btnTestConn,     "⚡ Testar",   600, 9,  Color.FromArgb(0, 150, 100));

        _lblStatus.AutoSize = true;
        _lblStatus.ForeColor = Color.LightGray;
        _lblStatus.Location = new Point(700, 15);
        _lblStatus.Text = "Nenhum dispositivo selecionado";

        _btnAddDevice.Click    += BtnAdd_Click;
        _btnEditDevice.Click   += BtnEdit_Click;
        _btnRemoveDevice.Click += BtnRemove_Click;
        _btnTestConn.Click     += BtnTest_Click;

        topPanel.Controls.AddRange(new Control[]
            { lblDevice, _cboDevices, _btnAddDevice, _btnEditDevice,
              _btnRemoveDevice, _btnTestConn, _lblStatus });

        // ── TabControl principal
        _tabs.Dock = DockStyle.Fill;
        _tabs.Font = new Font("Segoe UI", 9.5f);

        BuildTabs();
        _tabs.SelectedIndexChanged += Tabs_Changed;

        Controls.Add(_tabs);
        Controls.Add(topPanel);  // Add topPanel AFTER tabs so it draws on top
    }

    private static void StyleButton(Button btn, string text, int x, int y, Color color)
    {
        btn.Text = text;
        btn.Location = new Point(x, y);
        btn.Size = new Size(90, 30);
        btn.BackColor = color;
        btn.ForeColor = Color.White;
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderSize = 0;
        btn.Cursor = Cursors.Hand;
    }

    private void BuildTabs()
    {
        _tabInterfaces = CreateTab("🔌 Interfaces");
        _tabWireless   = CreateTab("📶 Wireless");
        _tabBridge     = CreateTab("🔗 Bridge");
        _tabIp         = CreateTab("🌐 Endereços IP");
        _tabRoutes     = CreateTab("🗺 Rotas");
        _tabDhcp       = CreateTab("📋 DHCP");
        _tabDns        = CreateTab("🔍 DNS");
        _tabWireGuard  = CreateTab("🔒 WireGuard VPN");

        _tabs.TabPages.AddRange(new[]
            { _tabInterfaces, _tabWireless, _tabBridge,
              _tabIp, _tabRoutes, _tabDhcp, _tabDns, _tabWireGuard });
    }

    private static TabPage CreateTab(string title)
    {
        var tab = new TabPage(title) { Padding = new Padding(6) };
        // Placeholder até ser carregado
        tab.Controls.Add(new Label
        {
            Text = "Seleciona um dispositivo para começar.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.Gray,
            Font = new Font("Segoe UI", 10f)
        });
        return tab;
    }

    // ════════════════════════════════════════════════════════════════
    //  Gestão de dispositivos
    // ════════════════════════════════════════════════════════════════

    private void RefreshDeviceList()
    {
        _cboDevices.Items.Clear();
        foreach (var d in DeviceManager.Devices)
            _cboDevices.Items.Add($"{d.Name}  ({d.Host})");

        if (_cboDevices.Items.Count > 0)
            _cboDevices.SelectedIndex = 0;
    }

    private void CboDevices_Changed(object? s, EventArgs e)
    {
        if (_cboDevices.SelectedIndex < 0) return;
        _selectedDevice = DeviceManager.Devices[_cboDevices.SelectedIndex];
        _lblStatus.Text = $"Dispositivo: {_selectedDevice.Name}";
        LoadCurrentTab();
    }

    private void BtnAdd_Click(object? s, EventArgs e)
    {
        using var frm = new AddEditDeviceForm();
        if (frm.ShowDialog() == DialogResult.OK)
        {
            DeviceManager.AddDevice(frm.Device);
            RefreshDeviceList();
            _cboDevices.SelectedIndex = _cboDevices.Items.Count - 1;
        }
    }

    private void BtnEdit_Click(object? s, EventArgs e)
    {
        if (_selectedDevice == null) return;
        using var frm = new AddEditDeviceForm(_selectedDevice);
        if (frm.ShowDialog() == DialogResult.OK)
        {
            DeviceManager.UpdateDevice(_cboDevices.SelectedIndex, frm.Device);
            RefreshDeviceList();
        }
    }

    private void BtnRemove_Click(object? s, EventArgs e)
    {
        if (_selectedDevice == null) return;
        if (MessageBox.Show($"Remover '{_selectedDevice.Name}'?", "Confirmar",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
        {
            DeviceManager.RemoveDevice(_cboDevices.SelectedIndex);
            _selectedDevice = null;
            RefreshDeviceList();
        }
    }

    private async void BtnTest_Click(object? s, EventArgs e)
    {
        if (_selectedDevice == null) { ShowStatus("Sem dispositivo selecionado", false); return; }
        _btnTestConn.Enabled = false;
        ShowStatus("A testar ligação...", null);
        var client = DeviceManager.GetClient(_selectedDevice);
        var (ok, info) = await client.TestConnectionAsync();
        ShowStatus(ok ? $"✔ Ligado — {info}" : $"✘ Falhou: {info}", ok);
        _btnTestConn.Enabled = true;
    }

    private void ShowStatus(string msg, bool? success)
    {
        _lblStatus.Text = msg;
        _lblStatus.ForeColor = success switch
        {
            true  => Color.LightGreen,
            false => Color.Salmon,
            null  => Color.LightGray
        };
    }

    // ════════════════════════════════════════════════════════════════
    //  Carregamento dinâmico de tabs
    // ════════════════════════════════════════════════════════════════

    private void Tabs_Changed(object? s, EventArgs e) => LoadCurrentTab();

    private void LoadCurrentTab()
    {
        if (_selectedDevice == null) return;
        var client = DeviceManager.GetClient(_selectedDevice);
        var tab = _tabs.SelectedTab;

        // Fix: Ensure tab is not null before dereferencing
        if (tab == null) return;

        // Limpa o conteúdo anterior e carrega o painel adequado
        tab.Controls.Clear();

        Control panel = tab == _tabInterfaces ? new InterfacesPanel(client)
            : tab == _tabWireless  ? new WirelessPanel(client)
            : tab == _tabBridge    ? new BridgePanel(client)
            : tab == _tabIp        ? new IpPanel(client)
            : tab == _tabRoutes    ? new RoutesPanel(client)
            : tab == _tabDhcp      ? new DhcpPanel(client)
            : tab == _tabDns       ? new DnsPanel(client)
            : tab == _tabWireGuard ? new WireGuardPanel(client)
            : new Label { Text = "Tab desconhecida", Dock = DockStyle.Fill };

        panel.Dock = DockStyle.Fill;
        tab.Controls.Add(panel);
    }
}
