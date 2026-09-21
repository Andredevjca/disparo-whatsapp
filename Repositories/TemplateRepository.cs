using Dapper;
using DisparoApi.Data;
using DisparoApi.Dtos;

namespace DisparoApi.Repositories;

public class TemplateRepository : ITemplateRepository
{
    private readonly IDbConnectionFactory _factory;
    private const string Select = @"SELECT t.id, t.nome, t.mensagem, t.created_at AS CreatedAt, t.updated_at AS UpdatedAt,
        i.base64 AS Base64, i.mime_type AS MimeType, i.nome_arquivo AS NomeArquivo
        FROM templates t LEFT JOIN template_imagens i ON i.template_id = t.id";

    public TemplateRepository(IDbConnectionFactory factory) { _factory = factory; }

    public async Task<List<TemplateResponse>> ListAsync()
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<TemplateResponse, ImagemEnvioDto, TemplateResponse>(Select + " ORDER BY t.id DESC",
            (template, imagem) => { template.Imagem = imagem; return template; }, splitOn: "Base64");
        return rows.ToList();
    }

    public async Task<TemplateResponse?> GetByIdAsync(int id)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<TemplateResponse, ImagemEnvioDto, TemplateResponse>(Select + " WHERE t.id = @id",
            (template, imagem) => { template.Imagem = imagem; return template; }, new { id }, splitOn: "Base64");
        return rows.SingleOrDefault();
    }

    public async Task<TemplateResponse> CreateAsync(string nome, string mensagem, ImagemEnvioDto? imagem = null)
    {
        imagem?.Validar();
        using var conn = _factory.Create();
        await conn.OpenAsync();
        using var tx = conn.BeginTransaction();
        var id = await conn.ExecuteScalarAsync<int>("INSERT INTO templates (nome, mensagem) VALUES (@nome, @mensagem); SELECT LAST_INSERT_ID();", new { nome, mensagem }, tx);
        if (imagem != null)
            await conn.ExecuteAsync("INSERT INTO template_imagens (template_id, base64, mime_type, nome_arquivo) VALUES (@id, @Base64, @MimeType, @NomeArquivo)",
                new { id, imagem.Base64, imagem.MimeType, imagem.NomeArquivo }, tx);
        tx.Commit();
        return (await GetByIdAsync(id))!;
    }

    public async Task<TemplateResponse?> UpdateAsync(int id, string nome, string mensagem, ImagemEnvioDto? imagem = null)
    {
        imagem?.Validar();
        using var conn = _factory.Create();
        await conn.OpenAsync();
        using var tx = conn.BeginTransaction();
        var exists = await conn.ExecuteScalarAsync<int?>("SELECT id FROM templates WHERE id = @id FOR UPDATE", new { id }, tx);
        if (exists == null) return null;
        await conn.ExecuteAsync("UPDATE templates SET nome = @nome, mensagem = @mensagem WHERE id = @id", new { id, nome, mensagem }, tx);
        await conn.ExecuteAsync("DELETE FROM template_imagens WHERE template_id = @id", new { id }, tx);
        if (imagem != null)
            await conn.ExecuteAsync("INSERT INTO template_imagens (template_id, base64, mime_type, nome_arquivo) VALUES (@id, @Base64, @MimeType, @NomeArquivo)",
                new { id, imagem.Base64, imagem.MimeType, imagem.NomeArquivo }, tx);
        tx.Commit();
        return await GetByIdAsync(id);
    }

    public async Task DeleteAsync(int id)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync("DELETE FROM templates WHERE id = @id", new { id });
    }
}
