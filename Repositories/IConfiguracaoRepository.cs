namespace DisparoApi.Repositories;

public interface IConfiguracaoRepository
{
    Task<string?> ObterAsync(string chave);
    Task DefinirAsync(string chave, string valor);
}
