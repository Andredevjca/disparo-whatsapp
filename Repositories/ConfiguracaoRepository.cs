using Dapper;
using DisparoApi.Data;

namespace DisparoApi.Repositories;

public class ConfiguracaoRepository : IConfiguracaoRepository
{
    private readonly IDbConnectionFactory _factory;

    public ConfiguracaoRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<string?> ObterAsync(string chave)
    {
        using var conn = _factory.Create();
        var sql = @"SELECT valor FROM configuracoes WHERE chave = @chave";
        return await conn.QueryFirstOrDefaultAsync<string?>(sql, new { chave });
    }

    public async Task DefinirAsync(string chave, string valor)
    {
        using var conn = _factory.Create();
        var sql = @"INSERT INTO configuracoes (chave, valor) VALUES (@c, @v)
                   ON DUPLICATE KEY UPDATE valor = VALUES(valor)";
        await conn.ExecuteAsync(sql, new { c = chave, v = valor });
    }
}
