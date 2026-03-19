namespace MikroTikSDN.Api;

/// <summary>
/// Cliente HTTP para comunicar com a API REST do RouterOS MikroTik.
/// Documentação: https://help.mikrotik.com/docs/display/ROS/REST+API
/// </summary>
public class MikroTikClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;

    public string DeviceName { get; }
    public string Host { get; }

    public MikroTikClient(string host, string username, string password,
        string deviceName = "", bool useHttps = true, int port = 0)
    {
        Host = host;
        DeviceName = string.IsNullOrWhiteSpace(deviceName) ? host : deviceName;

        int actualPort = port != 0 ? port : (useHttps ? 443 : 80);
        string scheme = useHttps ? "https" : "http";
        _baseUrl = $"{scheme}://{host}:{actualPort}/rest";

        // MikroTik usa certificados self-signed → ignorar validação SSL
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };

        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };

        string creds = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", creds);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    // ─── GET ────────────────────────────────────────────────────────────────

    public async Task<List<T>> GetListAsync<T>(string endpoint)
    {
        var json = await RawGetAsync(endpoint);
        return JsonConvert.DeserializeObject<List<T>>(json) ?? new List<T>();
    }

    public async Task<T?> GetSingleAsync<T>(string endpoint)
    {
        var json = await RawGetAsync(endpoint);
        return JsonConvert.DeserializeObject<T>(json);
    }

    public async Task<string> RawGetAsync(string endpoint)
    {
        var resp = await _http.GetAsync($"{_baseUrl}/{endpoint}");
        await EnsureSuccessAsync(resp);
        return await resp.Content.ReadAsStringAsync();
    }

    // ─── POST ───────────────────────────────────────────────────────────────

    public async Task<T?> PostAsync<T>(string endpoint, object body)
    {
        var resp = await _http.PostAsync($"{_baseUrl}/{endpoint}", ToJson(body));
        await EnsureSuccessAsync(resp);
        var json = await resp.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<T>(json);
    }

    public async Task PostAsync(string endpoint, object body)
    {
        var resp = await _http.PostAsync($"{_baseUrl}/{endpoint}", ToJson(body));
        await EnsureSuccessAsync(resp);
    }

    // ─── PATCH (atualização parcial) ─────────────────────────────────────────

    public async Task<T?> PatchAsync<T>(string endpoint, object body)
    {
        var req = new HttpRequestMessage(new HttpMethod("PATCH"), $"{_baseUrl}/{endpoint}")
            { Content = ToJson(body) };
        var resp = await _http.SendAsync(req);
        await EnsureSuccessAsync(resp);
        var json = await resp.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<T>(json);
    }

    public async Task PatchAsync(string endpoint, object body)
    {
        var req = new HttpRequestMessage(new HttpMethod("PATCH"), $"{_baseUrl}/{endpoint}")
            { Content = ToJson(body) };
        var resp = await _http.SendAsync(req);
        await EnsureSuccessAsync(resp);
    }

    // ─── PUT (substituição completa) ─────────────────────────────────────────

    public async Task PutAsync(string endpoint, object body)
    {
        var resp = await _http.PutAsync($"{_baseUrl}/{endpoint}", ToJson(body));
        await EnsureSuccessAsync(resp);
    }

    // ─── DELETE ─────────────────────────────────────────────────────────────

    public async Task DeleteAsync(string endpoint)
    {
        var resp = await _http.DeleteAsync($"{_baseUrl}/{endpoint}");
        await EnsureSuccessAsync(resp);
    }

    // ─── Utilitários ────────────────────────────────────────────────────────

    /// <summary>Testa a ligação ao dispositivo.</summary>
    public async Task<(bool ok, string identity)> TestConnectionAsync()
    {
        try
        {
            var json = await RawGetAsync("system/identity");
            var obj = JObject.Parse(json);
            return (true, obj["name"]?.ToString() ?? Host);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static StringContent ToJson(object obj) =>
        new(JsonConvert.SerializeObject(obj, new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        }), Encoding.UTF8, "application/json");

    private static async Task EnsureSuccessAsync(HttpResponseMessage resp)
    {
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync();
            throw new MikroTikApiException((int)resp.StatusCode, body);
        }
    }

    public void Dispose() => _http.Dispose();
}

public class MikroTikApiException(int statusCode, string body)
    : Exception($"MikroTik API erro {statusCode}: {body}")
{
    public int StatusCode { get; } = statusCode;
    public string Body { get; } = body;
}
