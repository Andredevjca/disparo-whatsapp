using System.Text;
using System.Text.Json;
using Dapper;
using DisparoApi.Data;
using DisparoApi.Dtos;
using DisparoApi.Helpers;
using DisparoApi.Models;
using DisparoApi.Options;
using DisparoApi.Repositories;
using Microsoft.Extensions.Options;

namespace DisparoApi.Services;

public class AtendimentoService : IAtendimentoService
{
    private readonly IAtendimentoRepository _repo;
    private readonly IEvolutionApiService _evolution;
    private readonly SincroniaMonitor _monitor;
    private readonly EvolutionOptions _evolutionOptions;
    private readonly IDbConnectionFactory _dbFactory;
    private readonly SemaphoreSlim _persistenciaLock = new(1, 1);

    public AtendimentoService(
        IAtendimentoRepository repo,
        IEvolutionApiService evolution,
        SincroniaMonitor monitor,
        IOptions<EvolutionOptions> evolutionOptions,
        IDbConnectionFactory dbFactory)
    {
        _repo = repo;
        _evolution = evolution;
        _monitor = monitor;
        _evolutionOptions = evolutionOptions.Value;
        _dbFactory = dbFactory;
        _ = RestaurarMonitorPersistidoAsync();
    }

    public async Task<ImagemEnvioDto?> ObterImagemMensagemAsync(int conversaId, int mensagemId, int usuarioId, string role)
    {
        if (await _repo.ObterConversaAsync(conversaId, usuarioId, role) == null) return null;
        return await _repo.ObterImagemMensagemAsync(conversaId, mensagemId);
    }

    public Task<ConversaPaginadaResponse> ListarConversasAsync(int usuarioId, string role, string? busca, int page, int perPage)
        => _repo.ListarConversasAsync(usuarioId, role, busca, page, perPage);

    public Task<ConversaDetalheResponse?> ObterConversaAsync(int id, int usuarioId, string role)
        => _repo.ObterConversaAsync(id, usuarioId, role);

    public async Task<MensagemPaginadaResponse> ListarMensagensAsync(int conversaId, int usuarioId, string role, int page, int perPage)
    {
        var conversa = await _repo.ObterConversaAsync(conversaId, usuarioId, role);
        if (conversa == null) throw new KeyNotFoundException("Conversa não encontrada");
        return await _repo.ListarMensagensAsync(conversaId, page, perPage);
    }

    public async Task<MensagemResponse> EnviarMensagemAtendimentoAsync(int usuarioId, int conversaId, string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            throw new ArgumentException("Informe a mensagem");

        var conversa = await _repo.ObterConversaAsync(conversaId, usuarioId, "admin");
        if (conversa == null) throw new KeyNotFoundException("Conversa não encontrada");

        var instancia = string.IsNullOrWhiteSpace(conversa.Conversa.Instancia)
            ? throw new ArgumentException("Conversa sem instância associada")
            : conversa.Conversa.Instancia;

        var telOk = conversa.Conversa.Telefone;
        if (string.IsNullOrWhiteSpace(telOk))
            throw new ArgumentException("Conversa sem telefone associado");

        var (ok, evolutionId, numeroOrigem, erro) = await _evolution.EnviarMensagemAsync(instancia, telOk, texto);
        var status = ok ? StatusMensagem.Enviada : StatusMensagem.Erro;

        var (inseriu, id) = await _repo.InserirMensagemSeNaoExistirAsync(
            conversaId: conversaId,
            usuarioId: usuarioId,
            envioDetalheId: null,
            evolutionId: evolutionId,
            telefone: telOk,
            instancia: instancia,
            tipo: TipoMensagem.Texto,
            direcao: DirecaoMensagem.Enviada,
            conteudo: texto,
            status: status,
            dataMensagem: DateTime.Now,
            erro: erro);

        var msgId = id ?? 0;
        await _repo.AtualizarConversaPosMensagemAsync(
            conversaId,
            ultimaMensagem: texto.Length > 300 ? texto.Substring(0, 300) : texto,
            ultimaMensagemEm: DateTime.Now,
            incrementoNaoLidas: 0);

        return new MensagemResponse
        {
            Id = msgId,
            ConversaId = conversaId,
            UsuarioId = usuarioId,
            EnvioDetalheId = null,
            EvolutionId = evolutionId,
            Telefone = telOk,
            Instancia = instancia,
            Tipo = TipoMensagem.Texto,
            Direcao = DirecaoMensagem.Enviada,
            Conteudo = texto,
            Status = status,
            DataMensagem = DateTime.Now,
            CreatedAt = DateTime.Now,
            Erro = ok ? null : erro
        };
    }

    public async Task MarcarComoLidaAsync(int usuarioId, string role, int conversaId)
    {
        var conversa = await _repo.ObterConversaAsync(conversaId, usuarioId, role);
        if (conversa == null) throw new KeyNotFoundException("Conversa não encontrada");
        await _repo.MarcarConversaLidaAsync(conversaId);
    }

    public Task<List<ContatoWhatsAppResponse>> ListarContatosAsync(int usuarioId, string role, string? busca)
        => _repo.ListarContatosAsync(usuarioId, role, busca);

    public async Task AssociarMensagemDisparoAsync(
        string instancia,
        string telefone,
        int? envioDetalheId,
        string? evolutionId,
        string direcao,
        string texto,
        string status,
        DateTime? dataMensagem,
        string? erro = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(instancia) || string.IsNullOrWhiteSpace(telefone)) return;

            var (telOk, telNormalizado, _) = TelefoneHelper.NormalizarTelefone(telefone);
            var telFinal = telOk ? telNormalizado : telefone;

            var contato = await _repo.CriarOuObterContatoAsync(instancia, telFinal);
            var conversa = await _repo.CriarOuObterConversaAsync(instancia, telFinal, contato.Id);

            var tipo = TipoMensagem.Texto;
            var data = dataMensagem ?? DateTime.Now;

            var (_, msgId) = await _repo.InserirMensagemSeNaoExistirAsync(
                conversaId: conversa.Id,
                usuarioId: null,
                envioDetalheId: envioDetalheId,
                evolutionId: evolutionId,
                telefone: telFinal,
                instancia: instancia,
                tipo: tipo,
                direcao: direcao,
                conteudo: texto,
                status: status,
                dataMensagem: data,
                erro: erro);

            if (envioDetalheId.HasValue && msgId.HasValue)
                await _repo.AtualizarEnvioDetalheMensagemIdAsync(envioDetalheId.Value, msgId.Value);

            if (!string.IsNullOrWhiteSpace(texto) && string.Equals(direcao, DirecaoMensagem.Enviada, StringComparison.OrdinalIgnoreCase))
            {
                await _repo.AtualizarConversaPosMensagemAsync(
                    conversa.Id,
                    ultimaMensagem: texto.Length > 300 ? texto.Substring(0, 300) : texto,
                    ultimaMensagemEm: data,
                    incrementoNaoLidas: 0);
            }
        }
        catch
        {
            // NÃO quebrar o fluxo de disparo por falha de associação com a conversa
        }
    }

    public async Task ProcessarWebhookEventoAsync(string instancia, JsonDocument payload, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(instancia))
            throw new ArgumentException("Instância não informada");

        var root = payload.RootElement;
        string? eventName = null;

        if (root.TryGetProperty("event", out var evProp)) eventName = evProp.GetString();
        else if (root.TryGetProperty("eventName", out var evnProp)) eventName = evnProp.GetString();

        if (string.IsNullOrWhiteSpace(eventName))
            eventName = InferirEvento(root);

        eventName = eventName?.Replace('_', '.').Replace('-', '.');

        logger.LogInformation("Webhook recebido. Instancia={Instancia} Evento={Evento}", instancia, eventName);

        if (string.Equals(eventName, "messages.upsert", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(eventName, "messages:upsert", StringComparison.OrdinalIgnoreCase))
        {
            await ProcessarMensagensUpsert(instancia, root, logger);
            return;
        }

        if (string.Equals(eventName, "messages.update", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(eventName, "messages:update", StringComparison.OrdinalIgnoreCase))
        {
            await ProcessarMensagensUpdate(instancia, root, logger);
            return;
        }

        if (string.Equals(eventName, "connection.update", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(eventName, "instance.connection.update", StringComparison.OrdinalIgnoreCase) ||
            eventName?.StartsWith("connection", StringComparison.OrdinalIgnoreCase) == true)
        {
            await ProcessarConnectionUpdate(instancia, root, logger);
            return;
        }

        if (string.Equals(eventName, "chats.upsert", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(eventName, "chats.update", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(eventName, "chats.set", StringComparison.OrdinalIgnoreCase) ||
            eventName?.StartsWith("chats", StringComparison.OrdinalIgnoreCase) == true)
        {
            await ProcessarChatsUpsert(instancia, root, logger);
            return;
        }

        if (string.Equals(eventName, "contacts.upsert", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(eventName, "contacts.update", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(eventName, "contacts.set", StringComparison.OrdinalIgnoreCase) ||
            eventName?.StartsWith("contacts", StringComparison.OrdinalIgnoreCase) == true)
        {
            await ProcessarContactsUpsert(instancia, root, logger);
            return;
        }

        // Fallback para mensagens em payloads onde o event é omitido
        if (eventName?.StartsWith("messages", StringComparison.OrdinalIgnoreCase) == true ||
            root.TryGetProperty("messages", out _) ||
            root.TryGetProperty("key", out _))
        {
            await ProcessarMensagensUpsert(instancia, root, logger);
            return;
        }

        logger.LogInformation("Evento ignorado pelo processador: {Evento}", eventName);
    }

    private static string? InferirEvento(JsonElement root)
    {
        if (EvolutionWebhookHelper.ExtrairMensagens(root).Count > 0) return "messages.upsert";
        if (root.TryGetProperty("messages", out var msgs) && msgs.ValueKind == JsonValueKind.Array) return "messages.upsert";
        if (root.TryGetProperty("key", out _) && root.TryGetProperty("update", out _)) return "messages.update";
        if (root.TryGetProperty("chats", out var chats) && chats.ValueKind == JsonValueKind.Array) return "chats.upsert";
        if (root.TryGetProperty("contacts", out var cts) && cts.ValueKind == JsonValueKind.Array) return "contacts.upsert";
        if (root.TryGetProperty("state", out _) ||
            root.TryGetProperty("connection", out _) ||
            root.TryGetProperty("instance", out _))
            return "connection.update";
        return null;
    }

    private async Task ProcessarConnectionUpdate(string instancia, JsonElement root, ILogger logger)
    {
        var state = ExtrairEstadoConexao(root);
        logger.LogInformation("Connection.update processado. Instancia={Instancia} Estado={State}", instancia, state ?? "null");
        var conectado = string.Equals(state, "open", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(state, "connected", StringComparison.OrdinalIgnoreCase);
        if (conectado && !_monitor.EstaRodando)
        {
            logger.LogInformation("Conexão estabelecida — disparando sincronização inicial (forcar=false). Instancia={Instancia}", instancia);
            _ = Task.Run(async () =>
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
                    await SincronizarHistoricoCompletoAsync(instancia, forcar: false, logger, cts.Token);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Falha na sincronização disparada por connection.update. Instancia={Instancia}", instancia);
                }
            }, CancellationToken.None);
        }
    }

    private static string? ExtrairEstadoConexao(JsonElement root)
    {
        var el = root;
        if (root.TryGetProperty("data", out var d)) el = d;
        if (el.TryGetProperty("state", out var st)) return st.GetString();
        if (el.TryGetProperty("connection", out var conn) && conn.TryGetProperty("state", out var st2)) return st2.GetString();
        if (el.TryGetProperty("instance", out var inst) && inst.ValueKind == JsonValueKind.Object)
        {
            if (inst.TryGetProperty("status", out var ist)) return ist.GetString();
            if (inst.TryGetProperty("connectionStatus", out var ics)) return ics.GetString();
        }
        if (root.TryGetProperty("status", out var rs)) return rs.GetString();
        return null;
    }

    private async Task ProcessarChatsUpsert(string instancia, JsonElement root, ILogger logger)
    {
        IEnumerable<JsonElement> enumeravel;
        if (root.TryGetProperty("data", out var data) && data.TryGetProperty("chats", out var dataChats) &&
            dataChats.ValueKind == JsonValueKind.Array)
            enumeravel = dataChats.EnumerateArray();
        else if (root.TryGetProperty("chats", out var chats) && chats.ValueKind == JsonValueKind.Array)
            enumeravel = chats.EnumerateArray();
        else
            enumeravel = new[] { root };

        var processados = 0;
        foreach (var it in enumeravel)
        {
            try
            {
                string? remoteJid = null;
                if (it.TryGetProperty("remoteJid", out var rj)) remoteJid = rj.GetString();
                else if (it.TryGetProperty("jid", out var jid)) remoteJid = jid.GetString();
                else if (it.TryGetProperty("chatId", out var ci)) remoteJid = ci.GetString();
                if (string.IsNullOrWhiteSpace(remoteJid)) continue;

                var tel = ExtrairTelefoneDoJid(remoteJid);
                if (string.IsNullOrWhiteSpace(tel)) continue;
                var (telOk, telNorm, _) = TelefoneHelper.NormalizarTelefone(tel);
                var telFinal = telOk ? telNorm : tel;

                DateTime? ultimaEm = null;
                string? ultimaTxt = null;
                if (it.TryGetProperty("lastMessageTime", out var lm) || it.TryGetProperty("conversationTimestamp", out lm))
                {
                    if (lm.ValueKind == JsonValueKind.Number && lm.TryGetInt64(out var unix))
                        ultimaEm = DateTimeOffset.FromUnixTimeSeconds(unix).LocalDateTime;
                    else if (DateTime.TryParse(lm.GetString(), out var dtr))
                        ultimaEm = dtr.ToLocalTime();
                }
                if (it.TryGetProperty("lastMessage", out var lmp))
                {
                    if (lmp.ValueKind == JsonValueKind.String) ultimaTxt = lmp.GetString();
                    else if (lmp.ValueKind == JsonValueKind.Object && lmp.TryGetProperty("message", out var inner))
                    {
                        if (inner.TryGetProperty("conversation", out var conv)) ultimaTxt = conv.GetString();
                        else if (inner.TryGetProperty("extendedTextMessage", out var etm) && etm.TryGetProperty("text", out var ett)) ultimaTxt = ett.GetString();
                        else if (inner.TryGetProperty("textMessage", out var tm) && tm.TryGetProperty("text", out var tmt)) ultimaTxt = tmt.GetString();
                    }
                }
                if (string.IsNullOrWhiteSpace(ultimaTxt) && it.TryGetProperty("conversation", out var convP))
                    ultimaTxt = convP.GetString();

                string? pushName = null;
                if (it.TryGetProperty("name", out var nm)) pushName = nm.GetString();
                else if (it.TryGetProperty("pushName", out var pn)) pushName = pn.GetString();

                var contato = await _repo.CriarOuObterContatoAsync(instancia, telFinal, nomeWhatsApp: pushName);
                var conversa = await _repo.CriarOuObterConversaAsync(instancia, telFinal, contato.Id);
                if (ultimaEm.HasValue || !string.IsNullOrWhiteSpace(ultimaTxt))
                {
                    var txtUlt = string.IsNullOrWhiteSpace(ultimaTxt) ? conversa.UltimaMensagem ?? "" : ultimaTxt;
                    if (txtUlt.Length > 300) txtUlt = txtUlt.Substring(0, 300);
                    await _repo.AtualizarConversaPosMensagemAsync(
                        conversa.Id,
                        txtUlt,
                        ultimaEm ?? conversa.UltimaMensagemEm ?? DateTime.Now,
                        incrementoNaoLidas: 0);
                }
                processados++;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Um chat falhou no chats.upsert.");
            }
        }
        logger.LogInformation("Webhook chats.upsert processado. Instancia={Instancia} ChatsProcessados={Total}", instancia, processados);
    }

    private async Task ProcessarContactsUpsert(string instancia, JsonElement root, ILogger logger)
    {
        IEnumerable<JsonElement> enumeravel;
        if (root.TryGetProperty("data", out var data) && data.TryGetProperty("contacts", out var dCt) &&
            dCt.ValueKind == JsonValueKind.Array)
            enumeravel = dCt.EnumerateArray();
        else if (root.TryGetProperty("contacts", out var cts) && cts.ValueKind == JsonValueKind.Array)
            enumeravel = cts.EnumerateArray();
        else
            enumeravel = new[] { root };

        var processados = 0;
        foreach (var it in enumeravel)
        {
            try
            {
                string? remoteJid = null;
                if (it.TryGetProperty("remoteJid", out var rj)) remoteJid = rj.GetString();
                else if (it.TryGetProperty("jid", out var jid)) remoteJid = jid.GetString();
                else if (it.TryGetProperty("id", out var idp)) remoteJid = idp.GetString();
                if (string.IsNullOrWhiteSpace(remoteJid)) continue;
                var tel = ExtrairTelefoneDoJid(remoteJid);
                if (string.IsNullOrWhiteSpace(tel)) continue;
                var (telOk, telNorm, _) = TelefoneHelper.NormalizarTelefone(tel);
                var telFinal = telOk ? telNorm : tel;

                string? pushName = null;
                if (it.TryGetProperty("pushName", out var pn)) pushName = pn.GetString();
                else if (it.TryGetProperty("notify", out var nt)) pushName = nt.GetString();
                else if (it.TryGetProperty("name", out var nm)) pushName = nm.GetString();

                string? foto = null;
                if (it.TryGetProperty("profilePictureUrl", out var pf)) foto = pf.GetString();
                else if (it.TryGetProperty("pictureUrl", out var pu)) foto = pu.GetString();

                await _repo.CriarOuObterContatoAsync(instancia, telFinal, nomeWhatsApp: pushName, fotoUrl: foto);
                processados++;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Um contato falhou no contacts.upsert.");
            }
        }
        logger.LogInformation("Webhook contacts.upsert processado. Instancia={Instancia} ContatosProcessados={Total}", instancia, processados);
    }

    private async Task ProcessarMensagensUpsert(string instancia, JsonElement root, ILogger logger)
    {
        var lista = EvolutionWebhookHelper.ExtrairMensagens(root);
        if (lista.Count == 0) {
            logger.LogWarning("Webhook sem mensagens reconhecidas. Instancia={Instancia}", instancia);
            return;
        }

        // Lotes grandes ou datas antigas → histórico
        var ehHistorico = lista.Count >= 10;

        foreach (var msg in lista)
        {
            if (!ehHistorico)
            {
                var (_, _, _, _, dm) = ParseMensagemBasica(msg);
                if (dm.HasValue && dm.Value < DateTime.Now.AddMinutes(-5))
                    ehHistorico = true;
            }
            await ProcessarUmaMensagemWebhook(instancia, msg, logger, ehHistorico);
        }
    }

    private async Task ProcessarUmaMensagemWebhook(string instancia, JsonElement msg, ILogger logger, bool ehHistorico = false)
    {
        var (evolutionId, remoteJid, fromMe, texto, dataMensagem) = ParseMensagemBasica(msg);

        if (string.IsNullOrWhiteSpace(evolutionId))
        {
            logger.LogWarning("Mensagem sem evolutionId (key.id) — ignorada");
            return;
        }

        var telefone = ExtrairTelefoneDoJid(remoteJid);
        if (string.IsNullOrWhiteSpace(telefone))
        {
            logger.LogWarning("Mensagem sem telefone remetente — ignorada. EvolutionId={EvolutionId}", evolutionId);
            return;
        }

        var (telOk, telNormalizado, _) = TelefoneHelper.NormalizarTelefone(telefone);
        var telFinal = telOk ? telNormalizado : telefone;

        var direcao = fromMe ? DirecaoMensagem.Enviada : DirecaoMensagem.Recebida;
        var nomeWhatsApp = ExtrairNomeRemetente(msg);
        var fotoUrl = default(string);
        var status = fromMe ? StatusMensagem.Enviada : StatusMensagem.Entregue;

        var contato = await _repo.CriarOuObterContatoAsync(instancia, telFinal, nomeWhatsApp, fotoUrl);

        if (!fromMe)
        {
            var match = await _repo.ObterContatoImportadoPorTelefoneAsync(0, telFinal);
            if (match.HasValue && string.IsNullOrWhiteSpace(contato.Nome) && !string.IsNullOrWhiteSpace(match.Value.nomeImportado))
            {
                // Atualiza contato se encontrado importado (ainda não existe coluna, então só loga)
                // Em versões futuras pode-se atualizar nome a partir daqui.
            }
        }

        var conversa = await _repo.CriarOuObterConversaAsync(instancia, telFinal, contato.Id);

        (bool inseriu, int? msgId) = await _repo.InserirMensagemSeNaoExistirAsync(
            conversaId: conversa.Id,
            usuarioId: null,
            envioDetalheId: null,
            evolutionId: evolutionId,
            telefone: telFinal,
            instancia: instancia,
            tipo: TipoMensagem.Texto,
            direcao: direcao,
            conteudo: texto,
            status: status,
            dataMensagem: dataMensagem);

        if (inseriu)
        {
            var incrementoNaoLidas = ehHistorico
                ? 0
                : (fromMe ? 0 : 1);

            await _repo.AtualizarConversaPosMensagemAsync(
                conversa.Id,
                ultimaMensagem: (string.IsNullOrWhiteSpace(texto) ? "[mensagem]" : texto).Length > 300
                    ? (string.IsNullOrWhiteSpace(texto) ? "[mensagem]" : texto).Substring(0, 300)
                    : (string.IsNullOrWhiteSpace(texto) ? "[mensagem]" : texto),
                ultimaMensagemEm: dataMensagem ?? DateTime.Now,
                incrementoNaoLidas: incrementoNaoLidas);

            logger.LogInformation(
                "Mensagem webhook processada. Instancia={Instancia} Telefone={Telefone} Direcao={Direcao} MsgId={MsgId} Historico={Historico}",
                instancia, telFinal, direcao, msgId, ehHistorico);
        }
        else
        {
            logger.LogInformation(
                "Mensagem duplicada ignorada (idempotência). Instancia={Instancia} EvolutionId={EvolutionId}",
                instancia, evolutionId);
        }
    }

    private async Task ProcessarMensagensUpdate(string instancia, JsonElement root, ILogger logger)
    {
        var enumeravel = EvolutionWebhookHelper.ExtrairMensagens(root);

        foreach (var msg in enumeravel)
        {
            var (evolutionId, _, _, _, _) = ParseMensagemBasica(msg);
            if (string.IsNullOrWhiteSpace(evolutionId)) continue;

            var novoStatus = ExtrairStatusDeUpdate(msg);
            if (!string.IsNullOrWhiteSpace(novoStatus))
            {
                await _repo.AtualizarStatusMensagemPorEvolutionIdAsync(instancia, evolutionId, novoStatus);
                logger.LogInformation(
                    "Status de mensagem atualizado via webhook. Instancia={Instancia} EvolutionId={EvolutionId} NovoStatus={NovoStatus}",
                    instancia, evolutionId, novoStatus);
            }
        }
    }

    private static (string? evolutionId, string? remoteJid, bool fromMe, string? texto, DateTime? dataMensagem) ParseMensagemBasica(JsonElement msg)
    {
        string? evolutionId = null;
        string? remoteJid = null;
        var fromMe = false;
        string? texto = null;
        DateTime? dataMensagem = null;

        if (msg.TryGetProperty("key", out var key))
        {
            if (key.TryGetProperty("id", out var idProp)) evolutionId = idProp.GetString();
            if (key.TryGetProperty("remoteJid", out var rj)) remoteJid = rj.GetString();
            if (key.TryGetProperty("fromMe", out var fm)) fromMe = fm.ValueKind == JsonValueKind.True;
            var alternativo = EvolutionWebhookHelper.Texto(key, "remoteJidAlt");
            if (remoteJid?.EndsWith("@lid") == true && alternativo?.EndsWith("@s.whatsapp.net") == true)
                remoteJid = alternativo;
        }

        if (msg.TryGetProperty("messageID", out var mid))
            evolutionId ??= mid.GetString();
        if (string.IsNullOrWhiteSpace(evolutionId) && msg.TryGetProperty("messageId", out var mid2))
            evolutionId ??= mid2.GetString();
        if (string.IsNullOrWhiteSpace(evolutionId) && msg.TryGetProperty("id", out var mid3))
            evolutionId ??= mid3.GetString();
        if (string.IsNullOrWhiteSpace(evolutionId) && msg.TryGetProperty("msgId", out var mid4))
            evolutionId ??= mid4.GetString();
        if (string.IsNullOrWhiteSpace(evolutionId) && msg.TryGetProperty("message_id", out var mid5))
            evolutionId ??= mid5.GetString();

        if (string.IsNullOrWhiteSpace(remoteJid) && msg.TryGetProperty("remoteJid", out var rj2))
            remoteJid = rj2.GetString();
        if (string.IsNullOrWhiteSpace(remoteJid) && msg.TryGetProperty("jid", out var rj3))
            remoteJid = rj3.GetString();

        JsonElement? messageProp = null;
        if (msg.TryGetProperty("message", out var mp)) messageProp = mp;
        else if (msg.TryGetProperty("messageType", out _) && msg.TryGetProperty("message", out var mp2)) messageProp = mp2;
        else if (msg.TryGetProperty("data", out var dt) && dt.TryGetProperty("message", out var mp3)) messageProp = mp3;

        if (messageProp.HasValue)
        {
            var m = messageProp.Value;
            texto = ExtrairTextoDeMessage(m);
            if (string.IsNullOrWhiteSpace(texto) && m.TryGetProperty("viewOnceMessageV2", out var vom) && vom.TryGetProperty("message", out var vomMsg))
                texto = ExtrairTextoDeMessage(vomMsg);
            if (string.IsNullOrWhiteSpace(texto) && m.TryGetProperty("viewOnceMessage", out var vom2) && vom2.TryGetProperty("message", out var vomMsg2))
                texto = ExtrairTextoDeMessage(vomMsg2);
        }

        if (string.IsNullOrWhiteSpace(texto) && msg.TryGetProperty("text", out var textProp))
            texto = textProp.GetString();
        if (string.IsNullOrWhiteSpace(texto) && msg.TryGetProperty("body", out var bodyProp))
            texto = bodyProp.GetString();
        if (string.IsNullOrWhiteSpace(texto) && msg.TryGetProperty("content", out var contentProp))
            texto = contentProp.GetString();
        if (string.IsNullOrWhiteSpace(texto) && msg.TryGetProperty("payload", out var payloadProp))
        {
            if (payloadProp.TryGetProperty("text", out var pt)) texto = pt.GetString();
            if (string.IsNullOrWhiteSpace(texto) && payloadProp.TryGetProperty("body", out var pb)) texto = pb.GetString();
        }
        if (string.IsNullOrWhiteSpace(texto) && msg.TryGetProperty("data", out var dataProp) && dataProp.TryGetProperty("text", out var dtxt))
            texto = dtxt.GetString();

        if (msg.TryGetProperty("messageTimestamp", out var ts))
        {
            if (ts.ValueKind == JsonValueKind.Number && ts.TryGetInt64(out var unix))
                dataMensagem = DateTimeOffset.FromUnixTimeSeconds(unix).LocalDateTime;
            else if (ts.GetString() is string s && long.TryParse(s, out var unixS))
                dataMensagem = DateTimeOffset.FromUnixTimeSeconds(unixS).LocalDateTime;
        }
        if (msg.TryGetProperty("timestamp", out var ts2))
        {
            if (!dataMensagem.HasValue && ts2.ValueKind == JsonValueKind.Number && ts2.TryGetInt64(out var unix))
                dataMensagem = DateTimeOffset.FromUnixTimeSeconds(unix).LocalDateTime;
            else if (!dataMensagem.HasValue && ts2.GetString() is string s2 && long.TryParse(s2, out var unixS2))
                dataMensagem = DateTimeOffset.FromUnixTimeSeconds(unixS2).LocalDateTime;
        }
        if (msg.TryGetProperty("date", out var dateProp) && !dataMensagem.HasValue)
        {
            if (dateProp.ValueKind == JsonValueKind.Number && dateProp.TryGetInt64(out var unixD))
                dataMensagem = DateTimeOffset.FromUnixTimeSeconds(unixD).LocalDateTime;
            else if (dateProp.GetString() is string sD && DateTime.TryParse(sD, out var dt))
                dataMensagem = dt.ToLocalTime();
        }

        dataMensagem ??= DateTime.Now;

        return (evolutionId, remoteJid, fromMe, texto, dataMensagem);
    }

    private static string? ExtrairTextoDeMessage(JsonElement m)
    {
        if (m.ValueKind != JsonValueKind.Object) return null;
        foreach (var wrapper in new[] { "ephemeralMessage", "viewOnceMessage", "viewOnceMessageV2", "documentWithCaptionMessage" })
            if (m.TryGetProperty(wrapper, out var wrapped) && wrapped.TryGetProperty("message", out var inner))
                return ExtrairTextoDeMessage(inner);
        if (m.TryGetProperty("conversation", out var conv) && !string.IsNullOrWhiteSpace(conv.GetString()))
            return conv.GetString();
        if (m.TryGetProperty("extendedTextMessage", out var etm) && etm.TryGetProperty("text", out var etmText) && !string.IsNullOrWhiteSpace(etmText.GetString()))
            return etmText.GetString();
        if (m.TryGetProperty("textMessage", out var tm) && tm.TryGetProperty("text", out var tmText) && !string.IsNullOrWhiteSpace(tmText.GetString()))
            return tmText.GetString();

        if (m.TryGetProperty("imageMessage", out var im) && im.TryGetProperty("caption", out var imCap) && !string.IsNullOrWhiteSpace(imCap.GetString()))
            return imCap.GetString();
        if (m.TryGetProperty("videoMessage", out var vm) && vm.TryGetProperty("caption", out var vmCap) && !string.IsNullOrWhiteSpace(vmCap.GetString()))
            return vmCap.GetString();
        if (m.TryGetProperty("documentMessage", out var dm) && dm.TryGetProperty("caption", out var dmCap) && !string.IsNullOrWhiteSpace(dmCap.GetString()))
            return dmCap.GetString();
        if (m.TryGetProperty("audioMessage", out _))
            return "[áudio]";
        if (m.TryGetProperty("voiceMessage", out _))
            return "[áudio]";
        if (m.TryGetProperty("stickerMessage", out _))
            return "[figurinha]";
        if (m.TryGetProperty("locationMessage", out _))
            return "[localização]";
        if (m.TryGetProperty("liveLocationMessage", out _))
            return "[localização ao vivo]";
        if (m.TryGetProperty("contactMessage", out _))
            return "[contato]";
        if (m.TryGetProperty("contactsArrayMessage", out _))
            return "[contatos]";

        if (m.TryGetProperty("buttonsResponseMessage", out var brm) && brm.TryGetProperty("selectedDisplayText", out var brmT) && !string.IsNullOrWhiteSpace(brmT.GetString()))
            return brmT.GetString();
        if (m.TryGetProperty("buttonsResponseMessage", out var brm2) && brm2.TryGetProperty("selectedButtonId", out var brmId) && !string.IsNullOrWhiteSpace(brmId.GetString()))
            return brmId.GetString();
        if (m.TryGetProperty("templateButtonReplyMessage", out var tbr) && tbr.TryGetProperty("selectedDisplayText", out var tbrT) && !string.IsNullOrWhiteSpace(tbrT.GetString()))
            return tbrT.GetString();
        if (m.TryGetProperty("templateButtonReplyMessage", out var tbr2) && tbr2.TryGetProperty("selectedId", out var tbrId) && !string.IsNullOrWhiteSpace(tbrId.GetString()))
            return tbrId.GetString();

        if (m.TryGetProperty("interactiveResponseMessage", out var irm))
        {
            if (irm.TryGetProperty("nativeFlowResponseMessage", out var nfrm) && nfrm.TryGetProperty("text", out var ntxt) && !string.IsNullOrWhiteSpace(ntxt.GetString()))
                return ntxt.GetString();
            if (irm.TryGetProperty("body", out var irmBody) && !string.IsNullOrWhiteSpace(irmBody.GetString()))
                return irmBody.GetString();
        }

        if (m.TryGetProperty("listResponseMessage", out var lrm) && lrm.TryGetProperty("title", out var lrmT) && !string.IsNullOrWhiteSpace(lrmT.GetString()))
            return lrmT.GetString();
        if (m.TryGetProperty("listResponseMessage", out var lrm2) && lrm2.TryGetProperty("singleSelectReply", out var ssr) && ssr.TryGetProperty("title", out var ssrT) && !string.IsNullOrWhiteSpace(ssrT.GetString()))
            return ssrT.GetString();

        if (m.TryGetProperty("contextInfo", out var ctx) && ctx.TryGetProperty("quotedMessage", out var quoted))
        {
            var q = ExtrairTextoDeMessage(quoted);
            if (!string.IsNullOrWhiteSpace(q))
                return q;
        }

        if (m.TryGetProperty("body", out var body) && !string.IsNullOrWhiteSpace(body.GetString()))
            return body.GetString();
        if (m.TryGetProperty("text", out var txt) && !string.IsNullOrWhiteSpace(txt.GetString()))
            return txt.GetString();

        return null;
    }

    private static string? ExtrairNomeRemetente(JsonElement msg)
    {
        if (msg.TryGetProperty("pushName", out var pn)) return pn.GetString();
        if (msg.TryGetProperty("notify", out var nt)) return nt.GetString();
        if (msg.TryGetProperty("sender", out var sender) && sender.TryGetProperty("pushName", out var spn))
            return spn.GetString();
        return null;
    }

    private static string? ExtrairStatusDeUpdate(JsonElement msg)
    {
        var value = msg;
        if (msg.TryGetProperty("update", out var update)) value = update;
        if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("status", out var status)) value = status;
        var raw = value.ValueKind == JsonValueKind.String ? value.GetString()?.ToLowerInvariant()
            : value.ValueKind == JsonValueKind.Number ? value.GetRawText() : null;
        return raw switch {
            "read" or "readed" or "lida" or "lido" or "4" or "5" => StatusMensagem.Lida,
            "delivered" or "delivery_ack" or "entregue" or "3" => StatusMensagem.Entregue,
            "sent" or "server_ack" or "enviada" or "enviado" or "2" => StatusMensagem.Enviada,
            "pending" or "pendente" or "1" => StatusMensagem.Pendente,
            "error" or "failed" or "erro" or "0" => StatusMensagem.Erro,
            _ => null
        };
    }

    private static string? ExtrairTelefoneDoJid(string? jid)
    {
        if (string.IsNullOrWhiteSpace(jid)) return null;
        var span = jid.AsSpan();
        var arroba = span.IndexOf('@');
        var parte = arroba >= 0 ? span.Slice(0, arroba) : span;
        if (parte.IsEmpty) return null;
        if (parte.StartsWith("+")) parte = parte.Slice(1);
        if (parte.IsEmpty) return null;
        var sb = new StringBuilder(parte.Length);
        foreach (var c in parte)
            if (char.IsDigit(c))
                sb.Append(c);
        return sb.Length == 0 ? null : sb.ToString();
    }

    public async Task<SincroniaStatusResponse> SincronizarHistoricoCompletoAsync(
        string instancia, bool forcar, ILogger logger, CancellationToken ct = default)
    {
        var statusAtual = _monitor.UltimoStatus;
        if (_monitor.EstaRodando)
            return new SincroniaStatusResponse
            {
                Status = "EM_ANDAMENTO",
                IniciadoEm = _monitor.IniciadoEm,
                TotalContatos = statusAtual.TotalContatos,
                TotalConversas = statusAtual.TotalConversas,
                TotalMensagens = statusAtual.TotalMensagens,
                Erro = "Sincronização já está em andamento."
            };

        if (!forcar && string.Equals(statusAtual.Status, "OK", StringComparison.OrdinalIgnoreCase)
            && statusAtual.FinalizadoEm.HasValue
            && (DateTime.Now - statusAtual.FinalizadoEm.Value).TotalHours < 48)
        {
            return statusAtual;
        }

        if (string.IsNullOrWhiteSpace(instancia))
            instancia = _evolutionOptions.Instance;
        if (string.IsNullOrWhiteSpace(instancia))
        {
            var err = "Nenhuma instância informada nem Evolution:Instance configurada em appsettings.";
            logger.LogError(err);
            return new SincroniaStatusResponse { Status = "ERRO", Erro = err };
        }

        _monitor.MarcarInicio();
        _ = PersistirMonitorAsync(safe: true);
        var totalContatos = 0;
        var totalConversas = 0;
        var totalMensagens = 0;
        var conversasComErro = 0;
        string? erroGlobal = null;

        try
        {
            logger.LogInformation("Iniciando sincronia de histórico Evolution. Instancia={Instancia} Forcar={Forcar}", instancia, forcar);

            // 1) Contatos
            try
            {
                var contatos = await _evolution.ListarContatosEvolutionAsync(instancia);
                ct.ThrowIfCancellationRequested();
                foreach (var (remoteJid, pushName, nome, fotoPerfil) in contatos)
                {
                    try
                    {
                        var tel = ExtrairTelefoneDoJid(remoteJid);
                        if (string.IsNullOrWhiteSpace(tel)) continue;
                        var (okTel, telNorm, _) = TelefoneHelper.NormalizarTelefone(tel);
                        var telFinal = okTel ? telNorm : tel;
                        var nomeFinal = string.IsNullOrWhiteSpace(nome) ? pushName : nome;
                        await _repo.CriarOuObterContatoAsync(instancia, telFinal, nomeFinal, fotoPerfil);
                        totalContatos++;
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Contato com falha no processamento: RemoteJid={Jid}", remoteJid);
                    }
                }
                logger.LogInformation("Sincronia contatos OK. Total={TotalContatos}", totalContatos);
                _monitor.AtualizarContadores(totalContatos, 0, 0);
                _ = PersistirMonitorAsync(safe: true);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro ao listar contatos Evolution");
                erroGlobal ??= "Falha ao listar contatos: " + ex.Message;
            }

            // 2) Conversas
            List<(string remoteJid, DateTime? ultimaMensagemEm, string? ultimaMensagemTexto, int? totalMensagens)>? conversas = null;
            try
            {
                conversas = (await _evolution.ListarConversasEvolutionAsync(instancia))
                    .Take(150).ToList();
                ct.ThrowIfCancellationRequested();
                totalConversas = conversas.Count;
                _monitor.AtualizarContadores(totalContatos, totalConversas, 0);
                _ = PersistirMonitorAsync(safe: true);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro ao listar conversas Evolution");
                erroGlobal ??= "Falha ao listar conversas: " + ex.Message;
            }

            // 3) Mensagens por conversa
            if (conversas != null && conversas.Count > 0)
            {
                var idx = 0;
                foreach (var conv in conversas)
                {
                    idx++;
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        var tel = ExtrairTelefoneDoJid(conv.remoteJid);
                        if (string.IsNullOrWhiteSpace(tel)) continue;
                        var (okTel, telNorm, _) = TelefoneHelper.NormalizarTelefone(tel);
                        var telFinal = okTel ? telNorm : tel;
                        var contato = await _repo.CriarOuObterContatoAsync(instancia, telFinal);
                        var conversaLocal = await _repo.CriarOuObterConversaAsync(instancia, telFinal, contato.Id);

                        // Atualiza ultima_mensagem da fonte Evolution
                        if (conv.ultimaMensagemEm.HasValue || !string.IsNullOrWhiteSpace(conv.ultimaMensagemTexto))
                        {
                            var txtUlt = string.IsNullOrWhiteSpace(conv.ultimaMensagemTexto) ? conversaLocal.UltimaMensagem ?? "" : conv.ultimaMensagemTexto;
                            if (txtUlt.Length > 300) txtUlt = txtUlt.Substring(0, 300);
                            await _repo.AtualizarConversaPosMensagemAsync(
                                conversaLocal.Id,
                                txtUlt,
                                conv.ultimaMensagemEm ?? conversaLocal.UltimaMensagemEm ?? DateTime.Now,
                                0);
                        }

                        var page = 1;
                        while (true)
                        {
                            ct.ThrowIfCancellationRequested();
                            var (msgsPagina, temMais) = await _evolution.ListarPaginaMensagensEvolutionAsync(instancia, conv.remoteJid, page, 100);
                            if (msgsPagina.Count == 0) break;
                            foreach (var msgElem in msgsPagina)
                            {
                                try
                                {
                                    var (evolutionId, _, fromMe, texto, dataMensagem) = ParseMensagemBasica(msgElem);
                                    if (string.IsNullOrWhiteSpace(evolutionId)) continue;
                                    var direcao = fromMe ? DirecaoMensagem.Enviada : DirecaoMensagem.Recebida;
                                    var status = fromMe ? StatusMensagem.Enviada : StatusMensagem.Entregue;

                                    var (inseriu, _) = await _repo.InserirMensagemSeNaoExistirAsync(
                                        conversaId: conversaLocal.Id,
                                        usuarioId: null,
                                        envioDetalheId: null,
                                        evolutionId: evolutionId,
                                        telefone: telFinal,
                                        instancia: instancia,
                                        tipo: TipoMensagem.Texto,
                                        direcao: direcao,
                                        conteudo: texto,
                                        status: status,
                                        dataMensagem: dataMensagem);

                                    if (!inseriu)
                                    {
                                        await _repo.AtualizarMensagemSeIncompletaAsync(
                                            instancia, evolutionId,
                                            novoConteudo: texto,
                                            direcao: direcao,
                                            status: status,
                                            dataMensagem: dataMensagem);
                                    }
                                    totalMensagens++;
                                }
                                catch (Exception mEx)
                                {
                                    logger.LogDebug(mEx, "Uma mensagem falhou ao processar");
                                }
                            }
                            _monitor.AtualizarContadores(totalContatos, totalConversas, totalMensagens, conversasComErro);
                            _ = PersistirMonitorAsync(safe: true);
                            if (!temMais || msgsPagina.Count < 100) break;
                            if (page >= 20) break; // segurança: 2000 msgs max por conversa
                            page++;
                        }
                    }
                    catch (Exception ex)
                    {
                        conversasComErro++;
                        logger.LogWarning(ex, "Conversa falhou. Jid={RemoteJid} Indice={Idx}/{Total}", conv.remoteJid, idx, conversas.Count);
                    }
                }
            }

            var okFinal = erroGlobal == null && conversasComErro < Math.Max(1, (totalConversas * 30) / 100);
            if (!okFinal && erroGlobal == null && conversasComErro > 0)
                erroGlobal = $"Concluído parcialmente — {conversasComErro} conversa(s) com falha.";
            _monitor.MarcarFim(okFinal, erroGlobal, conversasComErro);
            logger.LogInformation("Sincronia finalizada. OK={Ok} Contatos={TC} Conversas={TV} Msgs={TM} ErrosConv={ConvErro}",
                okFinal, totalContatos, totalConversas, totalMensagens, conversasComErro);
        }
        catch (OperationCanceledException)
        {
            _monitor.MarcarFim(false, "Sincronização cancelada.", conversasComErro);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro geral na sincronia Evolution");
            _monitor.MarcarFim(false, ex.Message, conversasComErro);
        }

        _ = PersistirMonitorAsync(safe: true);
        return _monitor.UltimoStatus;
    }

    private async Task RestaurarMonitorPersistidoAsync()
    {
        try
        {
            using var conn = _dbFactory.Create();
            var valor = await conn.QueryFirstOrDefaultAsync<string?>(
                "SELECT valor FROM configuracoes WHERE chave = @chave",
                new { chave = "sincronia_status_json" });
            if (string.IsNullOrWhiteSpace(valor)) return;
            using var doc = JsonDocument.Parse(valor);
            var root = doc.RootElement;
            var rsp = new SincroniaStatusResponse
            {
                Status = root.TryGetProperty("status", out var p1) ? p1.GetString() ?? "PENDENTE" : "PENDENTE",
                IniciadoEm = root.TryGetProperty("iniciadoEm", out var p2) && p2.TryGetDateTime(out var dt1) ? dt1 : null,
                FinalizadoEm = root.TryGetProperty("finalizadoEm", out var p3) && p3.TryGetDateTime(out var dt2) ? dt2 : null,
                TotalContatos = root.TryGetProperty("totalContatos", out var p4) && p4.TryGetInt32(out var v1) ? v1 : 0,
                TotalConversas = root.TryGetProperty("totalConversas", out var p5) && p5.TryGetInt32(out var v2) ? v2 : 0,
                TotalMensagens = root.TryGetProperty("totalMensagens", out var p6) && p6.TryGetInt32(out var v3) ? v3 : 0,
                ConversasComErro = root.TryGetProperty("conversasComErro", out var p7) && p7.TryGetInt32(out var v4) ? v4 : 0,
                Erro = root.TryGetProperty("erro", out var p8) ? p8.GetString() : null
            };
            _monitor.Restaurar(rsp);
        }
        catch
        {
            // Não quebrar inicialização por falha de restauração.
        }
    }

    private async Task PersistirMonitorAsync(bool safe = false)
    {
        try
        {
            var acquired = await _persistenciaLock.WaitAsync(TimeSpan.FromSeconds(3));
            if (!acquired) return;
            try
            {
                var json = JsonSerializer.Serialize(new
                {
                    status = _monitor.UltimoStatus.Status,
                    iniciadoEm = _monitor.UltimoStatus.IniciadoEm,
                    finalizadoEm = _monitor.UltimoStatus.FinalizadoEm,
                    totalContatos = _monitor.UltimoStatus.TotalContatos,
                    totalConversas = _monitor.UltimoStatus.TotalConversas,
                    totalMensagens = _monitor.UltimoStatus.TotalMensagens,
                    conversasComErro = _monitor.UltimoStatus.ConversasComErro,
                    erro = _monitor.UltimoStatus.Erro,
                    salvoEm = DateTime.Now
                }, new JsonSerializerOptions { WriteIndented = false });

                using var conn = _dbFactory.Create();
                await conn.ExecuteAsync(@"
                    INSERT INTO configuracoes (chave, valor) VALUES (@c, @v)
                    ON DUPLICATE KEY UPDATE valor = VALUES(valor)",
                    new { c = "sincronia_status_json", v = json });
            }
            finally
            {
                _persistenciaLock.Release();
            }
        }
        catch when (safe)
        {
            // ignora
        }
    }
}
