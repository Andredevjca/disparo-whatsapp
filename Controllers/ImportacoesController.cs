using System.Security.Claims;
using DisparoApi.Dtos;
using DisparoApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DisparoApi.Controllers;

[Authorize]
[ApiController]
[Route("api/importacoes")]
public class ImportacoesController : ControllerBase
{
    private readonly IImportacaoService _importacao;
    private readonly IEnvioService _envios;
    private readonly Repositories.IImportacaoRepository _repo;

    public ImportacoesController(IImportacaoService importacao, IEnvioService envios, Repositories.IImportacaoRepository repo)
    {
        _importacao = importacao;
        _envios = envios;
        _repo = repo;
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

    [HttpPost("importar")]
    [DisableRequestSizeLimit]
    [RequestFormLimits(ValueLengthLimit = int.MaxValue, MultipartBodyLengthLimit = long.MaxValue)]
    public async Task<IActionResult> Importar(IFormFile? arquivo, [FromForm] string? nome)
    {
        if (arquivo == null)
            return BadRequest(new ErrorMessageResponse { Message = "Informe o campo 'arquivo'" });
        try
        {
            var res = await _importacao.ImportarArquivoAsync(UsuarioId, arquivo, nome);
            return Ok(res);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorMessageResponse { Message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ErrorMessageResponse { Message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> ListarGrupos()
    {
        var res = await _repo.ListarGruposAsync(UsuarioId, UsuarioRole);
        return Ok(res);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detalhe(int id, [FromQuery] int page = 1, [FromQuery] int perPage = 20, [FromQuery] string? busca = null)
    {
        if (page < 1) page = 1;
        if (perPage < 1) perPage = 20;
        if (perPage > 200) perPage = 200;

        var grupo = await _repo.ObterGrupoAsync(id, UsuarioId, UsuarioRole);
        if (grupo == null) return NotFound(new ErrorMessageResponse { Message = "Grupo não encontrado" });

        var contatos = await _repo.ListarContatosPaginadoAsync(id, page, perPage, busca);
        var total = await _repo.ContarContatosAsync(id, busca);

        return Ok(new GrupoDetalhePaginadoResponse
        {
            Grupo = grupo,
            Contatos = contatos,
            Page = page,
            PerPage = perPage,
            Total = total
        });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Excluir(int id)
    {
        var grupo = await _repo.ObterGrupoAsync(id, UsuarioId, UsuarioRole);
        if (grupo == null) return NotFound(new ErrorMessageResponse { Message = "Grupo não encontrado" });
        await _repo.ExcluirGrupoAsync(id, UsuarioId, UsuarioRole);
        return NoContent();
    }

    [HttpPost("{id:int}/enviar")]
    public async Task<IActionResult> Enviar(int id, [FromBody] EnvioPorGrupoRequest req)
    {
        try
        {
            var res = await _envios.CriarEIniciarEnvioMassaPorGrupoAsync(id, UsuarioId, UsuarioRole, req ?? new EnvioPorGrupoRequest());
            return Ok(new EnvioMassaResponse { Id = res.Id });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ErrorMessageResponse { Message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorMessageResponse { Message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ErrorMessageResponse { Message = ex.Message });
        }
    }
}
