using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using DisparoApi.Dtos;
using DisparoApi.Repositories;
using DisparoApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DisparoApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly IUsuarioRepository _usuarios;

    public AuthController(IAuthService auth, IUsuarioRepository usuarios)
    {
        _auth = auth;
        _usuarios = usuarios;
    }

    [HttpPost("fazer-login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorMessageResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        try
        {
            var res = await _auth.LoginAsync(req.Email ?? string.Empty, req.Password ?? string.Empty);
            return Ok(res);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ErrorMessageResponse { Message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorMessageResponse { Message = ex.Message });
        }
    }

    [HttpGet("minha-conta")]
    [Authorize]
    [ProducesResponseType(typeof(MinhaContaResponse), StatusCodes.Status200OK)]
    public IActionResult MinhaConta()
    {
        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (email == null) return Unauthorized(new ErrorMessageResponse { Message = "Sessão inválida" });
        return Ok(new MinhaContaResponse { Email = email });
    }
}
