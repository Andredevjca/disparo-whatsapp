using Dapper;
using DisparoApi.Data;
using DisparoApi.Dtos;
using DisparoApi.Models;

namespace DisparoApi.Repositories;

public class EnvioRepository : IEnvioRepository
{
    private readonly IDbConnectionFactory _factory;

    public EnvioRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task SalvarImagemAsync(int envioId, ImagemEnvioDto imagem)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync("INSERT INTO envio_imagens (envio_id, base64, mime_type, nome_arquivo) VALUES (@envioId, @Base64, @MimeType, @NomeArquivo)",
            new { envioId, imagem.Base64, imagem.MimeType, imagem.NomeArquivo });
    }
    public async Task<ImagemEnvioDto?> ObterImagemAsync(int envioId)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<ImagemEnvioDto>("SELECT base64 AS Base64, mime_type AS MimeType, nome_arquivo AS NomeArquivo FROM envio_imagens WHERE envio_id = @envioId", new { envioId });
    }

    public async Task<(int envioId, int detalheId)> CriarEnvioUnitarioAsync(
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
        string statusDetalhe)
    {
        using var conn = _factory.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();

        var sqlEnvio = @"INSERT INTO envios
                        (usuario_id, tipo, template_id, template_nome, intervalo_ms, total, enviados, erros, pendentes, status, instancia, numero_origem)
                        VALUES
                        (@usuarioId, @tipo, @templateId, @templateNome, @intervaloMs, 1, 0, 0, 1, @status, @instancia, @numeroOrigem);
                        SELECT LAST_INSERT_ID();";

        var envioId = await conn.ExecuteScalarAsync<int>(sqlEnvio, new
        {
            usuarioId,
            tipo,
            templateId,
            templateNome,
            intervaloMs,
            status,
            instancia,
            numeroOrigem
        }, tx);

        var sqlDetalhe = @"INSERT INTO envios_detalhes
                          (envio_id, usuario_id, nome, telefone, mensagem, status, numero_origem)
                          VALUES
                          (@envioId, @usuarioId, @nome, @telefone, @mensagem, @statusDetalhe, @numeroOrigem);
                          SELECT LAST_INSERT_ID();";

        var detalheId = await conn.ExecuteScalarAsync<int>(sqlDetalhe, new
        {
            envioId,
            usuarioId,
            nome,
            telefone,
            mensagem,
            statusDetalhe,
            numeroOrigem
        }, tx);

        tx.Commit();
        return (envioId, detalheId);
    }

    public async Task AtualizarDetalheUnitarioAsync(int detalheId, string status, string? erro, string? evolutionId)
    {
        using var conn = _factory.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();

        var sqlDetalhe = @"UPDATE envios_detalhes
                          SET status = @status,
                              erro = @erro,
                              evolution_id = @evolutionId,
                              enviado_em = CASE WHEN @status = 'ENVIADO' THEN NOW() ELSE enviado_em END
                          WHERE id = @detalheId;
                          SELECT envio_id FROM envios_detalhes WHERE id = @detalheId";

        var envioId = await conn.ExecuteScalarAsync<int?>(sqlDetalhe, new { detalheId, status, erro, evolutionId }, tx);

        if (envioId.HasValue)
        {
            await AtualizarContagensInternoAsync(conn, tx, envioId.Value);

            var sqlFinaliza = @"UPDATE envios SET
                               finished_at = CASE WHEN pendentes = 0 THEN NOW() ELSE finished_at END,
                               status = CASE WHEN pendentes = 0 AND erros > 0 THEN status
                                            WHEN pendentes = 0 THEN 'CONCLUIDO'
                                            ELSE status END
                               WHERE id = @envioId";
            await conn.ExecuteAsync(sqlFinaliza, new { envioId = envioId.Value }, tx);
        }

        tx.Commit();
    }

    public async Task<int> CriarEnvioMassaCabecalhoAsync(
        int usuarioId,
        string tipo,
        int? templateId,
        string? templateNome,
        int intervaloMs,
        int total,
        string status,
        string instancia,
        string numeroOrigem,
        int? grupoImportacaoId = null)
    {
        using var conn = _factory.Create();
        var sql = @"INSERT INTO envios
                   (usuario_id, tipo, template_id, template_nome, intervalo_ms, total, enviados, erros, pendentes, status, instancia, numero_origem, grupo_importacao_id)
                   VALUES
                   (@usuarioId, @tipo, @templateId, @templateNome, @intervaloMs, @total, 0, 0, @total, @status, @instancia, @numeroOrigem, @grupoImportacaoId);
                   SELECT LAST_INSERT_ID();";
        return await conn.ExecuteScalarAsync<int>(sql, new
        {
            usuarioId,
            tipo,
            templateId,
            templateNome,
            intervaloMs,
            total,
            status,
            instancia,
            numeroOrigem,
            grupoImportacaoId
        });
    }

    public async Task InserirDetalhesMassaAsync(int envioId, int usuarioId, int? grupoImportacaoId, IEnumerable<(string? nome, string telefone, string mensagem, string status, string numeroOrigem)> detalhes)
    {
        using var conn = _factory.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();

        var sql = @"INSERT INTO envios_detalhes
                   (envio_id, usuario_id, grupo_importacao_id, nome, telefone, mensagem, status, numero_origem)
                   VALUES
                   (@envioId, @usuarioId, @grupoImportacaoId, @nome, @telefone, @mensagem, @status, @numeroOrigem)";

        foreach (var d in detalhes)
        {
            await conn.ExecuteAsync(sql, new
            {
                envioId,
                usuarioId,
                grupoImportacaoId,
                d.nome,
                d.telefone,
                d.mensagem,
                d.status,
                d.numeroOrigem
            }, tx);
        }

        tx.Commit();
    }

    public async Task<List<(int id, string? nome, string telefone, string mensagem)>> ListarPendentesMassaAsync(int envioId)
    {
        using var conn = _factory.Create();
        var sql = @"SELECT id, nome, telefone, mensagem
                   FROM envios_detalhes
                   WHERE envio_id = @envioId AND status = 'PENDENTE'
                   ORDER BY id ASC";
        var result = await conn.QueryAsync<(int id, string? nome, string telefone, string mensagem)>(sql, new { envioId });
        return result.ToList();
    }

    public async Task AtualizarStatusDetalheAsync(int detalheId, string status, string? erro, string? evolutionId)
    {
        using var conn = _factory.Create();
        var sql = @"UPDATE envios_detalhes
                   SET status = @status,
                       erro = @erro,
                       evolution_id = @evolutionId,
                       enviado_em = CASE WHEN @status = 'ENVIADO' THEN NOW() ELSE enviado_em END
                   WHERE id = @detalheId";
        await conn.ExecuteAsync(sql, new { detalheId, status, erro, evolutionId });
    }

    public async Task AtualizarContagensEnvioAsync(int envioId)
    {
        using var conn = _factory.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();
        await AtualizarContagensInternoAsync(conn, tx, envioId);
        tx.Commit();
    }

    private static async Task AtualizarContagensInternoAsync(MySqlConnector.MySqlConnection conn, System.Data.IDbTransaction tx, int envioId)
    {
        var sqlContagens = @"SELECT
                             SUM(CASE WHEN status = 'ENVIADO' THEN 1 ELSE 0 END) AS enviados,
                             SUM(CASE WHEN status = 'ERRO' THEN 1 ELSE 0 END) AS erros,
                             SUM(CASE WHEN status IN ('PENDENTE','ENVIANDO') THEN 1 ELSE 0 END) AS pendentes
                             FROM envios_detalhes WHERE envio_id = @envioId";

        var contagens = await conn.QueryFirstAsync(sqlContagens, new { envioId }, tx);

        var sqlUpdate = @"UPDATE envios SET
                         enviados = @enviados,
                         erros = @erros,
                         pendentes = @pendentes
                         WHERE id = @envioId";
        await conn.ExecuteAsync(sqlUpdate, new
        {
            enviados = (int?)contagens.enviados ?? 0,
            erros = (int?)contagens.erros ?? 0,
            pendentes = (int?)contagens.pendentes ?? 0,
            envioId
        }, tx);
    }

    public async Task FinalizarEnvioAsync(int envioId, string status)
    {
        using var conn = _factory.Create();
        var sql = @"UPDATE envios SET status = @status, finished_at = NOW() WHERE id = @envioId";
        await conn.ExecuteAsync(sql, new { envioId, status });
    }

    public async Task<EnvioMassaGetResponse?> ObterEnvioComDetalhesAsync(int id)
    {
        using var conn = _factory.Create();

        var sqlEnvio = @"SELECT id, usuario_id, tipo, template_id, template_nome, intervalo_ms,
                        total, enviados, erros, pendentes, status, instancia, numero_origem,
                        created_at, finished_at
                        FROM envios WHERE id = @id";
        var envio = await conn.QueryFirstOrDefaultAsync<EnvioResponse>(sqlEnvio, new { id });
        if (envio == null) return null;

        var sqlDetalhes = @"SELECT id, envio_id, nome, telefone, mensagem, status,
                           erro, evolution_id, numero_origem, created_at, enviado_em
                           FROM envios_detalhes WHERE envio_id = @envioId
                           ORDER BY id DESC LIMIT 500";
        var detalhes = await conn.QueryAsync<EnvioDetalheResponse>(sqlDetalhes, new { envioId = id });

        return new EnvioMassaGetResponse
        {
            Envio = envio,
            Detalhes = detalhes.ToList()
        };
    }

    public async Task<HistoricoPaginadoResponse> ObterHistoricoPaginadoAsync(int usuarioId, string role, HistoricoFiltroDto filtro)
    {
        using var conn = _factory.Create();
        var isAdmin = string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase);
        var page = filtro.Page ?? 1;
        var perPage = filtro.PerPage ?? 50;
        if (page < 1) page = 1;
        if (perPage < 1) perPage = 20;
        if (perPage > 200) perPage = 200;
        var offset = (page - 1) * perPage;

        var baseSql = @" FROM envios_detalhes d
                       INNER JOIN envios e ON e.id = d.envio_id
                       WHERE 1=1";
        if (!isAdmin) baseSql += " AND d.usuario_id = @usuarioId";

        var parametros = new DynamicParameters();
        if (!isAdmin) parametros.Add("usuarioId", usuarioId);

        if (!string.IsNullOrWhiteSpace(filtro.Status))
        {
            baseSql += " AND d.status = @status";
            parametros.Add("status", filtro.Status);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Telefone))
        {
            baseSql += " AND d.telefone LIKE @telefone";
            parametros.Add("telefone", $"%{filtro.Telefone}%");
        }

        if (!string.IsNullOrWhiteSpace(filtro.Nome))
        {
            baseSql += " AND d.nome LIKE @nome";
            parametros.Add("nome", $"%{filtro.Nome}%");
        }

        if (!string.IsNullOrWhiteSpace(filtro.Instancia))
        {
            baseSql += " AND e.instancia = @instancia";
            parametros.Add("instancia", filtro.Instancia);
        }

        if (filtro.GrupoImportacaoId.HasValue)
        {
            baseSql += " AND d.grupo_importacao_id = @grupoId";
            parametros.Add("grupoId", filtro.GrupoImportacaoId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filtro.De))
        {
            baseSql += " AND d.created_at >= @de";
            parametros.Add("de", DateTime.Parse(filtro.De).Date);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Ate))
        {
            baseSql += " AND d.created_at <= @ate";
            parametros.Add("ate", DateTime.Parse(filtro.Ate).Date.AddDays(1).AddTicks(-1));
        }

        var total = await conn.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) " + baseSql, parametros));

        var rowsSql = @"SELECT d.id, d.envio_id, d.grupo_importacao_id, d.usuario_id, d.nome, d.telefone, d.mensagem, d.status,
                       d.erro, d.evolution_id, d.numero_origem, d.created_at, d.enviado_em,
                       e.tipo AS tipo, e.template_nome AS template_nome, e.instancia AS instancia,
                       CASE WHEN e.tipo = 'UNITARIO' THEN 'Unitário' ELSE 'Massa' END AS envio_origem"
                       + baseSql
                       + " ORDER BY d.id DESC LIMIT @perPage OFFSET @offset";
        parametros.Add("perPage", perPage);
        parametros.Add("offset", offset);
        var rows = await conn.QueryAsync<EnvioDetalheResponse>(rowsSql, parametros);

        return new HistoricoPaginadoResponse
        {
            Page = page,
            PerPage = perPage,
            Total = total,
            Rows = rows.ToList()
        };
    }

    public async Task<EnvioDetalheResponse?> ObterHistoricoDetalheAsync(int id)
    {
        using var conn = _factory.Create();
        var sql = @"SELECT d.id, d.envio_id, d.nome, d.telefone, d.mensagem, d.status,
                   d.erro, d.evolution_id, d.numero_origem, d.created_at, d.enviado_em,
                   e.tipo AS tipo, e.template_nome AS template_nome, e.instancia AS instancia,
                   CASE WHEN e.tipo = 'UNITARIO' THEN 'Unitário' ELSE 'Massa' END AS envio_origem
                   FROM envios_detalhes d
                   INNER JOIN envios e ON e.id = d.envio_id
                   WHERE d.id = @id";
        return await conn.QueryFirstOrDefaultAsync<EnvioDetalheResponse>(sql, new { id });
    }
}
