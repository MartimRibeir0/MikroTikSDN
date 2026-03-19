namespace MikroTikSDN.Models;

// ════════════════════════════════════════════════════════════════════
//  INTERFACES
// ════════════════════════════════════════════════════════════════════

public class MikrotikInterface
{
    [JsonProperty(".id")]       public string Id { get; set; } = "";
    [JsonProperty("name")]      public string Name { get; set; } = "";
    [JsonProperty("type")]      public string Type { get; set; } = "";
    [JsonProperty("mtu")]       public string Mtu { get; set; } = "";
    [JsonProperty("running")]   public bool Running { get; set; }
    [JsonProperty("disabled")]  public bool Disabled { get; set; }
    [JsonProperty("comment")]   public string Comment { get; set; } = "";
    [JsonProperty("mac-address")] public string MacAddress { get; set; } = "";
}

// ════════════════════════════════════════════════════════════════════
//  WIRELESS
// ════════════════════════════════════════════════════════════════════

public class WirelessInterface
{
    [JsonProperty(".id")]               public string Id { get; set; } = "";
    [JsonProperty("name")]              public string Name { get; set; } = "";
    [JsonProperty("ssid")]              public string Ssid { get; set; } = "";
    [JsonProperty("mode")]              public string Mode { get; set; } = "ap-bridge";
    [JsonProperty("band")]              public string Band { get; set; } = "";
    [JsonProperty("frequency")]         public string Frequency { get; set; } = "";
    [JsonProperty("channel-width")]     public string ChannelWidth { get; set; } = "";
    [JsonProperty("security-profile")]  public string SecurityProfile { get; set; } = "";
    [JsonProperty("disabled")]          public bool Disabled { get; set; }
    [JsonProperty("running")]           public bool Running { get; set; }
    [JsonProperty("mac-address")]       public string MacAddress { get; set; } = "";
    [JsonProperty("comment")]           public string Comment { get; set; } = "";
}

public class SecurityProfile
{
    [JsonProperty(".id")]                   public string Id { get; set; } = "";
    [JsonProperty("name")]                  public string Name { get; set; } = "";
    [JsonProperty("mode")]                  public string Mode { get; set; } = "dynamic-keys";
    [JsonProperty("authentication-types")]  public string AuthenticationTypes { get; set; } = "wpa2-psk";
    [JsonProperty("unicast-ciphers")]       public string UnicastCiphers { get; set; } = "aes-ccm";
    [JsonProperty("group-ciphers")]         public string GroupCiphers { get; set; } = "aes-ccm";
    [JsonProperty("wpa2-pre-shared-key")]   public string Wpa2PreSharedKey { get; set; } = "";
    [JsonProperty("comment")]               public string Comment { get; set; } = "";
}

// ════════════════════════════════════════════════════════════════════
//  BRIDGE
// ════════════════════════════════════════════════════════════════════

public class BridgeInterface
{
    [JsonProperty(".id")]               public string Id { get; set; } = "";
    [JsonProperty("name")]              public string Name { get; set; } = "";
    [JsonProperty("mtu")]               public string Mtu { get; set; } = "auto";
    [JsonProperty("vlan-filtering")]    public bool VlanFiltering { get; set; }
    [JsonProperty("disabled")]          public bool Disabled { get; set; }
    [JsonProperty("running")]           public bool Running { get; set; }
    [JsonProperty("comment")]           public string Comment { get; set; } = "";
    [JsonProperty("mac-address")]       public string MacAddress { get; set; } = "";
}

public class BridgePort
{
    [JsonProperty(".id")]       public string Id { get; set; } = "";
    [JsonProperty("interface")] public string Interface { get; set; } = "";
    [JsonProperty("bridge")]    public string Bridge { get; set; } = "";
    [JsonProperty("disabled")]  public bool Disabled { get; set; }
    [JsonProperty("comment")]   public string Comment { get; set; } = "";
}

// ════════════════════════════════════════════════════════════════════
//  IP / ROTAS
// ════════════════════════════════════════════════════════════════════

public class IpAddress
{
    [JsonProperty(".id")]           public string Id { get; set; } = "";
    [JsonProperty("address")]       public string Address { get; set; } = "";   // ex: 192.168.1.1/24
    [JsonProperty("network")]       public string Network { get; set; } = "";
    [JsonProperty("interface")]     public string Interface { get; set; } = "";
    [JsonProperty("disabled")]      public bool Disabled { get; set; }
    [JsonProperty("dynamic")]       public bool Dynamic { get; set; }
    [JsonProperty("comment")]       public string Comment { get; set; } = "";
}

public class StaticRoute
{
    [JsonProperty(".id")]           public string Id { get; set; } = "";
    [JsonProperty("dst-address")]   public string DstAddress { get; set; } = "";
    [JsonProperty("gateway")]       public string Gateway { get; set; } = "";
    [JsonProperty("distance")]      public string Distance { get; set; } = "1";
    [JsonProperty("disabled")]      public bool Disabled { get; set; }
    [JsonProperty("active")]        public bool Active { get; set; }
    [JsonProperty("dynamic")]       public bool Dynamic { get; set; }
    [JsonProperty("comment")]       public string Comment { get; set; } = "";
}

// ════════════════════════════════════════════════════════════════════
//  DHCP
// ════════════════════════════════════════════════════════════════════

public class DhcpServer
{
    [JsonProperty(".id")]               public string Id { get; set; } = "";
    [JsonProperty("name")]              public string Name { get; set; } = "";
    [JsonProperty("interface")]         public string Interface { get; set; } = "";
    [JsonProperty("address-pool")]      public string AddressPool { get; set; } = "";
    [JsonProperty("lease-time")]        public string LeaseTime { get; set; } = "10m";
    [JsonProperty("disabled")]          public bool Disabled { get; set; }
    [JsonProperty("authoritative")]     public string Authoritative { get; set; } = "yes";
    [JsonProperty("comment")]           public string Comment { get; set; } = "";
}

public class DhcpPool
{
    [JsonProperty(".id")]       public string Id { get; set; } = "";
    [JsonProperty("name")]      public string Name { get; set; } = "";
    [JsonProperty("ranges")]    public string Ranges { get; set; } = "";   // ex: 192.168.1.100-192.168.1.200
    [JsonProperty("comment")]   public string Comment { get; set; } = "";
}

public class DhcpNetwork
{
    [JsonProperty(".id")]       public string Id { get; set; } = "";
    [JsonProperty("address")]   public string Address { get; set; } = "";
    [JsonProperty("gateway")]   public string Gateway { get; set; } = "";
    [JsonProperty("dns-server")] public string DnsServer { get; set; } = "";
    [JsonProperty("comment")]   public string Comment { get; set; } = "";
}

// ════════════════════════════════════════════════════════════════════
//  DNS
// ════════════════════════════════════════════════════════════════════

public class DnsSettings
{
    [JsonProperty("servers")]               public string Servers { get; set; } = "";
    [JsonProperty("allow-remote-requests")] public bool AllowRemoteRequests { get; set; }
    [JsonProperty("max-udp-packet-size")]   public string MaxUdpPacketSize { get; set; } = "4096";
    [JsonProperty("cache-size")]            public string CacheSize { get; set; } = "2048KiB";
    [JsonProperty("cache-used")]            public string CacheUsed { get; set; } = "";
}

// ════════════════════════════════════════════════════════════════════
//  WIREGUARD (2ª Parte)
// ════════════════════════════════════════════════════════════════════

public class WireGuardInterface
{
    [JsonProperty(".id")]           public string Id { get; set; } = "";
    [JsonProperty("name")]          public string Name { get; set; } = "";
    [JsonProperty("listen-port")]   public string ListenPort { get; set; } = "13231";
    [JsonProperty("private-key")]   public string PrivateKey { get; set; } = "";
    [JsonProperty("public-key")]    public string PublicKey { get; set; } = "";
    [JsonProperty("mtu")]           public string Mtu { get; set; } = "1420";
    [JsonProperty("disabled")]      public bool Disabled { get; set; }
    [JsonProperty("running")]       public bool Running { get; set; }
    [JsonProperty("comment")]       public string Comment { get; set; } = "";
}

public class WireGuardPeer
{
    [JsonProperty(".id")]               public string Id { get; set; } = "";
    [JsonProperty("interface")]         public string Interface { get; set; } = "";
    [JsonProperty("public-key")]        public string PublicKey { get; set; } = "";
    [JsonProperty("allowed-address")]   public string AllowedAddress { get; set; } = "";
    [JsonProperty("endpoint-address")]  public string EndpointAddress { get; set; } = "";
    [JsonProperty("endpoint-port")]     public string EndpointPort { get; set; } = "";
    [JsonProperty("persistent-keepalive")] public string PersistentKeepalive { get; set; } = "";
    [JsonProperty("comment")]           public string Comment { get; set; } = "";
    [JsonProperty("disabled")]          public bool Disabled { get; set; }
    [JsonProperty("rx")]                public string Rx { get; set; } = "0";
    [JsonProperty("tx")]                public string Tx { get; set; } = "0";
}
