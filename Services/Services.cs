using MikroTikSDN.Api;
using MikroTikSDN.Models;

namespace MikroTikSDN.Services;

// ════════════════════════════════════════════════════════════════════
//  INTERFACES
// ════════════════════════════════════════════════════════════════════

public class InterfaceService(MikroTikClient client)
{
    public Task<List<MikrotikInterface>> GetAllAsync() =>
        client.GetListAsync<MikrotikInterface>("interface");

    public Task<List<MikrotikInterface>> GetWirelessAsync() =>
        client.GetListAsync<MikrotikInterface>("interface/wireless");

    public Task EnableAsync(string id) =>
        client.PostAsync("interface/enable", new Dictionary<string, object> { [".id"] = id });

    public Task DisableAsync(string id) =>
        client.PostAsync("interface/disable", new Dictionary<string, object> { [".id"] = id });
}

// ════════════════════════════════════════════════════════════════════
//  WIRELESS
// ════════════════════════════════════════════════════════════════════

public class WirelessService(MikroTikClient client)
{
    // Interfaces wireless
    public Task<List<WirelessInterface>> GetInterfacesAsync() =>
        client.GetListAsync<WirelessInterface>("interface/wireless");

    public Task<WirelessInterface?> GetInterfaceAsync(string id) =>
        client.GetSingleAsync<WirelessInterface>($"interface/wireless/{id}");

    public Task UpdateInterfaceAsync(string id, object changes) =>
        client.PatchAsync($"interface/wireless/{id}", changes);

    public Task EnableInterfaceAsync(string id) =>
        client.PostAsync("interface/wireless/enable", new Dictionary<string, object> { [".id"] = id });

    public Task DisableInterfaceAsync(string id) =>
        client.PostAsync("interface/wireless/disable", new Dictionary<string, object> { [".id"] = id });

    // Perfis de segurança
    public Task<List<SecurityProfile>> GetSecurityProfilesAsync() =>
        client.GetListAsync<SecurityProfile>("interface/wireless/security-profiles");

    public Task<SecurityProfile?> CreateSecurityProfileAsync(SecurityProfile profile) =>
        client.PostAsync<SecurityProfile>("interface/wireless/security-profiles", profile);

    public Task UpdateSecurityProfileAsync(string id, object changes) =>
        client.PatchAsync($"interface/wireless/security-profiles/{id}", changes);

    public Task DeleteSecurityProfileAsync(string id) =>
        client.DeleteAsync($"interface/wireless/security-profiles/{id}");
}

// ════════════════════════════════════════════════════════════════════
//  BRIDGE
// ════════════════════════════════════════════════════════════════════

public class BridgeService(MikroTikClient client)
{
    // Bridge interfaces
    public Task<List<BridgeInterface>> GetBridgesAsync() =>
        client.GetListAsync<BridgeInterface>("interface/bridge");

    public Task<BridgeInterface?> CreateBridgeAsync(BridgeInterface bridge) =>
        client.PostAsync<BridgeInterface>("interface/bridge", bridge);

    public Task UpdateBridgeAsync(string id, object changes) =>
        client.PatchAsync($"interface/bridge/{id}", changes);

    public Task DeleteBridgeAsync(string id) =>
        client.DeleteAsync($"interface/bridge/{id}");

    // Bridge ports
    public Task<List<BridgePort>> GetPortsAsync() =>
        client.GetListAsync<BridgePort>("interface/bridge/port");

    public Task<List<BridgePort>> GetPortsByBridgeAsync(string bridgeName) =>
        // Filtra localmente pois o RouterOS não suporta query params neste endpoint
        GetPortsAsync().ContinueWith(t => t.Result.Where(p => p.Bridge == bridgeName).ToList());

    public Task<BridgePort?> AddPortAsync(BridgePort port) =>
        client.PostAsync<BridgePort>("interface/bridge/port", port);

    public Task UpdatePortAsync(string id, object changes) =>
        client.PatchAsync($"interface/bridge/port/{id}", changes);

    public Task RemovePortAsync(string id) =>
        client.DeleteAsync($"interface/bridge/port/{id}");
}

// ════════════════════════════════════════════════════════════════════
//  IP ADDRESSES
// ════════════════════════════════════════════════════════════════════

public class IpService(MikroTikClient client)
{
    public Task<List<IpAddress>> GetAddressesAsync() =>
        client.GetListAsync<IpAddress>("ip/address");

    public Task<IpAddress?> AddAddressAsync(IpAddress address) =>
        client.PostAsync<IpAddress>("ip/address", address);

    public Task UpdateAddressAsync(string id, object changes) =>
        client.PatchAsync($"ip/address/{id}", changes);

    public Task DeleteAddressAsync(string id) =>
        client.DeleteAsync($"ip/address/{id}");
}

// ════════════════════════════════════════════════════════════════════
//  ROTAS ESTÁTICAS
// ════════════════════════════════════════════════════════════════════

public class RoutingService(MikroTikClient client)
{
    public Task<List<StaticRoute>> GetRoutesAsync() =>
        client.GetListAsync<StaticRoute>("ip/route");

    public Task<StaticRoute?> AddRouteAsync(StaticRoute route) =>
        client.PostAsync<StaticRoute>("ip/route", route);

    public Task UpdateRouteAsync(string id, object changes) =>
        client.PatchAsync($"ip/route/{id}", changes);

    public Task DeleteRouteAsync(string id) =>
        client.DeleteAsync($"ip/route/{id}");

    public Task EnableRouteAsync(string id) =>
        client.PostAsync("ip/route/enable", new Dictionary<string, object> { [".id"] = id });

    public Task DisableRouteAsync(string id) =>
        client.PostAsync("ip/route/disable", new Dictionary<string, object> { [".id"] = id });
}

// ════════════════════════════════════════════════════════════════════
//  DHCP
// ════════════════════════════════════════════════════════════════════

public class DhcpService(MikroTikClient client)
{
    // Servidores DHCP
    public Task<List<DhcpServer>> GetServersAsync() =>
        client.GetListAsync<DhcpServer>("ip/dhcp-server");

    public Task<DhcpServer?> CreateServerAsync(DhcpServer server) =>
        client.PostAsync<DhcpServer>("ip/dhcp-server", server);

    public Task UpdateServerAsync(string id, object changes) =>
        client.PatchAsync($"ip/dhcp-server/{id}", changes);

    public Task DeleteServerAsync(string id) =>
        client.DeleteAsync($"ip/dhcp-server/{id}");

    public Task EnableServerAsync(string id) =>
        client.PostAsync("ip/dhcp-server/enable", new Dictionary<string, object> { [".id"] = id });

    public Task DisableServerAsync(string id) =>
        client.PostAsync("ip/dhcp-server/disable", new Dictionary<string, object> { [".id"] = id });

    // Pools de endereços
    public Task<List<DhcpPool>> GetPoolsAsync() =>
        client.GetListAsync<DhcpPool>("ip/pool");

    public Task<DhcpPool?> CreatePoolAsync(DhcpPool pool) =>
        client.PostAsync<DhcpPool>("ip/pool", pool);

    public Task DeletePoolAsync(string id) =>
        client.DeleteAsync($"ip/pool/{id}");

    // Redes DHCP
    public Task<List<DhcpNetwork>> GetNetworksAsync() =>
        client.GetListAsync<DhcpNetwork>("ip/dhcp-server/network");

    public Task<DhcpNetwork?> CreateNetworkAsync(DhcpNetwork network) =>
        client.PostAsync<DhcpNetwork>("ip/dhcp-server/network", network);

    public Task UpdateNetworkAsync(string id, object changes) =>
        client.PatchAsync($"ip/dhcp-server/network/{id}", changes);

    public Task DeleteNetworkAsync(string id) =>
        client.DeleteAsync($"ip/dhcp-server/network/{id}");
}

// ════════════════════════════════════════════════════════════════════
//  DNS
// ════════════════════════════════════════════════════════════════════

public class DnsService(MikroTikClient client)
{
    public Task<DnsSettings?> GetSettingsAsync() =>
        client.GetSingleAsync<DnsSettings>("ip/dns");

    public Task UpdateSettingsAsync(object changes) =>
        client.PostAsync("ip/dns/set", changes);

    public Task FlushCacheAsync() =>
        client.PostAsync("ip/dns/cache/flush", new { });
}

// ════════════════════════════════════════════════════════════════════
//  WIREGUARD (2ª Parte)
// ════════════════════════════════════════════════════════════════════

public class WireGuardService(MikroTikClient client)
{
    // Interfaces WireGuard
    public Task<List<WireGuardInterface>> GetInterfacesAsync() =>
        client.GetListAsync<WireGuardInterface>("interface/wireguard");

    public Task<WireGuardInterface?> CreateInterfaceAsync(WireGuardInterface wg) =>
        client.PostAsync<WireGuardInterface>("interface/wireguard", wg);

    public Task UpdateInterfaceAsync(string id, object changes) =>
        client.PatchAsync($"interface/wireguard/{id}", changes);

    public Task DeleteInterfaceAsync(string id) =>
        client.DeleteAsync($"interface/wireguard/{id}");

    // Peers
    public Task<List<WireGuardPeer>> GetPeersAsync() =>
        client.GetListAsync<WireGuardPeer>("interface/wireguard/peers");

    public Task<WireGuardPeer?> AddPeerAsync(WireGuardPeer peer) =>
        client.PostAsync<WireGuardPeer>("interface/wireguard/peers", peer);

    public Task UpdatePeerAsync(string id, object changes) =>
        client.PatchAsync($"interface/wireguard/peers/{id}", changes);

    public Task DeletePeerAsync(string id) =>
        client.DeleteAsync($"interface/wireguard/peers/{id}");

    /// <summary>
    /// Gera um ficheiro de configuração WireGuard para um cliente
    /// (Windows/Linux/Android/iOS) com base nos dados do peer e da interface servidor.
    /// </summary>
    public static string GenerateClientConfig(WireGuardPeer peer, string serverPublicKey,
        string serverEndpoint, string serverPort, string clientPrivateKey,
        string clientAddress, string dns = "1.1.1.1")
    {
        return $"""
            [Interface]
            PrivateKey = {clientPrivateKey}
            Address = {clientAddress}
            DNS = {dns}

            [Peer]
            PublicKey = {serverPublicKey}
            AllowedIPs = 0.0.0.0/0, ::/0
            Endpoint = {serverEndpoint}:{serverPort}
            PersistentKeepalive = 25
            """;
    }
}
