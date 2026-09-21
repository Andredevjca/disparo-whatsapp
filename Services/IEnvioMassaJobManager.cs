namespace DisparoApi.Services;

public interface IEnvioMassaJobManager
{
    void IniciarProcessamento(int envioId, int intervaloMs, string instance);
    bool SolicitarParada(int envioId);
    bool EstaProcessando(int envioId);
}
