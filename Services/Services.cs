using MikroTikSDN.Api;
using MikroTikSDN.Models;

namespace MikroTikSDN.Services;

public class InterfaceService(MikroTikClient client)
{
    public Task<List<MikrotikInterface>> GetAllAsync() =>
        client.GetListAsync<MikrotikInterface>("interface");

    public Task<List<MikrotikInterface>> GetWirelessAsync() =>
        client.GetListAsync<MikrotikInterface>("interface/wireless");

    public Task EnableAsync(string id) =>
        client.PatchAsync($"interface/{id}", new { disabled = "false" });

    public Task DisableAsync(string id) =>
        client.PatchAsync($"interface/{id}", new { disabled = "true" });
}

public class WirelessService(MikroTikClient client)
{
    public Task<List<WirelessInterface>> GetInterfacesAsync() =>
        client.GetListAsync<WirelessInterface>("interface/wireless");

    public Task<WirelessInterface?> GetInterfaceAsync(string id) =>
        client.GetSingleAsync<WirelessInterface>($"interface/wireless/{id}");

    public Task UpdateInterfaceAsync(string id, object changes) =>
        client.PatchAsync($"interface/wireless/{id}", changes);

    public Task EnableInterfaceAsync(string id) =>
        client.PatchAsync($"interface/wireless/{id}", new { disabled = "false" });

    public Task DisableInterfaceAsync(string id) =>
        client.PatchAsync($"interface/wireless/{id}", new { disabled = "true" });

    public Task<List<SecurityProfile>> GetSecurityProfilesAsync() =>
        client.GetListAsync<SecurityProfile>("interface/wireless/security-profiles");

    public Task<SecurityProfile?> CreateSecurityProfileAsync(SecurityProfile profile) =>
        client.PutAsync<SecurityProfile>("interface/wireless/security-profiles", profile);

    public Task UpdateSecurityProfileAsync(string id, object changes) =>
        client.PatchAsync($"interface/wireless/security-profiles/{id}", changes);

    public Task DeleteSecurityProfileAsync(string id) =>
        client.DeleteAsync($"interface/wireless/security-profiles/{id}");
}

public class BridgeService(MikroTikClient client)
{
    public Task<List<BridgeInterface>> GetBridgesAsync() =>
        client.GetListAsync<BridgeInterface>("interface/bridge");

    public Task<BridgeInterface?> CreateBridgeAsync(BridgeInterface bridge) =>
        client.PutAsync<BridgeInterface>("interface/bridge", bridge);

    public Task UpdateBridgeAsync(string id, object changes) =>
        client.PatchAsync($"interface/bridge/{id}", changes);

    public Task DeleteBridgeAsync(string id) =>
        client.DeleteAsync($"interface/bridge/{id}");

    public Task<List<BridgePort>> GetPortsAsync() =>
        client.GetListAsync<BridgePort>("interface/bridge/port");

    public Task<List<BridgePort>> GetPortsByBridgeAsync(string bridgeName) =>
        GetPortsAsync().ContinueWith(t => t.Result.Where(p => p.Bridge == bridgeName).ToList());

    public Task<BridgePort?> AddPortAsync(BridgePort port) =>
        client.PutAsync<BridgePort>("interface/bridge/port", port);

    public Task UpdatePortAsync(string id, object changes) =>
        client.PatchAsync($"interface/bridge/port/{id}", changes);

    public Task RemovePortAsync(string id) =>
        client.DeleteAsync($"interface/bridge/port/{id}");
}

public class IpService(MikroTikClient client)
{
    public Task<List<IpAddress>> GetAddressesAsync() =>
        client.GetListAsync<IpAddress>("ip/address");

    public Task<IpAddress?> AddAddressAsync(IpAddress address) =>
        client.PutAsync<IpAddress>("ip/address", address);

    public Task UpdateAddressAsync(string id, object changes) =>
        client.PatchAsync($"ip/address/{id}", changes);

    public Task DeleteAddressAsync(string id) =>
        client.DeleteAsync($"ip/address/{id}");
}

public class RoutingService(MikroTikClient client)
{
    public Task<List<StaticRoute>> GetRoutesAsync() =>
        client.GetListAsync<StaticRoute>("ip/route");

    public Task<StaticRoute?> AddRouteAsync(StaticRoute route) =>
        client.PutAsync<StaticRoute>("ip/route", route);

    public Task UpdateRouteAsync(string id, object changes) =>
        client.PatchAsync($"ip/route/{id}", changes);

    public Task DeleteRouteAsync(string id) =>
        client.DeleteAsync($"ip/route/{id}");

    public Task EnableRouteAsync(string id) =>
        client.PatchAsync($"ip/route/{id}", new { disabled = "false" });

    public Task DisableRouteAsync(string id) =>
        client.PatchAsync($"ip/route/{id}", new { disabled = "true" });
}

public class DhcpService(MikroTikClient client)
{
    public Task<List<DhcpServer>> GetServersAsync() =>
        client.GetListAsync<DhcpServer>("ip/dhcp-server");

    public Task<DhcpServer?> CreateServerAsync(DhcpServer server) =>
        client.PutAsync<DhcpServer>("ip/dhcp-server", server);

    public Task UpdateServerAsync(string id, object changes) =>
        client.PatchAsync($"ip/dhcp-server/{id}", changes);

    public Task DeleteServerAsync(string id) =>
        client.DeleteAsync($"ip/dhcp-server/{id}");

    public Task EnableServerAsync(string id) =>
        client.PatchAsync($"ip/dhcp-server/{id}", new { disabled = "false" });

    public Task DisableServerAsync(string id) =>
        client.PatchAsync($"ip/dhcp-server/{id}", new { disabled = "true" });

    public Task<List<DhcpPool>> GetPoolsAsync() =>
        client.GetListAsync<DhcpPool>("ip/pool");

    public Task<DhcpPool?> CreatePoolAsync(DhcpPool pool) =>
        client.PutAsync<DhcpPool>("ip/pool", pool);

    public Task DeletePoolAsync(string id) =>
        client.DeleteAsync($"ip/pool/{id}");

    public Task<List<DhcpNetwork>> GetNetworksAsync() =>
        client.GetListAsync<DhcpNetwork>("ip/dhcp-server/network");

    public Task<DhcpNetwork?> CreateNetworkAsync(DhcpNetwork network) =>
        client.PutAsync<DhcpNetwork>("ip/dhcp-server/network", network);

    public Task UpdateNetworkAsync(string id, object changes) =>
        client.PatchAsync($"ip/dhcp-server/network/{id}", changes);

    public Task DeleteNetworkAsync(string id) =>
        client.DeleteAsync($"ip/dhcp-server/network/{id}");
}

public class DnsService(MikroTikClient client)
{
    public Task<DnsSettings?> GetSettingsAsync() =>
        client.GetSingleAsync<DnsSettings>("ip/dns");

    public Task UpdateSettingsAsync(string servers, bool allowRemote) =>
        client.PatchAsync("ip/dns", new Dictionary<string, object>
        {
            ["servers"] = servers,
            ["allow-remote-requests"] = allowRemote ? "yes" : "no"
        });

    public async Task FlushCacheAsync()
    {
        try { await client.PostAsync("ip/dns/cache/flush", new { }); }
        catch { /* ignorar se ROS não suportar via REST */ }
    }

    // ---> ESTES SÃO OS MÉTODOS QUE TE ESTAVAM A FALTAR
    public Task<List<DnsStaticEntry>> GetStaticEntriesAsync() =>
        client.GetListAsync<DnsStaticEntry>("ip/dns/static");

    public Task<DnsStaticEntry?> AddStaticEntryAsync(DnsStaticEntry entry) =>
        client.PutAsync<DnsStaticEntry>("ip/dns/static", entry);

    public Task DeleteStaticEntryAsync(string id) =>
        client.DeleteAsync($"ip/dns/static/{id}");
}

public class WireGuardService(MikroTikClient client)
{
    public Task<List<WireGuardInterface>> GetInterfacesAsync() =>
        client.GetListAsync<WireGuardInterface>("interface/wireguard");

    public Task<WireGuardInterface?> CreateInterfaceAsync(WireGuardInterface wg) =>
        client.PutAsync<WireGuardInterface>("interface/wireguard", wg);

    public Task UpdateInterfaceAsync(string id, object changes) =>
        client.PatchAsync($"interface/wireguard/{id}", changes);

    public async Task DeleteInterfaceAsync(string id)
    {
        var iface = await client.GetSingleAsync<WireGuardInterface>($"interface/wireguard/{id}");
        if (iface != null)
        {
            var peers = await GetPeersAsync();
            foreach (var peer in peers.Where(p => p.Interface == iface.Name))
            {
                await DeletePeerAsync(peer.Id);
            }
        }
        await client.DeleteAsync($"interface/wireguard/{id}");
    }

    public Task<List<WireGuardPeer>> GetPeersAsync() =>
        client.GetListAsync<WireGuardPeer>("interface/wireguard/peers");

    public Task<WireGuardPeer?> AddPeerAsync(WireGuardPeer peer) =>
        client.PutAsync<WireGuardPeer>("interface/wireguard/peers", peer);

    public Task UpdatePeerAsync(string id, object changes) =>
        client.PatchAsync($"interface/wireguard/peers/{id}", changes);

    public Task DeletePeerAsync(string id) =>
        client.DeleteAsync($"interface/wireguard/peers/{id}");

    public async Task SetupFullVpnAsync(string name, string port, string mtu, string networkIp, string peerPubKey, string peerAllowedIp)
    {
        var wg = new WireGuardInterface
        {
            Name = name,
            ListenPort = port,
            Mtu = mtu,
            Comment = "VPN SDN Auto"
        };
        await client.PutAsync<WireGuardInterface>("interface/wireguard", wg);

        var ip = new IpAddress
        {
            Address = networkIp,
            Interface = name,
            Comment = "VPN Network (SDN Auto)"
        };
        await client.PutAsync<IpAddress>("ip/address", ip);

        var nat = new NatRule
        {
            Chain = "srcnat",
            Action = "masquerade",
            SrcAddress = networkIp,
            DstAddress = "0.0.0.0/0",
            Comment = "VPN Masquerade (SDN Auto)"
        };
        await client.PutAsync<NatRule>("ip/firewall/nat", nat);

        var peer = new WireGuardPeer
        {
            Interface = name,
            PublicKey = peerPubKey,
            AllowedAddress = peerAllowedIp,
            Comment = "VPN Client Auto"
        };
        await client.PutAsync<WireGuardPeer>("interface/wireguard/peers", peer);
    }

    public static string GenerateClientConfig(WireGuardPeer peer, string serverPublicKey,
        string serverEndpoint, string serverPort, string clientPrivateKey,
        string clientAddress, string dns = "8.8.8.8")
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