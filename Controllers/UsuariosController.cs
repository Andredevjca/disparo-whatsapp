using System.Security.Claims;
using DisparoApi.Dtos;
using DisparoApi.Repositories;
using DisparoApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace DisparoApi.Controllers;

[ApiController]
[Route("api/usuarios")]
public class UsuariosController : ControllerBase
{
    private readonly IUsuarioRepository _usuarios;
    private readonly IAuthService _auth;

    public UsuariosController(IUsuarioRepository usuarios, IAuthService auth)
    {
        _usuarios = usuarios;
        _auth = auth;
    }

    private async Task<IActionResult?> AutorizarSeNecessario()
    {
        var permiteAnonimo = await _auth.PermiteCadastroAnonimoAsync();
        var autenticado = User.Identity?.IsAuthenticated == true;
        if (permiteAnonimo || autenticado) return null;
        return Unauthorized(new ErrorMessageResponse { Message = "Não autenticado" });
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Listar()
    {
        var res = await _usuarios.ListAllAsync();
        return Ok(res);
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] UsuarioCreateDto dto)
    {
        var authResult = await AutorizarSeNecessario();
        if (authResult != null) return authResult;

        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
        {
            return BadRequest(new ErrorMessageResponse { Message = "E-mail e senha são obrigatórios" });
        }

        var nome = string.IsNullOrWhiteSpace(dto.Nome) ? dto.Email : dto.Nome.Trim();
        var email = dto.Email.Trim().ToLowerInvariant();
        var hash = BCrypt.Net.BCrypt.EnhancedHashPassword(dto.Password, workFactor: 10);

        try
        {
            var criado = await _usuarios.CreateAsync(nome, email, hash);
            if (criado == null) return StatusCode(500, new ErrorMessageResponse { Message = "Falha ao criar usuário" });
            return Created($"api/usuarios/{criado.Id}", criado);
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            return BadRequest(new ErrorMessageResponse { Message = "E-mail já cadastrado" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ErrorMessageResponse { Message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    [Authorize]
    public async Task<IActionResult> Atualizar(int id, [FromBody] UsuarioUpdateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email))
        {
            return BadRequest(new ErrorMessageResponse { Message = "E-mail é obrigatório" });
        }
        var existente = await _usuarios.GetByIdAsync(id);
        if (existente == null) return NotFound(new ErrorMessageResponse { Message = "Usuário não encontrado" });

        var nome = string.IsNullOrWhiteSpace(dto.Nome) ? dto.Email : dto.Nome.Trim();
        var email = dto.Email.Trim().ToLowerInvariant();
        string? hash = null;
        if (!string.IsNullOrWhiteSpace(dto.Password))
        {
            hash = BCrypt.Net.BCrypt.EnhancedHashPassword(dto.Password, workFactor: 10);
        }

        try
        {
            var res = await _usuarios.UpdateAsync(id, nome, email, hash);
            if (res == null) return NotFound();
            return Ok(res);
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            return BadRequest(new ErrorMessageResponse { Message = "E-mail já cadastrado" });
        }
    }

    [HttpDelete("{id:int}")]
    [Authorize]
    public async Task<IActionResult> Excluir(int id)
    {
        var atualIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimTypes.PrimarySid) ?? User.FindFirstValue("sub");
        if (int.TryParse(atualIdStr, out var atualId) && atualId == id)
        {
            return BadRequest(new ErrorMessageResponse { Message = "Não é possível excluir o próprio usuário" });
        }
        var existente = await _usuarios.GetByIdAsync(id);
        if (existente == null) return NotFound(new ErrorMessageResponse { Message = "Usuário não encontrado" });
        await _usuarios.DeleteAsync(id);
        return NoContent();
    }
}
