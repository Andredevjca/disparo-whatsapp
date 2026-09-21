using System.Text.Json;
using DisparoApi.Dtos;

namespace DisparoApi.Services;

public interface IAtendimentoService
{
    Task<ImagemEnvioDto?> ObterImagemMensagemAsync(int conversaId, int mensagemId, int usuarioId, string role);
    Task<ConversaPaginadaResponse> ListarConversasAsync(int usuarioId, string role, string? busca, int page, int perPage);

    Task<ConversaDetalheResponse?> ObterConversaAsync(int id, int usuarioId, string role);

    Task<MensagemPaginadaResponse> ListarMensagensAsync(int conversaId, int usuarioId, string role, int page, int perPage);

    Task<MensagemResponse> EnviarMensagemAtendimentoAsync(int usuarioId, int conversaId, string texto);

    Task MarcarComoLidaAsync(int usuarioId, string role, int conversaId);

    Task<List<ContatoWhatsAppResponse>> ListarContatosAsync(int usuarioId, string role, string? busca);

    Task AssociarMensagemDisparoAsync(
        string instancia,
        string telefone,
        int? envioDetalheId,
        string? evolutionId,
        string direcao,
        string texto,
        string status,
        DateTime? dataMensagem,
        string? erro = null);

    Task ProcessarWebhookEventoAsync(string instancia, JsonDocument payload, ILogger logger);

    Task<SincroniaStatusResponse> SincronizarHistoricoCompletoAsync(string instancia, bool forcar, ILogger logger, CancellationToken ct = default);
}
