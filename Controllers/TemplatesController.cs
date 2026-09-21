using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DisparoApi.Dtos;
using DisparoApi.Repositories;

namespace DisparoApi.Controllers;

[Authorize]
[ApiController]
[Route("api/modelos")]
public class TemplatesController : ControllerBase
{
    private readonly ITemplateRepository _repository;

    public TemplatesController(ITemplateRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<ActionResult<List<TemplateResponse>>> ListAsync()
    {
        var templates = await _repository.ListAsync();
        return Ok(templates);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TemplateResponse>> GetByIdAsync(int id)
    {
        var template = await _repository.GetByIdAsync(id);
        if (template == null)
            return NotFound();
        return Ok(template);
    }

    [HttpPost]
    public async Task<ActionResult<TemplateResponse>> PostAsync([FromBody] TemplateCreateUpdateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nome) || string.IsNullOrWhiteSpace(dto.Mensagem))
            return BadRequest(new ErrorMessageResponse { Message = "Nome e mensagem são obrigatórios" });

        try { dto.Imagem?.Validar(); }
        catch (ArgumentException ex) { return BadRequest(new ErrorMessageResponse { Message = ex.Message }); }

        var created = await _repository.CreateAsync(dto.Nome.Trim(), dto.Mensagem.Trim(), dto.Imagem);
        return Created($"/api/modelos/{created.Id}", created);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<TemplateResponse>> PutAsync(int id, [FromBody] TemplateCreateUpdateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nome) || string.IsNullOrWhiteSpace(dto.Mensagem))
            return BadRequest(new ErrorMessageResponse { Message = "Nome e mensagem são obrigatórios" });

        try { dto.Imagem?.Validar(); }
        catch (ArgumentException ex) { return BadRequest(new ErrorMessageResponse { Message = ex.Message }); }

        var updated = await _repository.UpdateAsync(id, dto.Nome.Trim(), dto.Mensagem.Trim(), dto.Imagem);
        if (updated == null)
            return NotFound();
        return Ok(updated);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAsync(int id)
    {
        var existing = await _repository.GetByIdAsync(id);
        if (existing == null)
            return NotFound();

        await _repository.DeleteAsync(id);
        return NoContent();
    }
}
