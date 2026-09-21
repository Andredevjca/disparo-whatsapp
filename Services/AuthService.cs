using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DisparoApi.Dtos;
using DisparoApi.Options;
using DisparoApi.Repositories;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DisparoApi.Services;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(string email, string password);
    Task<bool> PermiteCadastroAnonimoAsync();
}

public class AuthService : IAuthService
{
    private readonly IUsuarioRepository _usuarios;
    private readonly JwtOptions _jwt;

    public AuthService(IUsuarioRepository usuarios, IOptions<JwtOptions> jwt)
    {
        _usuarios = usuarios;
        _jwt = jwt.Value;
    }

    public async Task<bool> PermiteCadastroAnonimoAsync()
    {
        var total = await _usuarios.CountAllAsync();
        return total == 0;
    }

    public async Task<LoginResponse> LoginAsync(string email, string password)
    {
        var usuario = await _usuarios.GetByEmailAsync(email.Trim().ToLowerInvariant())
            ?? throw new UnauthorizedAccessException("E-mail ou senha inválidos");

        var valido = BCrypt.Net.BCrypt.EnhancedVerify(password, usuario.PasswordHash);
        if (!valido) throw new UnauthorizedAccessException("E-mail ou senha inválidos");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.Email, usuario.Email),
            new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new Claim(ClaimTypes.Role, usuario.Role ?? "user"),
            new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(_jwt.ExpireHours),
            signingCredentials: creds);

        var jwt = new JwtSecurityTokenHandler().WriteToken(token);
        return new LoginResponse { Token = jwt, Email = usuario.Email };
    }
}
