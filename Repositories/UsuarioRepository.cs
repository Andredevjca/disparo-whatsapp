using Dapper;
using DisparoApi.Data;
using DisparoApi.Dtos;
using DisparoApi.Models;

namespace DisparoApi.Repositories;

public interface IUsuarioRepository
{
    Task<int> CountAllAsync();
    Task<Usuario?> GetByEmailAsync(string email);
    Task<Usuario?> GetByIdAsync(int id);
    Task<List<UsuarioResponse>> ListAllAsync();
    Task<UsuarioResponse?> CreateAsync(string? nome, string email, string passwordHash, string role = "user");
    Task<UsuarioResponse?> UpdateAsync(int id, string? nome, string email, string? passwordHash = null);
    Task DeleteAsync(int id);
}

public class UsuarioRepository : IUsuarioRepository
{
    private readonly IDbConnectionFactory _factory;

    public UsuarioRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<int> CountAllAsync()
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM usuarios");
    }

    public async Task<Usuario?> GetByEmailAsync(string email)
    {
        using var conn = _factory.Create();
        var sql = "SELECT id AS Id, nome AS Nome, email AS Email, password_hash AS PasswordHash, role AS Role, created_at AS CreatedAt, updated_at AS UpdatedAt FROM usuarios WHERE email = @email";
        return await conn.QueryFirstOrDefaultAsync<Usuario?>(sql, new { email });
    }

    public async Task<Usuario?> GetByIdAsync(int id)
    {
        using var conn = _factory.Create();
        var sql = "SELECT id AS Id, nome AS Nome, email AS Email, password_hash AS PasswordHash, role AS Role, created_at AS CreatedAt, updated_at AS UpdatedAt FROM usuarios WHERE id = @id";
        return await conn.QueryFirstOrDefaultAsync<Usuario?>(sql, new { id });
    }

    public async Task<List<UsuarioResponse>> ListAllAsync()
    {
        using var conn = _factory.Create();
        var sql = "SELECT id, nome, email, role, created_at FROM usuarios ORDER BY id ASC";
        var rows = await conn.QueryAsync<(int id, string? nome, string email, string role, DateTime created_at)>(sql);
        return rows
            .Select(r => new UsuarioResponse
            {
                Id = r.id,
                Nome = r.nome,
                Email = r.email,
                Role = r.role,
                CreatedAt = r.created_at
            })
            .ToList();
    }

    public async Task<UsuarioResponse?> CreateAsync(string? nome, string email, string passwordHash, string role = "user")
    {
        using var conn = _factory.Create();
        var sql = @"INSERT INTO usuarios (nome, email, password_hash, role)
                    VALUES (@nome, @email, @passwordHash, @role);
                    SELECT LAST_INSERT_ID();";
        var id = await conn.ExecuteScalarAsync<int>(sql, new { nome, email, passwordHash, role });
        return await GetResponseAsync(conn, id);
    }

    public async Task<UsuarioResponse?> UpdateAsync(int id, string? nome, string email, string? passwordHash = null)
    {
        using var conn = _factory.Create();
        var sql = passwordHash != null
            ? "UPDATE usuarios SET nome = @nome, email = @email, password_hash = @passwordHash, updated_at = CURRENT_TIMESTAMP WHERE id = @id"
            : "UPDATE usuarios SET nome = @nome, email = @email, updated_at = CURRENT_TIMESTAMP WHERE id = @id";
        await conn.ExecuteAsync(sql, new { id, nome, email, passwordHash });
        return await GetResponseAsync(conn, id);
    }

    public async Task DeleteAsync(int id)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync("DELETE FROM usuarios WHERE id = @id", new { id });
    }

    private static async Task<UsuarioResponse?> GetResponseAsync(MySqlConnector.MySqlConnection conn, int id)
    {
        var sql = "SELECT id, nome, email, role, created_at FROM usuarios WHERE id = @id";
        var r = await conn.QueryFirstOrDefaultAsync<(int id, string? nome, string email, string role, DateTime created_at)?>(sql, new { id });
        return r == null
            ? null
            : new UsuarioResponse
            {
                Id = r.Value.id,
                Nome = r.Value.nome,
                Email = r.Value.email,
                Role = r.Value.role,
                CreatedAt = r.Value.created_at
            };
    }
}
