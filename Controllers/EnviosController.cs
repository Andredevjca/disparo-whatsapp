using System.Security.Claims;
using DisparoApi.Dtos;
using DisparoApi.Repositories;
using DisparoApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DisparoApi.Controllers;

[ApiController]
[Route("api/envios")]
[Authorize]
public class EnviosController : ControllerBase
{
    private readonly IEnvioService _envioService;
    private readonly IEnvioRepository _envios;
    private readonly IEnvioMassaJobManager _jobs;

    public EnviosController(IEnvioService envioService, IEnvioRepository envios, IEnvioMassaJobManager jobs)
    {
        _envioService = envioService;
        _envios = envios;
        _jobs = jobs;
    }

    private int UsuarioId
    {
        get
        {
            var s = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            return int.TryParse(s, out var v) ? v : 0;
        }
    }

    [HttpPost("unitario")]
    [ProducesResponseType(typeof(EnvioUnitarioResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorMessageResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> EnviarUnitario([FromBody] EnvioUnitarioRequest req)
    {
        try
        {
            var res = await _envioService.EnviarUnitarioAsync(UsuarioId, req);
            return Ok(res);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorMessageResponse { Message = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new ErrorMessageResponse { Message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorMessageResponse { Message = ex.Message });
        }
    }

    [HttpPost("massa")]
    [ProducesResponseType(typeof(EnvioMassaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorMessageResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CriarMassa([FromBody] EnvioMassaRequest req)
    {
        try
        {
            var res = await _envioService.CriarEIniciarEnvioMassaAsync(UsuarioId, req);
            return Ok(res);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorMessageResponse { Message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorMessageResponse { Message = ex.Message });
        }
    }

    [HttpGet("massa/{id:int}")]
    [ProducesResponseType(typeof(EnvioMassaGetResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorMessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterMassa(int id)
    {
        var res = await _envios.ObterEnvioComDetalhesAsync(id);
        if (res == null || (!User.IsInRole("admin") && res.Envio.UsuarioId != UsuarioId))
            return NotFound(new ErrorMessageResponse { Message = "Envio não encontrado" });
        return Ok(res);
    }

    [HttpPost("massa/{id:int}/parar")]
    [ProducesResponseType(typeof(EnvioMassaGetResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorMessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PararMassa(int id)
    {
        var existe = await _envios.ObterEnvioComDetalhesAsync(id);
        if (existe == null || (!User.IsInRole("admin") && existe.Envio.UsuarioId != UsuarioId))
            return NotFound(new ErrorMessageResponse { Message = "Envio não encontrado" });

        if (!_jobs.SolicitarParada(id))
            return Conflict(new ErrorMessageResponse { Message = "Este disparo não está em execução nesta aplicação. Se foi iniciado no sistema anterior, pare-o por lá." });

        var atualizado = await _envios.ObterEnvioComDetalhesAsync(id);
        return Ok(atualizado ?? existe);
    }
}
