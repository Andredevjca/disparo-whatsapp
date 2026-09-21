using System.Security.Claims;
using DisparoApi.Dtos;
using DisparoApi.Options;
using DisparoApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DisparoApi.Controllers;

[ApiController]
[Route("api/atendimento")]
[Authorize]
public class AtendimentoController : ControllerBase
{
    private readonly IAtendimentoService _service;
    private readonly SincroniaMonitor _monitor;
    private readonly EvolutionOptions _evoOptions;
    private readonly ILogger<AtendimentoController> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public AtendimentoController(
        IAtendimentoService service,
        SincroniaMonitor monitor,
        IOptions<EvolutionOptions> evoOptions,
        ILogger<AtendimentoController> logger,
        IServiceScopeFactory scopeFactory)
    {
        _service = service;
        _monitor = monitor;
        _evoOptions = evoOptions.Value;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    private int UsuarioId => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
    private string Role => User.FindFirst(ClaimTypes.Role)?.Value ?? "user";

    [HttpGet("conversas")]
    public async Task<IActionResult> ListarConversas([FromQuery] string? busca, [FromQuery] int page = 1, [FromQuery] int perPage = 50)
    {
        try
        {
            var res = await _service.ListarConversasAsync(UsuarioId, Role, busca, page, perPage);
            return Ok(res);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { erro = ex.Message });
        }
    }

    [HttpGet("conversas/{id:int}")]
    public async Task<IActionResult> ObterConversa(int id)
    {
        try
        {
            var res = await _service.ObterConversaAsync(id, UsuarioId, Role);
            if (res == null) return NotFound(new { erro = "Conversa não encontrada" });
            return Ok(res);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { erro = ex.Message });
        }
    }

    [HttpGet("conversas/{id:int}/mensagens")]
    public async Task<IActionResult> ListarMensagens(int id, [FromQuery] int page = 1, [FromQuery] int perPage = 50)
    {
        try
        {
            var res = await _service.ListarMensagensAsync(id, UsuarioId, Role, page, perPage);
            return Ok(res);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { erro = "Conversa não encontrada" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { erro = ex.Message });
        }
    }

    [HttpGet("conversas/{id:int}/mensagens/{mensagemId:int}/imagem")]
    public async Task<IActionResult> ObterImagem(int id, int mensagemId)
    {
        var imagem = await _service.ObterImagemMensagemAsync(id, mensagemId, UsuarioId, Role);
        if (imagem == null) return NotFound();
        Response.Headers.CacheControl = "private, max-age=3600";
        return File(Convert.FromBase64String(imagem.Base64), imagem.MimeType);
    }

    [HttpPost("conversas/{id:int}/mensagens")]
    public async Task<IActionResult> EnviarMensagem(int id, [FromBody] EnviarMensagemAtendimentoRequest req)
    {
        try
        {
            if (await _service.ObterConversaAsync(id, UsuarioId, Role) == null)
                return NotFound(new { erro = "Conversa não encontrada" });
            var res = await _service.EnviarMensagemAtendimentoAsync(UsuarioId, id, req.Texto);
            return Ok(res);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { erro = "Conversa não encontrada" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { erro = ex.Message });
        }
    }

    [HttpPost("conversas/{id:int}/ler")]
    public async Task<IActionResult> MarcarComoLida(int id)
    {
        try
        {
            await _service.MarcarComoLidaAsync(UsuarioId, Role, id);
            return Ok(new { ok = true });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { erro = "Conversa não encontrada" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { erro = ex.Message });
        }
    }

    [HttpGet("contatos")]
    public async Task<IActionResult> ListarContatos([FromQuery] string? busca)
    {
        try
        {
            var res = await _service.ListarContatosAsync(UsuarioId, Role, busca);
            return Ok(res);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { erro = ex.Message });
        }
    }

    [HttpPost("sincronizar")]
    public async Task<IActionResult> DispararSincronia([FromQuery] bool forcar = false, [FromBody] SincronizarRequest? body = null)
    {
        try
        {
            var instancia = !string.IsNullOrWhiteSpace(body?.Instancia)
                ? body.Instancia
                : _evoOptions.Instance;
            if (_monitor.EstaRodando)
                return Accepted(new { status = "EM_ANDAMENTO", iniciado_em = _monitor.IniciadoEm, mensagem = "Já está sincronizando." });

            var iniciadoEm = DateTime.Now;
            _ = Task.Run(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
                var service = scope.ServiceProvider.GetRequiredService<IAtendimentoService>();
                await service.SincronizarHistoricoCompletoAsync(instancia, forcar, _logger, cts.Token);
            }, CancellationToken.None);

            return StatusCode(202, new { status = "DISPARADO", iniciado_em = iniciadoEm });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { erro = ex.Message });
        }
    }

    [HttpGet("sincronizar/status")]
    public IActionResult ObterStatusSincronia()
    {
        try
        {
            var s = _monitor.UltimoStatus;
            if (s == null) return Ok(new SincroniaStatusResponse { Status = "PENDENTE" });
            return Ok(s);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { erro = ex.Message });
        }
    }
}

public class SincronizarRequest
{
    public string? Instancia { get; set; }
}
