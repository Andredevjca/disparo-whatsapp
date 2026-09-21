using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DisparoApi.Dtos;
using DisparoApi.Options;
using DisparoApi.Repositories;
using Microsoft.Extensions.Options;

namespace DisparoApi.Controllers;

[Authorize]
[ApiController]
[Route("api/configuracao")]
public class ConfiguracaoController : ControllerBase
{
    private readonly IConfiguracaoRepository _repository;
    private readonly DefaultsOptions _defaults;

    public ConfiguracaoController(IConfiguracaoRepository repository, IOptions<DefaultsOptions> defaults)
    {
        _repository = repository;
        _defaults = defaults.Value;
    }

    [HttpGet]
    public async Task<ActionResult<ConfiguracaoResponse>> GetAsync()
    {
        var valor = await _repository.ObterAsync("intervalo_ms");
        var intervalo = int.TryParse(valor, out var v) ? v : _defaults.IntervaloMs;
        return Ok(new ConfiguracaoResponse { IntervaloMs = intervalo });
    }

    [HttpPut]
    public async Task<IActionResult> PutAsync([FromBody] ConfiguracaoUpdateDto dto)
    {
        if (dto.IntervaloMs < 1000)
            return BadRequest(new ErrorMessageResponse { Message = "Intervalo mínimo: 1 segundo" });

        await _repository.DefinirAsync("intervalo_ms", dto.IntervaloMs.ToString());
        return Ok();
    }
}
