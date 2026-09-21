using System.Text.Json;
using DisparoApi.Models;

namespace DisparoApi.Services;

public interface IEvolutionApiService
{
    Task<List<WhatsAppAccount>> ListarContasAsync();
    Task<WhatsAppAccount> GarantirInstanciaEConectarAsync(string instance);
    Task<WhatsAppAccount> ConectarAsync(string instance);
    Task DesconectarAsync(string instance);
    Task RemoverInstanciaAsync(string instance);
    Task<(bool ok, string? evolutionId, string? numeroOrigem, string? erro)> EnviarMensagemAsync(string instance, string telefone, string mensagem, DisparoApi.Dtos.ImagemEnvioDto? imagem = null);

    Task<List<(string remoteJid, string? pushName, string? nome, string? fotoPerfil)>> ListarContatosEvolutionAsync(string instance);
    Task<List<(string remoteJid, DateTime? ultimaMensagemEm, string? ultimaMensagemTexto, int? totalMensagens)>> ListarConversasEvolutionAsync(string instance);
    Task<(List<JsonElement> mensagens, bool temMais)> ListarPaginaMensagensEvolutionAsync(string instance, string remoteJid, int page, int perPage = 100);
}
