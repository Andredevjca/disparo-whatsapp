using DisparoApi.Dtos;
using Microsoft.AspNetCore.Http;

namespace DisparoApi.Services;

public interface IImportacaoService
{
    Task<ImportarPlanilhaResponse> ImportarArquivoAsync(int usuarioId, IFormFile arquivo, string? nomeGrupo);
}
