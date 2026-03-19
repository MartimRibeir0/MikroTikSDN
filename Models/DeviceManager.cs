using MikroTikSDN.Api;

namespace MikroTikSDN.Models;

/// <summary>Representa um dispositivo MikroTik guardado.</summary>
public class RouterDevice
{
    public string Name { get; set; } = "";
    public string Host { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";   // Em produção real: usar SecureString ou DPAPI
    public bool UseHttps { get; set; } = true;
    public int Port { get; set; } = 0;           // 0 = usa o default (443 ou 80)
}

/// <summary>
/// Gere a lista de dispositivos e os clientes ativos.
/// Persiste os dispositivos em JSON no AppData do utilizador.
/// </summary>
public static class DeviceManager
{
    private static readonly string _configPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "MikroTikSDN", "devices.json");

    private static List<RouterDevice> _devices = new();

    // Dicionário de clientes ativos (um por dispositivo)
    private static readonly Dictionary<string, MikroTikClient> _clients = new();

    public static IReadOnlyList<RouterDevice> Devices => _devices.AsReadOnly();

    // ─── Persistência ───────────────────────────────────────────────────────

    public static void Load()
    {
        try
        {
            if (!File.Exists(_configPath)) return;
            var json = File.ReadAllText(_configPath);
            _devices = JsonConvert.DeserializeObject<List<RouterDevice>>(json) ?? new();
        }
        catch { _devices = new(); }
    }

    public static void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
        File.WriteAllText(_configPath, JsonConvert.SerializeObject(_devices, Formatting.Indented));
    }

    // ─── CRUD de dispositivos ────────────────────────────────────────────────

    public static void AddDevice(RouterDevice device)
    {
        _devices.Add(device);
        Save();
    }

    public static void UpdateDevice(int index, RouterDevice device)
    {
        // Remove o cliente antigo se existir
        var old = _devices[index];
        if (_clients.ContainsKey(old.Host)) { _clients[old.Host].Dispose(); _clients.Remove(old.Host); }

        _devices[index] = device;
        Save();
    }

    public static void RemoveDevice(int index)
    {
        var dev = _devices[index];
        if (_clients.TryGetValue(dev.Host, out var c)) { c.Dispose(); _clients.Remove(dev.Host); }
        _devices.RemoveAt(index);
        Save();
    }

    // ─── Obter cliente ───────────────────────────────────────────────────────

    /// <summary>Devolve (ou cria) o cliente HTTP para o dispositivo.</summary>
    public static MikroTikClient GetClient(RouterDevice device)
    {
        if (!_clients.TryGetValue(device.Host, out var client))
        {
            client = new MikroTikClient(device.Host, device.Username, device.Password,
                                        device.Name, device.UseHttps, device.Port);
            _clients[device.Host] = client;
        }
        return client;
    }

    public static MikroTikClient? GetClient(string host) =>
        _clients.TryGetValue(host, out var c) ? c : null;
}
