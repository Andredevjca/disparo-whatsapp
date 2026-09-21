using System.Text.Json;
using DisparoApi.Helpers;
using DisparoApi.Models;
using DisparoApi.Options;
using DisparoApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DisparoApi.Controllers;

[ApiController]
[Route("api/evolution")]
public class WebhookController : ControllerBase
{
    private readonly IAtendimentoService _atendimento;
    private readonly EvolutionOptions _opts;
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(
        IAtendimentoService atendimento,
        IOptions<EvolutionOptions> opts,
        ILogger<WebhookController> logger)
    {
        _atendimento = atendimento;
        _opts = opts.Value;
        _logger = logger;
    }

    [HttpPost("webhook")]
    [HttpPost("webhook/{evento}")]
    public async Task<IActionResult> Webhook([FromQuery] string? instance, [FromHeader(Name = "apikey")] string? apiKeyHeader,
        CancellationToken ct)
    {
        try
        {
            using var reader = new StreamReader(Request.Body, leaveOpen: false);
            var body = await reader.ReadToEndAsync(ct);
            if (string.IsNullOrWhiteSpace(body))
            {
                _logger.LogWarning("Webhook recebido com body vazio");
                return BadRequest(new { ok = false, erro = "Body vazio" });
            }

            JsonDocument doc;
            try { doc = JsonDocument.Parse(body); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Webhook com JSON inválido");
                return BadRequest(new { ok = false, erro = "JSON inválido" });
            }

            using (doc)
            {
                var instancia = ExtrairInstancia(instance, doc.RootElement);

                // Valida apikey (se configurada)
                if (!string.IsNullOrWhiteSpace(_opts.ApiKey))
                {
                    var hdr = apiKeyHeader ?? Request.Headers["apikey"].FirstOrDefault()
                                                    ?? Request.Headers["x-api-key"].FirstOrDefault()
                                                    ?? Request.Query["apikey"].FirstOrDefault()
                                                    ?? EvolutionWebhookHelper.Texto(doc.RootElement, "apikey");
                    if (!string.Equals(hdr, _opts.ApiKey, StringComparison.Ordinal))
                    {
                        _logger.LogWarning(
                            "Webhook com apikey inválida ou ausente. Instancia={Instancia} HeaderPresente={Presente}",
                            instancia, !string.IsNullOrWhiteSpace(hdr));
                        return Unauthorized(new { ok = false, erro = "apikey inválida" });
                    }
                }

                if (string.IsNullOrWhiteSpace(instancia))
                    return BadRequest(new { ok = false, erro = "Instância não informada" });
                await _atendimento.ProcessarWebhookEventoAsync(instancia, doc, _logger);
                return Ok(new { ok = true });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no processamento do webhook");
            return StatusCode(500, new { ok = false, erro = "Erro interno" });
        }
    }

    private static string? ExtrairInstancia(string? queryInstance, JsonElement root)
    {
        if (!string.IsNullOrWhiteSpace(queryInstance)) return queryInstance;
        var value = EvolutionWebhookHelper.Texto(root, "instance") ?? EvolutionWebhookHelper.Texto(root, "instanceName");
        if (!string.IsNullOrWhiteSpace(value)) return value;
        if (root.TryGetProperty("data", out var data))
            return EvolutionWebhookHelper.Texto(data, "instance") ?? EvolutionWebhookHelper.Texto(data, "instanceName");
        return null;
    }
}
