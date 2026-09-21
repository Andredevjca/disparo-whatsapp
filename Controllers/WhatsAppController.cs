using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DisparoApi.Dtos;
using DisparoApi.Helpers;
using DisparoApi.Models;
using DisparoApi.Services;

namespace DisparoApi.Controllers;

[Authorize]
[ApiController]
[Route("api/whatsapp")]
public class WhatsAppController : ControllerBase
{
    private readonly IEvolutionApiService _evolution;

    public WhatsAppController(IEvolutionApiService evolution)
    {
        _evolution = evolution;
    }

    private static object SerializarConta(WhatsAppAccount c) => new
    {
        state = c.State.ToString().ToLowerInvariant(),
        connected = c.Connected,
        instance = c.Instance,
        number = c.Number,
        profileName = c.ProfileName,
        qrcode = c.Qrcode
    };

    [HttpGet("contas")]
    public async Task<ActionResult<List<object>>> GetContasAsync()
    {
        try
        {
            var contas = await _evolution.ListarContasAsync();
            return Ok(contas.Select(SerializarConta).ToList());
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new ErrorMessageResponse { Message = ex.Message });
        }
    }

    [HttpGet("status")]
    public async Task<ActionResult<object>> GetStatusAsync()
    {
        try
        {
            var contas = await _evolution.ListarContasAsync();
            var conectadas = contas.Count(c => c.Connected);
            var total = contas.Count;
            var numero = contas.FirstOrDefault(c => c.Connected)?.Number;
            return Ok(new
            {
                contas = contas.Select(SerializarConta).ToList(),
                connected = conectadas,
                total = total,
                number = numero
            });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new ErrorMessageResponse { Message = ex.Message });
        }
    }

    [HttpPost("contas")]
    public async Task<ActionResult> PostContasAsync([FromBody] ContaWhatsAppCreateDto dto)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dto.Nome))
                return BadRequest(new ErrorMessageResponse { Message = "Nome é obrigatório" });

            var instance = SlugHelper.GerarSlugInstancia(dto.Nome);
            if (string.IsNullOrWhiteSpace(instance))
                return BadRequest(new ErrorMessageResponse { Message = "Nome inválido" });

            var conta = await _evolution.GarantirInstanciaEConectarAsync(instance);
            return StatusCode(StatusCodes.Status201Created, SerializarConta(conta));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new ErrorMessageResponse { Message = ex.Message });
        }
    }

    [HttpPost("contas/{instance}/conectar")]
    [HttpPost("contas/{instance}/connect")]
    public async Task<ActionResult> PostConectarAsync(string instance)
    {
        try
        {
            var conta = await _evolution.ConectarAsync(instance);
            return Ok(SerializarConta(conta));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new ErrorMessageResponse { Message = ex.Message });
        }
    }

    [HttpPost("contas/{instance}/desconectar")]
    [HttpPost("contas/{instance}/disconnect")]
    public async Task<ActionResult> PostDesconectarAsync(string instance)
    {
        try
        {
            await _evolution.DesconectarAsync(instance);
            return Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new ErrorMessageResponse { Message = ex.Message });
        }
    }

    [HttpDelete("contas/{instance}")]
    public async Task<IActionResult> DeleteContaAsync(string instance)
    {
        try
        {
            try
            {
                await _evolution.DesconectarAsync(instance);
            }
            catch
            {
            }
            await _evolution.RemoverInstanciaAsync(instance);
            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new ErrorMessageResponse { Message = ex.Message });
        }
    }
}
