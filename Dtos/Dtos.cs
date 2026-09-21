using System.Text.Json.Serialization;
using DisparoApi.Models;

namespace DisparoApi.Dtos;

public class LoginRequest
{
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;
}

public class MinhaContaResponse
{
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;
}

public class UsuarioCreateDto
{
    [JsonPropertyName("nome")]
    public string? Nome { get; set; }

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;
}

public class UsuarioUpdateDto
{
    [JsonPropertyName("nome")]
    public string? Nome { get; set; }

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string? Password { get; set; }
}

public class UsuarioResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("nome")]
    public string? Nome { get; set; }

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }
}

public class TemplateCreateUpdateDto
{
    public ImagemEnvioDto? Imagem { get; set; }

    [JsonPropertyName("nome")]
    public string Nome { get; set; } = string.Empty;

    [JsonPropertyName("mensagem")]
    public string Mensagem { get; set; } = string.Empty;
}

public class TemplateResponse
{
    public ImagemEnvioDto? Imagem { get; set; }

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("nome")]
    public string Nome { get; set; } = string.Empty;

    [JsonPropertyName("mensagem")]
    public string Mensagem { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }
}

public class ConfiguracaoResponse
{
    [JsonPropertyName("intervaloMs")]
    public int IntervaloMs { get; set; }
}

public class ConfiguracaoUpdateDto
{
    [JsonPropertyName("intervaloMs")]
    public int IntervaloMs { get; set; }
}

public class ContaWhatsAppCreateDto
{
    [JsonPropertyName("nome")]
    public string Nome { get; set; } = string.Empty;
}

public class EnvioUnitarioRequest
{
    public bool UsarImagemTemplate { get; set; } = true;

    public ImagemEnvioDto? Imagem { get; set; }

    [JsonPropertyName("telefone")]
    public string Telefone { get; set; } = string.Empty;

    [JsonPropertyName("nome")]
    public string Nome { get; set; } = string.Empty;

    [JsonPropertyName("mensagem")]
    public string Mensagem { get; set; } = string.Empty;

    [JsonPropertyName("templateId")]
    public int? TemplateId { get; set; }

    [JsonPropertyName("templateNome")]
    public string? TemplateNome { get; set; }

    [JsonPropertyName("instance")]
    public string Instance { get; set; } = string.Empty;
}

public class EnvioUnitarioResponse
{
    [JsonPropertyName("id")]
    public int? Id { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("telefone")]
    public string Telefone { get; set; } = string.Empty;

    [JsonPropertyName("mensagem")]
    public string Mensagem { get; set; } = string.Empty;

    [JsonPropertyName("erro")]
    public string? Erro { get; set; }

    [JsonPropertyName("evolucaoId")]
    public string? EvolucaoId { get; set; }

    [JsonPropertyName("evolution_id")]
    public string? EvolutionId { get; set; }
}

public class ContatoMassaDto
{
    [JsonPropertyName("nome")]
    public string? Nome { get; set; }

    [JsonPropertyName("telefone")]
    public string Telefone { get; set; } = string.Empty;

    [JsonPropertyName("dados")]
    public Dictionary<string, string>? Dados { get; set; }
}

public class EnvioMassaRequest
{
    public bool UsarImagemTemplate { get; set; } = true;

    public ImagemEnvioDto? Imagem { get; set; }

    [JsonPropertyName("contatos")]
    public List<ContatoMassaDto> Contatos { get; set; } = new();

    [JsonPropertyName("mensagem")]
    public string Mensagem { get; set; } = string.Empty;

    [JsonPropertyName("intervaloMs")]
    public int? IntervaloMs { get; set; }

    [JsonPropertyName("templateId")]
    public int? TemplateId { get; set; }

    [JsonPropertyName("templateNome")]
    public string? TemplateNome { get; set; }

    [JsonPropertyName("instance")]
    public string Instance { get; set; } = string.Empty;
}

public class EnvioMassaResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
}

public class EnvioResponse
{
    [JsonIgnore]
    public int? UsuarioId { get; set; }

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("tipo")]
    public string Tipo { get; set; } = string.Empty;

    [JsonPropertyName("template_id")]
    public int? TemplateId { get; set; }

    [JsonPropertyName("template_nome")]
    public string? TemplateNome { get; set; }

    [JsonPropertyName("intervalo_ms")]
    public int? IntervaloMs { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("enviados")]
    public int Enviados { get; set; }

    [JsonPropertyName("erros")]
    public int Erros { get; set; }

    [JsonPropertyName("pendentes")]
    public int Pendentes { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("instancia")]
    public string? Instancia { get; set; }

    [JsonPropertyName("numero_origem")]
    public string? NumeroOrigem { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("finished_at")]
    public DateTime? FinishedAt { get; set; }
}

public class EnvioDetalheResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("envio_id")]
    public int EnvioId { get; set; }

    [JsonPropertyName("grupo_importacao_id")]
    public int? GrupoImportacaoId { get; set; }

    [JsonPropertyName("usuario_id")]
    public int? UsuarioId { get; set; }

    [JsonPropertyName("nome")]
    public string? Nome { get; set; }

    [JsonPropertyName("telefone")]
    public string Telefone { get; set; } = string.Empty;

    [JsonPropertyName("mensagem")]
    public string? Mensagem { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("erro")]
    public string? Erro { get; set; }

    [JsonPropertyName("evolution_id")]
    public string? EvolutionId { get; set; }

    [JsonPropertyName("numero_origem")]
    public string? NumeroOrigem { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("enviado_em")]
    public DateTime? EnviadoEm { get; set; }

    [JsonPropertyName("tipo")]
    public string? Tipo { get; set; }

    [JsonPropertyName("template_nome")]
    public string? TemplateNome { get; set; }

    [JsonPropertyName("instancia")]
    public string? Instancia { get; set; }

    [JsonPropertyName("envio_origem")]
    public string? EnvioOrigem { get; set; }

    [JsonPropertyName("mensagem_id")]
    public int? MensagemId { get; set; }
}

public class EnvioMassaGetResponse
{
    [JsonPropertyName("envio")]
    public EnvioResponse Envio { get; set; } = new();

    [JsonPropertyName("detalhes")]
    public List<EnvioDetalheResponse> Detalhes { get; set; } = new();
}

public class HistoricoFiltroDto
{
    public int? Page { get; set; }
    public int? PerPage { get; set; }
    public string? Status { get; set; }
    public string? Telefone { get; set; }
    public string? Nome { get; set; }
    public string? Instancia { get; set; }
    public int? GrupoImportacaoId { get; set; }
    public string? De { get; set; }
    public string? Ate { get; set; }
}

public class HistoricoPaginadoResponse
{
    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("perPage")]
    public int PerPage { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("rows")]
    public List<EnvioDetalheResponse> Rows { get; set; } = new();
}

public class ErrorMessageResponse
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

public class GrupoImportacaoResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("usuario_id")]
    public int UsuarioId { get; set; }

    [JsonPropertyName("nome")]
    public string? Nome { get; set; }

    [JsonPropertyName("arquivo_nome")]
    public string? ArquivoNome { get; set; }

    [JsonPropertyName("total_linhas")]
    public int TotalLinhas { get; set; }

    [JsonPropertyName("validos")]
    public int Validos { get; set; }

    [JsonPropertyName("invalidos")]
    public int Invalidos { get; set; }

    [JsonPropertyName("duplicados")]
    public int Duplicados { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }
}

public class ContatoImportadoResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("grupo_id")]
    public int GrupoId { get; set; }

    [JsonPropertyName("nome")]
    public string? Nome { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("telefone_normalizado")]
    public string? TelefoneNormalizado { get; set; }

    [JsonPropertyName("telefone_original")]
    public string? TelefoneOriginal { get; set; }

    [JsonPropertyName("dados")]
    public Dictionary<string, object?>? Dados { get; set; }

    [JsonPropertyName("status_validacao")]
    public string StatusValidacao { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }
}

public class ImportarPlanilhaResponse
{
    [JsonPropertyName("grupo_id")]
    public int GrupoId { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("validos")]
    public int Validos { get; set; }

    [JsonPropertyName("invalidos")]
    public int Invalidos { get; set; }

    [JsonPropertyName("duplicados")]
    public int Duplicados { get; set; }
}

public class GrupoDetalhePaginadoResponse
{
    [JsonPropertyName("grupo")]
    public GrupoImportacaoResponse Grupo { get; set; } = new();

    [JsonPropertyName("contatos")]
    public List<ContatoImportadoResponse> Contatos { get; set; } = new();

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("perPage")]
    public int PerPage { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }
}

public class EnvioPorGrupoRequest
{
    public bool UsarImagemTemplate { get; set; } = true;

    public ImagemEnvioDto? Imagem { get; set; }

    [JsonPropertyName("templateId")]
    public int? TemplateId { get; set; }

    [JsonPropertyName("mensagem")]
    public string? Mensagem { get; set; }

    [JsonPropertyName("templateNome")]
    public string? TemplateNome { get; set; }

    [JsonPropertyName("intervaloMs")]
    public int? IntervaloMs { get; set; }

    [JsonPropertyName("instance")]
    public string Instance { get; set; } = string.Empty;
}

public class ContatoWhatsAppResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("instancia")]
    public string Instancia { get; set; } = string.Empty;

    [JsonPropertyName("telefone")]
    public string Telefone { get; set; } = string.Empty;

    [JsonPropertyName("nome")]
    public string? Nome { get; set; }

    [JsonPropertyName("nome_whatsapp")]
    public string? NomeWhatsApp { get; set; }

    [JsonPropertyName("foto_url")]
    public string? FotoUrl { get; set; }

    [JsonPropertyName("ultimo_acesso")]
    public DateTime? UltimoAcesso { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }
}

public class ConversaResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("contato_whatsapp_id")]
    public int? ContatoWhatsAppId { get; set; }

    [JsonPropertyName("usuario_id")]
    public int? UsuarioId { get; set; }

    [JsonPropertyName("instancia")]
    public string Instancia { get; set; } = string.Empty;

    [JsonPropertyName("telefone")]
    public string Telefone { get; set; } = string.Empty;

    [JsonPropertyName("ultima_mensagem")]
    public string? UltimaMensagem { get; set; }

    [JsonPropertyName("ultima_mensagem_em")]
    public DateTime? UltimaMensagemEm { get; set; }

    [JsonPropertyName("mensagens_nao_lidas")]
    public int MensagensNaoLidas { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("nome")]
    public string? Nome { get; set; }

    [JsonPropertyName("nome_whatsapp")]
    public string? NomeWhatsApp { get; set; }

    [JsonPropertyName("foto_url")]
    public string? FotoUrl { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }
}

public class ConversaDetalheResponse
{
    [JsonPropertyName("conversa")]
    public ConversaResponse Conversa { get; set; } = new();

    [JsonPropertyName("contato")]
    public ContatoWhatsAppResponse? Contato { get; set; }
}

public class ConversaPaginadaResponse
{
    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("perPage")]
    public int PerPage { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("rows")]
    public List<ConversaResponse> Rows { get; set; } = new();
}

public class MensagemResponse
{
    [JsonPropertyName("tem_imagem")]
    public bool TemImagem { get; set; }

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("conversa_id")]
    public int ConversaId { get; set; }

    [JsonPropertyName("usuario_id")]
    public int? UsuarioId { get; set; }

    [JsonPropertyName("envio_detalhe_id")]
    public int? EnvioDetalheId { get; set; }

    [JsonPropertyName("evolution_id")]
    public string? EvolutionId { get; set; }

    [JsonPropertyName("telefone")]
    public string Telefone { get; set; } = string.Empty;

    [JsonPropertyName("instancia")]
    public string Instancia { get; set; } = string.Empty;

    [JsonPropertyName("tipo")]
    public string Tipo { get; set; } = string.Empty;

    [JsonPropertyName("direcao")]
    public string Direcao { get; set; } = string.Empty;

    [JsonPropertyName("conteudo")]
    public string? Conteudo { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("data_mensagem")]
    public DateTime? DataMensagem { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("erro")]
    public string? Erro { get; set; }
}

public class MensagemPaginadaResponse
{
    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("perPage")]
    public int PerPage { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("rows")]
    public List<MensagemResponse> Rows { get; set; } = new();

    [JsonPropertyName("tem_mais_antigas")]
    public bool TemMaisAntigas { get; set; }
}

public class EnviarMensagemAtendimentoRequest
{
    [JsonPropertyName("texto")]
    public string Texto { get; set; } = string.Empty;
}

public class SincroniaStatusResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "PENDENTE";

    [JsonPropertyName("iniciado_em")]
    public DateTime? IniciadoEm { get; set; }

    [JsonPropertyName("finalizado_em")]
    public DateTime? FinalizadoEm { get; set; }

    [JsonPropertyName("total_contatos")]
    public int TotalContatos { get; set; }

    [JsonPropertyName("total_conversas")]
    public int TotalConversas { get; set; }

    [JsonPropertyName("total_mensagens")]
    public int TotalMensagens { get; set; }

    [JsonPropertyName("erro")]
    public string? Erro { get; set; }

    [JsonPropertyName("conversas_com_erro")]
    public int ConversasComErro { get; set; }
}

public class SincroniaMonitor
{
    private readonly object _lock = new();
    private SincroniaStatusResponse _ultimo = new() { Status = "PENDENTE" };

    public SincroniaStatusResponse UltimoStatus
    {
        get { lock (_lock) return _ultimo; }
    }

    public bool EstaRodando { get; private set; }

    public DateTime? IniciadoEm { get; private set; }

    public void MarcarInicio()
    {
        lock (_lock)
        {
            EstaRodando = true;
            IniciadoEm = DateTime.Now;
            _ultimo = new SincroniaStatusResponse
            {
                Status = "EM_ANDAMENTO",
                IniciadoEm = IniciadoEm,
                TotalContatos = _ultimo.TotalContatos,
                TotalConversas = _ultimo.TotalConversas,
                TotalMensagens = _ultimo.TotalMensagens,
            };
        }
    }

    public void AtualizarContadores(int totalContatos, int totalConversas, int totalMensagens, int conversasComErro = 0, string? erro = null)
    {
        lock (_lock)
        {
            _ultimo.TotalContatos = totalContatos;
            _ultimo.TotalConversas = totalConversas;
            _ultimo.TotalMensagens = totalMensagens;
            _ultimo.ConversasComErro = conversasComErro;
            if (!string.IsNullOrWhiteSpace(erro)) _ultimo.Erro = erro;
        }
    }

    public void MarcarFim(bool ok, string? erro = null, int conversasComErro = 0)
    {
        lock (_lock)
        {
            EstaRodando = false;
            _ultimo.Status = ok ? "OK" : "ERRO";
            _ultimo.FinalizadoEm = DateTime.Now;
            _ultimo.ConversasComErro = conversasComErro;
            if (!string.IsNullOrWhiteSpace(erro)) _ultimo.Erro = erro;
            else if (!ok && string.IsNullOrWhiteSpace(_ultimo.Erro)) _ultimo.Erro = "Falha na sincronização";
        }
    }

    public void Restaurar(SincroniaStatusResponse salvo)
    {
        if (salvo == null) return;
        lock (_lock)
        {
            _ultimo = new SincroniaStatusResponse
            {
                Status = salvo.Status,
                IniciadoEm = salvo.IniciadoEm,
                FinalizadoEm = salvo.FinalizadoEm,
                TotalContatos = salvo.TotalContatos,
                TotalConversas = salvo.TotalConversas,
                TotalMensagens = salvo.TotalMensagens,
                ConversasComErro = salvo.ConversasComErro,
                Erro = salvo.Erro
            };
            IniciadoEm = salvo.IniciadoEm;
            EstaRodando = string.Equals(salvo.Status, "EM_ANDAMENTO", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(salvo.Status, "SINCRONIZANDO", StringComparison.OrdinalIgnoreCase);
        }
    }
}
