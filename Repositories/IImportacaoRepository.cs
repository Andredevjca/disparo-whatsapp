using DisparoApi.Dtos;
using DisparoApi.Models;

namespace DisparoApi.Repositories;

public interface IImportacaoRepository
{
    Task<int> CriarGrupoAsync(int usuarioId, string? nome, string? arquivoNome);
    Task AtualizarGrupoContagemAsync(int grupoId, int total, int validos, int invalidos, int duplicados);
    Task InserirContatosBulkAsync(int grupoId, int usuarioId, List<ContatoImportado> contatos);
    Task<List<GrupoImportacaoResponse>> ListarGruposAsync(int usuarioId, string role);
    Task<GrupoImportacaoResponse?> ObterGrupoAsync(int id, int usuarioId, string role);
    Task ExcluirGrupoAsync(int id, int usuarioId, string role);
    Task<List<ContatoImportadoResponse>> ListarContatosPaginadoAsync(int grupoId, int page, int perPage, string? busca);
    Task<int> ContarContatosAsync(int grupoId, string? busca);
    Task<List<ContatoMassaDto>> ListarContatosValidosParaEnvioAsync(int grupoId);
    Task AtualizarEnvioGrupoIdAsync(int envioId, int grupoId);
}
