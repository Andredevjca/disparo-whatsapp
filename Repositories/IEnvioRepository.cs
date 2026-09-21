using DisparoApi.Dtos;

namespace DisparoApi.Repositories;

public interface IEnvioRepository
{
    Task SalvarImagemAsync(int envioId, ImagemEnvioDto imagem);
    Task<ImagemEnvioDto?> ObterImagemAsync(int envioId);
    Task<(int envioId, int detalheId)> CriarEnvioUnitarioAsync(
        int usuarioId,
        string tipo,
        int? templateId,
        string? templateNome,
        int? intervaloMs,
        string status,
        string? instancia,
        string? numeroOrigem,
        string? nome,
        string telefone,
        string? mensagem,
        string statusDetalhe);

    Task AtualizarDetalheUnitarioAsync(int detalheId, string status, string? erro, string? evolutionId);

    Task<int> CriarEnvioMassaCabecalhoAsync(
        int usuarioId,
        string tipo,
        int? templateId,
        string? templateNome,
        int intervaloMs,
        int total,
        string status,
        string instancia,
        string numeroOrigem,
        int? grupoImportacaoId = null);

    Task InserirDetalhesMassaAsync(int envioId, int usuarioId, int? grupoImportacaoId, IEnumerable<(string? nome, string telefone, string mensagem, string status, string numeroOrigem)> detalhes);

    Task<List<(int id, string? nome, string telefone, string mensagem)>> ListarPendentesMassaAsync(int envioId);

    Task AtualizarStatusDetalheAsync(int detalheId, string status, string? erro, string? evolutionId);

    Task AtualizarContagensEnvioAsync(int envioId);

    Task FinalizarEnvioAsync(int envioId, string status);

    Task<EnvioMassaGetResponse?> ObterEnvioComDetalhesAsync(int id);

    Task<HistoricoPaginadoResponse> ObterHistoricoPaginadoAsync(int usuarioId, string role, HistoricoFiltroDto filtro);

    Task<EnvioDetalheResponse?> ObterHistoricoDetalheAsync(int id);
}
