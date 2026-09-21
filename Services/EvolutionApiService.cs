using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using DisparoApi.Models;
using DisparoApi.Options;

namespace DisparoApi.Services;

public class EvolutionApiService : IEvolutionApiService
{
    private readonly HttpClient _httpClient;
    private readonly EvolutionOptions _options;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public EvolutionApiService(HttpClient httpClient, IOptions<EvolutionOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
        var baseUrl = string.IsNullOrWhiteSpace(_options.Url) ? "http://localhost:8080/" : (_options.Url.EndsWith('/') ? _options.Url : _options.Url + "/");
        _httpClient.BaseAddress = new Uri(baseUrl);
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("apikey", _options.ApiKey);
        }
    }

    public async Task<List<WhatsAppAccount>> ListarContasAsync()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            return new List<WhatsAppAccount>();
        using var response = await _httpClient.GetAsync("instance/fetchInstances");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        var contas = new List<WhatsAppAccount>();
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                var conta = ParseAccount(item);
                if (conta != null)
                    contas.Add(conta);
            }
        }
        else if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("value", out var valueProp) && valueProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in valueProp.EnumerateArray())
                {
                    var conta = ParseAccount(item);
                    if (conta != null)
                        contas.Add(conta);
                }
            }
            else
            {
                var conta = ParseAccount(root);
                if (conta != null)
                    contas.Add(conta);
            }
        }

        return contas;
    }

    private static WhatsAppAccount? ParseAccount(JsonElement item)
    {
        string? instance = null;
        string? instanceName = null;

        if (item.TryGetProperty("name", out var nameProp))
            instanceName = nameProp.GetString();
        if (item.TryGetProperty("instanceName", out var inProp))
            instanceName ??= inProp.GetString();

        if (item.TryGetProperty("instance", out var instProp))
        {
            if (instProp.ValueKind == JsonValueKind.Object)
            {
                if (instProp.TryGetProperty("instanceName", out var subIn))
                    instance = subIn.GetString();
                if (instProp.TryGetProperty("name", out var subName))
                    instance ??= subName.GetString();
            }
            else if (instProp.ValueKind == JsonValueKind.String)
            {
                instance = instProp.GetString();
            }
        }
        instance ??= instanceName;

        if (string.IsNullOrWhiteSpace(instance))
            return null;

        var state = EstadoConexao.Unknown;
        var connected = false;
        string? number = null;
        string? profileName = null;
        string? qrcode = null;

        if (item.TryGetProperty("connectionStatus", out var connStatusProp))
        {
            var s = connStatusProp.GetString()?.ToLowerInvariant();
            state = s switch
            {
                "open" or "connected" => EstadoConexao.Open,
                "connecting" => EstadoConexao.Connecting,
                "close" or "disconnected" => EstadoConexao.Close,
                _ => EstadoConexao.Unknown
            };
            connected = s is "open" or "connected";
        }

        if (item.TryGetProperty("status", out var statusProp))
        {
            var s = statusProp.GetString()?.ToLowerInvariant();
            state = s switch
            {
                "open" or "connected" => EstadoConexao.Open,
                "connecting" => EstadoConexao.Connecting,
                "close" or "disconnected" => EstadoConexao.Close,
                _ => state
            };
            connected = connected || s is "open" or "connected";
        }

        if (item.TryGetProperty("instance", out var instObj) && instObj.ValueKind == JsonValueKind.Object)
        {
            if (instObj.TryGetProperty("status", out var instStatus))
            {
                var s = instStatus.GetString()?.ToLowerInvariant();
                state = s switch
                {
                    "open" or "connected" => EstadoConexao.Open,
                    "connecting" => EstadoConexao.Connecting,
                    "close" or "disconnected" => EstadoConexao.Close,
                    _ => state
                };
                connected = connected || s is "open" or "connected";
            }
            if (instObj.TryGetProperty("ownerJid", out var ownerJid))
                number = ownerJid.GetString();
            if (instObj.TryGetProperty("owner", out var owner))
                number ??= owner.GetString();
            if (instObj.TryGetProperty("profileName", out var pn))
                profileName = pn.GetString();
        }

        if (item.TryGetProperty("ownerJid", out var ownerJidProp))
            number ??= ownerJidProp.GetString();
        if (item.TryGetProperty("owner", out var ownerProp))
            number ??= ownerProp.GetString();
        if (item.TryGetProperty("profileName", out var profileProp))
            profileName ??= profileProp.GetString();

        if (item.TryGetProperty("qrcode", out var qrObj) && qrObj.ValueKind == JsonValueKind.Object)
        {
            if (qrObj.TryGetProperty("base64", out var base64Prop))
                qrcode = base64Prop.GetString();
            if (qrObj.TryGetProperty("pairingCode", out var pcQr))
                qrcode ??= pcQr.GetString();
        }

        if (string.IsNullOrWhiteSpace(qrcode) && item.TryGetProperty("base64", out var base64Direct))
            qrcode = base64Direct.GetString();
        if (string.IsNullOrWhiteSpace(qrcode) && item.TryGetProperty("qrCode", out var qr))
            qrcode = qr.GetString();
        if (string.IsNullOrWhiteSpace(qrcode) && item.TryGetProperty("qrcode", out var qr2) && qr2.ValueKind == JsonValueKind.String)
            qrcode = qr2.GetString();
        if (string.IsNullOrWhiteSpace(qrcode) && item.TryGetProperty("pairingCode", out var pc))
            qrcode = pc.GetString();

        return new WhatsAppAccount
        {
            Instance = instance,
            State = state,
            Connected = connected,
            Number = number,
            ProfileName = profileName,
            Qrcode = qrcode
        };
    }

    private async Task<WhatsAppAccount> ObterContaOuFallbackAsync(string instance)
    {
        try
        {
            var todas = await ListarContasAsync();
            var conta = todas.FirstOrDefault(c => string.Equals(c.Instance, instance, StringComparison.OrdinalIgnoreCase));
            if (conta != null) return conta;
        }
        catch
        {
        }
        return new WhatsAppAccount
        {
            Instance = instance,
            State = EstadoConexao.Unknown,
            Connected = false,
            Number = null,
            ProfileName = null,
            Qrcode = null
        };
    }

    public async Task<WhatsAppAccount> GarantirInstanciaEConectarAsync(string instance)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new InvalidOperationException("Evolution API Key não configurada. Edite appsettings.json: Evolution > ApiKey.");
        try
        {
            var body = new
            {
                instanceName = instance,
                token = _options.ApiKey,
                qrcode = true,
                integration = "WHATSAPP-BAILEYS",
                syncFullHistory = true,
                alwaysOnline = true
            };
            var json = JsonSerializer.Serialize(body);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync("instance/create", content);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Erro ao criar instância: {err}");
            }

            var respContent = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(respContent);
            var root = doc.RootElement;
            var parsed = ParseAccount(root);
            if (parsed != null && !string.IsNullOrWhiteSpace(parsed.Qrcode))
            {
                return parsed;
            }
        }
        catch (HttpRequestException)
        {
            throw;
        }
        return await ConectarAsync(instance);
    }

    public async Task<WhatsAppAccount> ConectarAsync(string instance)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new InvalidOperationException("Evolution API Key não configurada. Edite appsettings.json: Evolution > ApiKey.");
        using var response = await _httpClient.GetAsync($"instance/connect/{Uri.EscapeDataString(instance)}");
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Erro ao conectar: {err}");
        }
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;
        var parsed = ParseAccount(root);
        if (parsed != null)
        {
            parsed.Instance = instance;
            return parsed;
        }
        return await ObterContaOuFallbackAsync(instance);
    }

    public async Task DesconectarAsync(string instance)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new InvalidOperationException("Evolution API Key não configurada. Edite appsettings.json: Evolution > ApiKey.");
        using var response = await _httpClient.PostAsync($"instance/logout/{Uri.EscapeDataString(instance)}", null);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Erro ao desconectar: {err}");
        }
    }

    public async Task RemoverInstanciaAsync(string instance)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new InvalidOperationException("Evolution API Key não configurada. Edite appsettings.json: Evolution > ApiKey.");
        using var response = await _httpClient.DeleteAsync($"instance/delete/{Uri.EscapeDataString(instance)}");
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Erro ao remover instância: {err}");
        }
    }

    public async Task<(bool ok, string? evolutionId, string? numeroOrigem, string? erro)> EnviarMensagemAsync(string instance, string telefone, string mensagem, DisparoApi.Dtos.ImagemEnvioDto? imagem = null)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            return (false, null, null, "Evolution API Key não configurada. Edite appsettings.json: Evolution > ApiKey.");
        try
        {
            object body = imagem != null ? new
            {
                number = telefone, mediatype = "image", mimetype = imagem.MimeType,
                caption = mensagem, media = imagem.Base64, fileName = imagem.NomeArquivo
            } : new
            {
                number = telefone,
                text = mensagem,
                textMessage = new { text = mensagem },
                options = new { delay = 1200, presence = "composing" }
            };
            var json = JsonSerializer.Serialize(body);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync($"message/{(imagem == null ? "sendText" : "sendMedia")}/{Uri.EscapeDataString(instance)}", content);
            var raw = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return (false, null, null, $"Evolution respondeu {response.StatusCode}: {raw}");

            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            string? evolutionId = null;
            string? numeroOrigem = null;

            if (root.TryGetProperty("key", out var keyProp))
            {
                if (keyProp.TryGetProperty("id", out var idProp))
                    evolutionId = idProp.GetString();
                if (keyProp.TryGetProperty("remoteJid", out var remoteProp))
                    numeroOrigem = remoteProp.GetString();
            }
            if (string.IsNullOrWhiteSpace(evolutionId) && root.TryGetProperty("messageID", out var midProp))
                evolutionId = midProp.GetString();
            if (string.IsNullOrWhiteSpace(evolutionId) && root.TryGetProperty("message", out var msgProp) && msgProp.ValueKind == JsonValueKind.Object)
            {
                if (msgProp.TryGetProperty("id", out var msgId))
                    evolutionId = msgId.GetString();
            }

            return (true, evolutionId, numeroOrigem, null);
        }
        catch (Exception ex)
        {
            return (false, null, null, ex.Message);
        }
    }

    private async Task<string?> GetStringComRetryAsync(string endpoint)
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                using var response = await _httpClient.GetAsync(endpoint);
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    await Task.Delay(3000);
                    continue;
                }
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return null;
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch
            {
                if (attempt == 1) throw;
                await Task.Delay(1500);
            }
        }
        return null;
    }

    private async Task<string?> GetStringComFallbackAsync(string[] endpoints)
    {
        foreach (var endpoint in endpoints)
        {
            var res = await GetStringComRetryAsync(endpoint);
            if (!string.IsNullOrWhiteSpace(res)) return res;
        }
        return null;
    }

    private async Task<string?> PostStringComRetryAsync(string endpoint, object? body = null)
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                StringContent? content = null;
                if (body != null)
                {
                    var json = JsonSerializer.Serialize(body, JsonOptions);
                    content = new StringContent(json, Encoding.UTF8, "application/json");
                }

                using var response = await _httpClient.PostAsync(endpoint, content);
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    await Task.Delay(3000);
                    continue;
                }
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return null;
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch
            {
                if (attempt == 1) throw;
                await Task.Delay(1500);
            }
        }
        return null;
    }

    private async Task<string?> PostStringComFallbackAsync((string endpoint, object? body)[] variantes)
    {
        foreach (var (ep, body) in variantes)
        {
            var res = await PostStringComRetryAsync(ep, body);
            if (!string.IsNullOrWhiteSpace(res)) return res;
        }
        return null;
    }

    private static JsonElement RootOrData(JsonDocument doc)
    {
        if (doc.RootElement.TryGetProperty("data", out var d) && d.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            return d;
        return doc.RootElement;
    }

    public async Task<List<(string remoteJid, string? pushName, string? nome, string? fotoPerfil)>> ListarContatosEvolutionAsync(string instance)
    {
        var result = new List<(string remoteJid, string? pushName, string? nome, string? fotoPerfil)>();
        if (string.IsNullOrWhiteSpace(_options.ApiKey) || string.IsNullOrWhiteSpace(instance)) return result;

        var instEncoded = Uri.EscapeDataString(instance);
        var endpointsGet = new[]
        {
            $"chat/findAllContacts/{instEncoded}",
            $"chat/findAllContacts?instance={instEncoded}",
            $"contacts/findAllContacts/{instEncoded}",
            $"contacts/findAll?instance={instEncoded}",
        };
        var content = await GetStringComFallbackAsync(endpointsGet);
        if (string.IsNullOrWhiteSpace(content))
        {
            var variantesPost = new (string, object?)[]
            {
                ($"chat/findContacts/{instEncoded}", new { }),
                ($"chat/findContacts?instance={instEncoded}", new { }),
                ($"contacts/findAll/{instEncoded}", new { }),
                ($"chat/findAllContacts/{instEncoded}", new { }),
            };
            content = await PostStringComFallbackAsync(variantesPost);
        }
        if (string.IsNullOrWhiteSpace(content)) return result;

        using var doc = JsonDocument.Parse(content);
        var arr = RootOrData(doc);
        IEnumerable<JsonElement> items;
        if (arr.ValueKind == JsonValueKind.Array) items = arr.EnumerateArray();
        else if (arr.TryGetProperty("contacts", out var cts) && cts.ValueKind == JsonValueKind.Array) items = cts.EnumerateArray();
        else return result;

        foreach (var it in items)
        {
            string? remoteJid = null;
            if (it.TryGetProperty("remoteJid", out var rj)) remoteJid = rj.GetString();
            else if (it.TryGetProperty("jid", out var jid)) remoteJid = jid.GetString();
            else if (it.TryGetProperty("id", out var id)) remoteJid = id.GetString();
            if (string.IsNullOrWhiteSpace(remoteJid)) continue;

            string? pushName = null;
            if (it.TryGetProperty("pushName", out var pn)) pushName = pn.GetString();
            else if (it.TryGetProperty("notify", out var nt)) pushName = nt.GetString();
            else if (it.TryGetProperty("name", out var nm)) pushName = nm.GetString();

            string? nome = null;
            if (it.TryGetProperty("verifiedName", out var vn)) nome = vn.GetString();
            else if (it.TryGetProperty("bizName", out var bz)) nome = bz.GetString();
            else if (it.TryGetProperty("profileName", out var pfn)) nome = pfn.GetString();

            string? fotoPerfil = null;
            if (it.TryGetProperty("profilePictureUrl", out var pf)) fotoPerfil = pf.GetString();
            else if (it.TryGetProperty("pictureUrl", out var pu)) fotoPerfil = pu.GetString();
            else if (it.TryGetProperty("imgUrl", out var iu)) fotoPerfil = iu.GetString();

            result.Add((remoteJid, pushName, nome, fotoPerfil));
        }
        return result;
    }

    public async Task<List<(string remoteJid, DateTime? ultimaMensagemEm, string? ultimaMensagemTexto, int? totalMensagens)>> ListarConversasEvolutionAsync(string instance)
    {
        var result = new List<(string remoteJid, DateTime? ultimaMensagemEm, string? ultimaMensagemTexto, int? totalMensagens)>();
        if (string.IsNullOrWhiteSpace(_options.ApiKey) || string.IsNullOrWhiteSpace(instance)) return result;

        var instEncoded = Uri.EscapeDataString(instance);
        var endpointsGet = new[]
        {
            $"chat/findAllChats/{instEncoded}",
            $"chat/findAllChats?instance={instEncoded}",
            $"chats/findAll/{instEncoded}",
            $"chats/findAll?instance={instEncoded}",
        };
        var content = await GetStringComFallbackAsync(endpointsGet);
        if (string.IsNullOrWhiteSpace(content))
        {
            var variantesPost = new (string, object?)[]
            {
                ($"chat/findChats/{instEncoded}", new { }),
                ($"chat/findChats?instance={instEncoded}", new { }),
                ($"chats/findAll/{instEncoded}", new { }),
            };
            content = await PostStringComFallbackAsync(variantesPost);
        }
        if (string.IsNullOrWhiteSpace(content)) return result;

        using var doc = JsonDocument.Parse(content);
        var arr = RootOrData(doc);
        IEnumerable<JsonElement> items;
        if (arr.ValueKind == JsonValueKind.Array) items = arr.EnumerateArray();
        else if (arr.TryGetProperty("chats", out var cts) && cts.ValueKind == JsonValueKind.Array) items = cts.EnumerateArray();
        else return result;

        foreach (var it in items)
        {
            string? remoteJid = null;
            if (it.TryGetProperty("remoteJid", out var rj)) remoteJid = rj.GetString();
            else if (it.TryGetProperty("jid", out var jid)) remoteJid = jid.GetString();
            else if (it.TryGetProperty("chatId", out var ci)) remoteJid = ci.GetString();
            if (string.IsNullOrWhiteSpace(remoteJid)) continue;
            if (remoteJid.EndsWith("@g.us", StringComparison.OrdinalIgnoreCase) ||
                remoteJid.EndsWith("@newsletter", StringComparison.OrdinalIgnoreCase) ||
                remoteJid.EndsWith("@broadcast", StringComparison.OrdinalIgnoreCase) ||
                remoteJid.EndsWith("@c.us", StringComparison.OrdinalIgnoreCase) == false && !remoteJid.Contains('@'))
            {
                if (remoteJid.Contains('@') && !remoteJid.EndsWith("@s.whatsapp.net", StringComparison.OrdinalIgnoreCase)) continue;
            }

            DateTime? ultimaMensagemEm = null;
            if (it.TryGetProperty("lastMessageTime", out var lm) || it.TryGetProperty("conversationTimestamp", out lm) || it.TryGetProperty("modifiedAt", out lm))
            {
                if (lm.ValueKind == JsonValueKind.Number && lm.TryGetInt64(out var unix))
                    ultimaMensagemEm = DateTimeOffset.FromUnixTimeSeconds(unix).LocalDateTime;
                else if (lm.GetString() is string s && long.TryParse(s, out var unixS))
                    ultimaMensagemEm = DateTimeOffset.FromUnixTimeSeconds(unixS).LocalDateTime;
                else if (DateTime.TryParse(lm.GetString(), out var dtr))
                    ultimaMensagemEm = dtr.ToLocalTime();
            }

            string? ultimaMensagemTexto = null;
            if (it.TryGetProperty("lastMessage", out var lmp))
            {
                if (lmp.ValueKind == JsonValueKind.String) ultimaMensagemTexto = lmp.GetString();
                else if (lmp.ValueKind == JsonValueKind.Object)
                {
                    if (lmp.TryGetProperty("message", out var innerMsg))
                    {
                        var tempConv = string.Empty;
                        if (innerMsg.TryGetProperty("conversation", out var conv)) tempConv = conv.GetString();
                        else if (innerMsg.TryGetProperty("extendedTextMessage", out var etm) && etm.TryGetProperty("text", out var ett)) tempConv = ett.GetString();
                        else if (innerMsg.TryGetProperty("textMessage", out var tm) && tm.TryGetProperty("text", out var tmt)) tempConv = tmt.GetString();
                        if (!string.IsNullOrWhiteSpace(tempConv)) ultimaMensagemTexto = tempConv;
                    }
                    if (string.IsNullOrWhiteSpace(ultimaMensagemTexto) && lmp.TryGetProperty("text", out var txtProp))
                        ultimaMensagemTexto = txtProp.GetString();
                }
            }
            if (string.IsNullOrWhiteSpace(ultimaMensagemTexto) && it.TryGetProperty("conversation", out var convProp))
                ultimaMensagemTexto = convProp.GetString();

            int? totalMensagens = null;
            if (it.TryGetProperty("count", out var cnt) && cnt.TryGetInt32(out var cntV)) totalMensagens = cntV;
            else if (it.TryGetProperty("totalMessages", out var tmc) && tmc.TryGetInt32(out var tmv)) totalMensagens = tmv;
            else if (it.TryGetProperty("unreadCount", out _)) { }

            result.Add((remoteJid, ultimaMensagemEm, ultimaMensagemTexto, totalMensagens));
        }
        return result;
    }

    public async Task<(List<JsonElement> mensagens, bool temMais)> ListarPaginaMensagensEvolutionAsync(string instance, string remoteJid, int page, int perPage = 100)
    {
        var list = new List<JsonElement>();
        if (string.IsNullOrWhiteSpace(_options.ApiKey) || string.IsNullOrWhiteSpace(instance) || string.IsNullOrWhiteSpace(remoteJid))
            return (list, false);

        var instEncoded = Uri.EscapeDataString(instance);
        var jidEncoded = Uri.EscapeDataString(remoteJid);
        var endpointsGet = new[]
        {
            $"chat/findMessages/{instEncoded}/{jidEncoded}?page={page}&perPage={perPage}",
            $"chat/findMessages/{jidEncoded}?instance={instEncoded}&page={page}&perPage={perPage}",
            $"message/list/{instEncoded}/{jidEncoded}?page={page}&perPage={perPage}",
            $"messages/find/{instEncoded}?remoteJid={jidEncoded}&page={page}&perPage={perPage}",
        };
        var content = await GetStringComFallbackAsync(endpointsGet);
        if (string.IsNullOrWhiteSpace(content))
        {
            var variantesPost = new (string, object?)[]
            {
                ($"chat/findMessages/{instEncoded}", new { where = new { key = new { remoteJid } }, page, perPage }),
                ($"chat/findMessages?instance={instEncoded}", new { where = new { key = new { remoteJid } }, page, perPage }),
                ($"messages/find/{instEncoded}", new { remoteJid, page, perPage }),
            };
            content = await PostStringComFallbackAsync(variantesPost);
        }
        if (string.IsNullOrWhiteSpace(content)) return (list, false);

        using var doc = JsonDocument.Parse(content);
        var root = RootOrData(doc);
        IEnumerable<JsonElement> items;
        if (root.ValueKind == JsonValueKind.Array) items = root.EnumerateArray();
        else if (root.TryGetProperty("messages", out var msgs) && msgs.ValueKind == JsonValueKind.Array) items = msgs.EnumerateArray();
        else if (root.TryGetProperty("rows", out var rows) && rows.ValueKind == JsonValueKind.Array) items = rows.EnumerateArray();
        else return (list, false);

        foreach (var it in items) list.Add(it.Clone());
        var temMais = list.Count >= perPage;
        return (list, temMais);
    }
}
