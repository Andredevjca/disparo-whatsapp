using DisparoApi.Dtos;

namespace DisparoApi.Repositories;

public interface IAtendimentoRepository
{
    Task<ImagemEnvioDto?> ObterImagemMensagemAsync(int conversaId, int mensagemId);
    Task<ConversaPaginadaResponse> ListarConversasAsync(int usuarioId, string role, string? busca, int page, int perPage);

    Task<ConversaDetalheResponse?> ObterConversaAsync(int id, int usuarioId, string role);

    Task<MensagemPaginadaResponse> ListarMensagensAsync(int conversaId, int page, int perPage, DateTime? antesDe = null);

    Task<ContatoWhatsAppResponse> CriarOuObterContatoAsync(
        string instancia,
        string telefone,
        string? nomeWhatsApp = null,
        string? fotoUrl = null,
        int? usuarioId = null);

    Task<ConversaResponse> CriarOuObterConversaAsync(
        string instancia,
        string telefone,
        int contatoWhatsAppId,
        int? usuarioId = null);

    Task<(bool inseriu, int? id)> InserirMensagemSeNaoExistirAsync(
        int conversaId,
        int? usuarioId,
        int? envioDetalheId,
        string? evolutionId,
        string telefone,
        string instancia,
        string tipo,
        string direcao,
        string? conteudo,
        string status,
        DateTime? dataMensagem,
        string? erro = null);

    Task AtualizarStatusMensagemPorEvolutionIdAsync(string instancia, string evolutionId, string status);

    Task AtualizarMensagemSeIncompletaAsync(string instancia, string evolutionId, string? novoConteudo, string? direcao, string? status, DateTime? dataMensagem);

    Task MarcarConversaLidaAsync(int conversaId);

    Task AtualizarConversaPosMensagemAsync(
        int conversaId,
        string ultimaMensagem,
        DateTime ultimaMensagemEm,
        int incrementoNaoLidas);

    Task<List<ContatoWhatsAppResponse>> ListarContatosAsync(int usuarioId, string role, string? busca);

    Task<(int? contatoImportadoId, string? nomeImportado)?> ObterContatoImportadoPorTelefoneAsync(int usuarioId, string telefoneNormalizado);

    Task AtualizarEnvioDetalheMensagemIdAsync(int envioDetalheId, int mensagemId);
}
