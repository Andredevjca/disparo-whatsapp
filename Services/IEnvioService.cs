using DisparoApi.Dtos;

namespace DisparoApi.Services;

public interface IEnvioService
{
    Task<EnvioUnitarioResponse> EnviarUnitarioAsync(int usuarioId, EnvioUnitarioRequest req);
    Task<EnvioMassaResponse> CriarEIniciarEnvioMassaAsync(int usuarioId, EnvioMassaRequest req);
    Task<EnvioMassaResponse> CriarEIniciarEnvioMassaPorGrupoAsync(int grupoId, int usuarioId, string role, EnvioPorGrupoRequest req);
}
