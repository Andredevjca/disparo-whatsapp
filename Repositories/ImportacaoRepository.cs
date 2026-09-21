using Dapper;
using DisparoApi.Data;
using DisparoApi.Dtos;
using DisparoApi.Models;
using System.Text.Json;

namespace DisparoApi.Repositories;

public class ImportacaoRepository : IImportacaoRepository
{
    private readonly IDbConnectionFactory _factory;

    public ImportacaoRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<int> CriarGrupoAsync(int usuarioId, string? nome, string? arquivoNome)
    {
        using var conn = _factory.Create();
        var sql = @"INSERT INTO grupos_importacoes (usuario_id, nome, arquivo_nome)
                    VALUES (@usuarioId, @nome, @arquivoNome);
                    SELECT LAST_INSERT_ID()";
        return await conn.ExecuteScalarAsync<int>(sql, new { usuarioId, nome, arquivoNome });
    }

    public async Task AtualizarGrupoContagemAsync(int grupoId, int total, int validos, int invalidos, int duplicados)
    {
        using var conn = _factory.Create();
        var sql = @"UPDATE grupos_importacoes
                    SET total_linhas = @total,
                        validos = @validos,
                        invalidos = @invalidos,
                        duplicados = @duplicados
                    WHERE id = @grupoId";
        await conn.ExecuteAsync(sql, new { grupoId, total, validos, invalidos, duplicados });
    }

    public async Task InserirContatosBulkAsync(int grupoId, int usuarioId, List<ContatoImportado> contatos)
    {
        if (contatos.Count == 0) return;

        using var conn = _factory.Create();
        await conn.OpenAsync();
        using var tx = await conn.BeginTransactionAsync();

        try
        {
            var batchSize = 500;
            for (var i = 0; i < contatos.Count; i += batchSize)
            {
                var batch = contatos.Skip(i).Take(batchSize).ToList();
                var sql = @"INSERT INTO contatos_importados
                            (grupo_id, usuario_id, nome, email, telefone_normalizado, telefone_original, dados, status_validacao)
                            VALUES ";
                var parametros = new DynamicParameters();
                var parts = new List<string>();
                var idx = 0;
                foreach (var c in batch)
                {
                    var pfx = $"p{idx}";
                    parts.Add(
                        $"(@{pfx}_gid, @{pfx}_uid, @{pfx}_nome, @{pfx}_email, @{pfx}_tn, @{pfx}_to, @{pfx}_dados, @{pfx}_status)");
                    parametros.Add($"{pfx}_gid", grupoId);
                    parametros.Add($"{pfx}_uid", usuarioId);
                    parametros.Add($"{pfx}_nome", c.Nome);
                    parametros.Add($"{pfx}_email", c.Email);
                    parametros.Add($"{pfx}_tn", c.TelefoneNormalizado);
                    parametros.Add($"{pfx}_to", c.TelefoneOriginal);
                    parametros.Add($"{pfx}_dados", c.Dados);
                    parametros.Add($"{pfx}_status", c.StatusValidacao);
                    idx++;
                }

                sql += string.Join(", ", parts);
                await conn.ExecuteAsync(sql, parametros, tx);
            }

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<List<GrupoImportacaoResponse>> ListarGruposAsync(int usuarioId, string role)
    {
        using var conn = _factory.Create();
        var isAdmin = string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase);
        var sql = @"SELECT id, usuario_id, nome, arquivo_nome, total_linhas, validos, invalidos, duplicados, created_at
                    FROM grupos_importacoes
                    " + (isAdmin ? "" : "WHERE usuario_id = @usuarioId ") + @"
                    ORDER BY created_at DESC, id DESC";
        var result = await conn.QueryAsync<GrupoImportacaoResponse>(sql, new { usuarioId });
        return result.ToList();
    }

    public async Task<GrupoImportacaoResponse?> ObterGrupoAsync(int id, int usuarioId, string role)
    {
        using var conn = _factory.Create();
        var isAdmin = string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase);
        var sql = @"SELECT id, usuario_id, nome, arquivo_nome, total_linhas, validos, invalidos, duplicados, created_at
                    FROM grupos_importacoes
                    WHERE id = @id " + (isAdmin ? "" : " AND usuario_id = @usuarioId");
        return await conn.QueryFirstOrDefaultAsync<GrupoImportacaoResponse>(sql, new { id, usuarioId });
    }

    public async Task ExcluirGrupoAsync(int id, int usuarioId, string role)
    {
        using var conn = _factory.Create();
        var isAdmin = string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase);
        var sql = @"DELETE FROM grupos_importacoes
                    WHERE id = @id " + (isAdmin ? "" : " AND usuario_id = @usuarioId");
        await conn.ExecuteAsync(sql, new { id, usuarioId });
    }

    public async Task<List<ContatoImportadoResponse>> ListarContatosPaginadoAsync(int grupoId, int page, int perPage, string? busca)
    {
        using var conn = _factory.Create();
        var offset = (page - 1) * perPage;
        var temBusca = !string.IsNullOrWhiteSpace(busca);
        var sql = @"SELECT id, grupo_id, nome, email, telefone_normalizado, telefone_original, dados, status_validacao, created_at
                    FROM contatos_importados
                    WHERE grupo_id = @grupoId
                    " + (temBusca ? @"AND (
                        LOWER(COALESCE(nome,'')) LIKE LOWER(CONCAT('%', @busca, '%'))
                        OR LOWER(COALESCE(email,'')) LIKE LOWER(CONCAT('%', @busca, '%'))
                        OR LOWER(COALESCE(telefone_normalizado,'')) LIKE LOWER(CONCAT('%', @busca, '%'))
                        OR LOWER(COALESCE(telefone_original,'')) LIKE LOWER(CONCAT('%', @busca, '%'))
                    ) " : "") + @"
                    ORDER BY id ASC
                    LIMIT @perPage OFFSET @offset";
        var rows = await conn.QueryAsync<ContatoImportadoRaw>(sql, new { grupoId, busca, perPage, offset });
        var list = new List<ContatoImportadoResponse>();
        foreach (var r in rows)
        {
            Dictionary<string, object?>? dadosObj = null;
            if (!string.IsNullOrWhiteSpace(r.Dados))
            {
                try
                {
                    dadosObj = JsonSerializer.Deserialize<Dictionary<string, object?>>(r.Dados);
                }
                catch
                {
                    dadosObj = null;
                }
            }
            list.Add(new ContatoImportadoResponse
            {
                Id = r.Id,
                GrupoId = r.GrupoId,
                Nome = r.Nome,
                Email = r.Email,
                TelefoneNormalizado = r.TelefoneNormalizado,
                TelefoneOriginal = r.TelefoneOriginal,
                Dados = dadosObj,
                StatusValidacao = r.StatusValidacao,
                CreatedAt = r.CreatedAt
            });
        }
        return list;
    }

    public async Task<int> ContarContatosAsync(int grupoId, string? busca)
    {
        using var conn = _factory.Create();
        var temBusca = !string.IsNullOrWhiteSpace(busca);
        var sql = @"SELECT COUNT(*) FROM contatos_importados
                    WHERE grupo_id = @grupoId
                    " + (temBusca ? @"AND (
                        LOWER(COALESCE(nome,'')) LIKE LOWER(CONCAT('%', @busca, '%'))
                        OR LOWER(COALESCE(email,'')) LIKE LOWER(CONCAT('%', @busca, '%'))
                        OR LOWER(COALESCE(telefone_normalizado,'')) LIKE LOWER(CONCAT('%', @busca, '%'))
                        OR LOWER(COALESCE(telefone_original,'')) LIKE LOWER(CONCAT('%', @busca, '%'))
                    )" : "");
        return await conn.ExecuteScalarAsync<int>(sql, new { grupoId, busca });
    }

    public async Task<List<ContatoMassaDto>> ListarContatosValidosParaEnvioAsync(int grupoId)
    {
        using var conn = _factory.Create();
        var sql = @"SELECT nome, email, telefone_normalizado as Telefone, dados
                    FROM contatos_importados
                    WHERE grupo_id = @grupoId AND status_validacao = 'VALIDO'
                    ORDER BY id ASC";
        var rows = await conn.QueryAsync<ContatoValidoRaw>(sql, new { grupoId });
        var list = new List<ContatoMassaDto>();
        foreach (var r in rows)
        {
            Dictionary<string, string>? dadosObj = null;
            if (!string.IsNullOrWhiteSpace(r.Dados))
            {
                try
                {
                    var d = JsonSerializer.Deserialize<Dictionary<string, object?>>(r.Dados);
                    if (d != null)
                    {
                        dadosObj = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var kvp in d)
                        {
                            dadosObj[kvp.Key] = kvp.Value?.ToString() ?? string.Empty;
                        }
                    }
                }
                catch
                {
                    dadosObj = null;
                }
            }
            if (dadosObj == null) dadosObj = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(r.Email) && !dadosObj.ContainsKey("email"))
                dadosObj["email"] = r.Email!;
            list.Add(new ContatoMassaDto
            {
                Nome = r.Nome,
                Telefone = r.Telefone ?? string.Empty,
                Dados = dadosObj.Count == 0 ? null : dadosObj
            });
        }
        return list;
    }

    public async Task AtualizarEnvioGrupoIdAsync(int envioId, int grupoId)
    {
        using var conn = _factory.Create();
        var sql = "UPDATE envios SET grupo_importacao_id = @grupoId WHERE id = @envioId";
        await conn.ExecuteAsync(sql, new { envioId, grupoId });
    }

    private class ContatoImportadoRaw
    {
        public int Id { get; set; }
        public int GrupoId { get; set; }
        public string? Nome { get; set; }
        public string? Email { get; set; }
        public string? TelefoneNormalizado { get; set; }
        public string? TelefoneOriginal { get; set; }
        public string? Dados { get; set; }
        public string StatusValidacao { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    private class ContatoValidoRaw
    {
        public string? Nome { get; set; }
        public string? Email { get; set; }
        public string? Telefone { get; set; }
        public string? Dados { get; set; }
    }
}
