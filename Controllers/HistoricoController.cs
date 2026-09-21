using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DisparoApi.Dtos;
using DisparoApi.Repositories;

namespace DisparoApi.Controllers;

[Authorize]
[ApiController]
[Route("api/historico")]
public class HistoricoController : ControllerBase
{
    private readonly IEnvioRepository _repository;

    public HistoricoController(IEnvioRepository repository)
    {
        _repository = repository;
    }

    private int UsuarioId
    {
        get
        {
            var s = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            return int.TryParse(s, out var v) ? v : 0;
        }
    }

    private string UsuarioRole
        => User.FindFirstValue(ClaimTypes.Role) ?? "user";

    [HttpGet]
    public async Task<ActionResult<HistoricoPaginadoResponse>> GetAsync([FromQuery] HistoricoFiltroDto filtro)
    {
        filtro ??= new HistoricoFiltroDto();
        var historico = await _repository.ObterHistoricoPaginadoAsync(UsuarioId, UsuarioRole, filtro);
        return Ok(historico);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<EnvioDetalheResponse>> GetByIdAsync(int id)
    {
        var detalhe = await _repository.ObterHistoricoDetalheAsync(id);
        if (detalhe == null)
            return NotFound();
        if (!string.Equals(UsuarioRole, "admin", StringComparison.OrdinalIgnoreCase)
            && detalhe.UsuarioId.HasValue && detalhe.UsuarioId.Value != UsuarioId)
        {
            return NotFound();
        }
        return Ok(detalhe);
    }
}
