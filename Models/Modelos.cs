namespace DisparoApi.Models;

public class Usuario
{
    public int Id { get; set; }
    public string? Nome { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "user";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public static class StatusEnvio
{
    public const string EmAndamento = "EM_ANDAMENTO";
    public const string Concluido = "CONCLUIDO";
    public const string Parado = "PARADO";
    public const string Pausado = "PAUSADO";
}

public static class StatusDetalhe
{
    public const string Pendente = "PENDENTE";
    public const string Enviando = "ENVIANDO";
    public const string Enviado = "ENVIADO";
    public const string Erro = "ERRO";
}

public static class TipoEnvio
{
    public const string Unitario = "UNITARIO";
    public const string Massa = "MASSA";
}

public class Template
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Mensagem { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class Envio
{
    public int Id { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public int? TemplateId { get; set; }
    public string? TemplateNome { get; set; }
    public int? IntervaloMs { get; set; }
    public int Total { get; set; }
    public int Enviados { get; set; }
    public int Erros { get; set; }
    public int Pendentes { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Instancia { get; set; }
    public string? NumeroOrigem { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
}

public class EnvioDetalhe
{
    public int Id { get; set; }
    public int EnvioId { get; set; }
    public int? GrupoImportacaoId { get; set; }
    public int? UsuarioId { get; set; }
    public string? Nome { get; set; }
    public string Telefone { get; set; } = string.Empty;
    public string? Mensagem { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Erro { get; set; }
    public string? EvolutionId { get; set; }
    public string? NumeroOrigem { get; set; }
    public int? MensagemId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? EnviadoEm { get; set; }
}

public class Configuracao
{
    public string Chave { get; set; } = string.Empty;
    public string Valor { get; set; } = string.Empty;
}

public enum EstadoConexao
{
    Open,
    Connecting,
    Close,
    Unknown
}

public class WhatsAppAccount
{
    public EstadoConexao State { get; set; }
    public bool Connected { get; set; }
    public string Instance { get; set; } = string.Empty;
    public string? Number { get; set; }
    public string? ProfileName { get; set; }
    public string? Qrcode { get; set; }
}

public class WhatsAppSummary
{
    public List<WhatsAppAccount> Contas { get; set; } = new();
    public int Conectadas { get; set; }
    public int Total { get; set; }
    public string? Numero { get; set; }
}

public class GrupoImportacao
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public string? Nome { get; set; }
    public string? ArquivoNome { get; set; }
    public int TotalLinhas { get; set; }
    public int Validos { get; set; }
    public int Invalidos { get; set; }
    public int Duplicados { get; set; }
    public DateTime CreatedAt { get; set; }
}

public static class StatusValidacaoContato
{
    public const string Valido = "VALIDO";
    public const string Invalido = "INVALIDO";
    public const string Duplicado = "DUPLICADO";
}

public class ContatoImportado
{
    public int Id { get; set; }
    public int GrupoId { get; set; }
    public int UsuarioId { get; set; }
    public string? Nome { get; set; }
    public string? Email { get; set; }
    public string? TelefoneNormalizado { get; set; }
    public string? TelefoneOriginal { get; set; }
    public string? Dados { get; set; }
    public string StatusValidacao { get; set; } = StatusValidacaoContato.Valido;
    public DateTime CreatedAt { get; set; }
}

public static class StatusMensagem
{
    public const string Pendente = "PENDENTE";
    public const string Enviada = "ENVIADA";
    public const string Entregue = "ENTREGUE";
    public const string Lida = "LIDA";
    public const string Erro = "ERRO";
}

public static class DirecaoMensagem
{
    public const string Recebida = "RECEBIDA";
    public const string Enviada = "ENVIADA";
}

public static class TipoMensagem
{
    public const string Texto = "TEXTO";
    public const string Imagem = "IMAGEM";
    public const string Audio = "AUDIO";
    public const string Video = "VIDEO";
    public const string Documento = "DOCUMENTO";
    public const string Outro = "OUTRO";
}

public static class StatusConversa
{
    public const string Aberta = "ABERTA";
    public const string Fechada = "FECHADA";
}

public class ContatoWhatsApp
{
    public int Id { get; set; }
    public int? UsuarioId { get; set; }
    public string Instancia { get; set; } = string.Empty;
    public string Telefone { get; set; } = string.Empty;
    public string? Nome { get; set; }
    public string? NomeWhatsApp { get; set; }
    public string? FotoUrl { get; set; }
    public DateTime? UltimoAcesso { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class Conversa
{
    public int Id { get; set; }
    public int? ContatoWhatsAppId { get; set; }
    public int? UsuarioId { get; set; }
    public string Instancia { get; set; } = string.Empty;
    public string Telefone { get; set; } = string.Empty;
    public string? UltimaMensagem { get; set; }
    public DateTime? UltimaMensagemEm { get; set; }
    public int MensagensNaoLidas { get; set; }
    public string Status { get; set; } = StatusConversa.Aberta;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class Mensagem
{
    public int Id { get; set; }
    public int ConversaId { get; set; }
    public int? UsuarioId { get; set; }
    public int? EnvioDetalheId { get; set; }
    public string? EvolutionId { get; set; }
    public string Telefone { get; set; } = string.Empty;
    public string Instancia { get; set; } = string.Empty;
    public string Tipo { get; set; } = TipoMensagem.Texto;
    public string Direcao { get; set; } = DirecaoMensagem.Recebida;
    public string? Conteudo { get; set; }
    public string Status { get; set; } = StatusMensagem.Pendente;
    public DateTime? DataMensagem { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Erro { get; set; }
}
