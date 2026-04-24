using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace MikroTikSDN.Api;

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

        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };

        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };

        string creds = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", creds);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

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

    public async Task PostAsync(string endpoint, object body)
    {
        var content = ToJson(body);
        var sentBody = await content.ReadAsStringAsync();
        var resp = await _http.PostAsync($"{_baseUrl}/{endpoint}", content);
        await EnsureSuccessAsync(resp, sentBody);
    }

    public async Task<T?> PostAsync<T>(string endpoint, object body)
    {
        var content = ToJson(body);
        var sentBody = await content.ReadAsStringAsync();
        var resp = await _http.PostAsync($"{_baseUrl}/{endpoint}", content);
        await EnsureSuccessAsync(resp, sentBody);
        var json = await resp.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<T>(json);
    }

    public async Task PatchAsync(string endpoint, object body)
    {
        var content = ToJson(body);
        var sentBody = await content.ReadAsStringAsync();
        var req = new HttpRequestMessage(new HttpMethod("PATCH"), $"{_baseUrl}/{endpoint}")
        { Content = content };
        var resp = await _http.SendAsync(req);
        await EnsureSuccessAsync(resp, sentBody);
    }

    public async Task<T?> PutAsync<T>(string endpoint, object body)
    {
        var content = ToJson(body);
        var sentBody = await content.ReadAsStringAsync();
        var resp = await _http.PutAsync($"{_baseUrl}/{endpoint}", content);
        await EnsureSuccessAsync(resp, sentBody);
        var json = await resp.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<T>(json);
    }

    public async Task DeleteAsync(string endpoint)
    {
        var resp = await _http.DeleteAsync($"{_baseUrl}/{endpoint}");
        await EnsureSuccessAsync(resp);
    }

    // ADICIONADO: Método para reiniciar o dispositivo
    public async Task RebootAsync()
    {
        await PostAsync("system/reboot", new { });
    }

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

    private static StringContent ToJson(object obj)
    {
        var settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore,
            ContractResolver = new SkipEmptyStringsResolver()
        };
        return new StringContent(
            JsonConvert.SerializeObject(obj, settings),
            Encoding.UTF8, "application/json");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage resp, string sentBody = "")
    {
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync();
            var url = resp.RequestMessage?.RequestUri?.ToString() ?? "URL desconhecida";
            var method = resp.RequestMessage?.Method.ToString() ?? "";
            throw new MikroTikApiException((int)resp.StatusCode,
                $"\nMétodo: {method}\nURL: {url}\nEnviado: {sentBody}\nResposta: {body}");
        }
    }

    public void Dispose() => _http.Dispose();
}

public class SkipEmptyStringsResolver : DefaultContractResolver
{
    protected override JsonProperty CreateProperty(System.Reflection.MemberInfo member, MemberSerialization memberSerialization)
    {
        var prop = base.CreateProperty(member, memberSerialization);
        if (prop.PropertyType == typeof(string))
        {
            var originalShouldSerialize = prop.ShouldSerialize;
            prop.ShouldSerialize = instance =>
            {
                if (originalShouldSerialize != null && !originalShouldSerialize(instance)) return false;
                var val = prop.ValueProvider?.GetValue(instance) as string;
                return !string.IsNullOrEmpty(val);
            };
        }
        return prop;
    }
}

public class MikroTikApiException(int statusCode, string body)
    : Exception($"MikroTik API erro {statusCode}: {body}")
{
    public int StatusCode { get; } = statusCode;
    public string Body { get; } = body;
}