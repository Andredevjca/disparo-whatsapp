using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace DisparoApi.Controllers;

[Authorize]
public class PainelController : Controller
{
    public IActionResult Index() => Tela("Dashboard", "Dashboard", "Visão geral dos envios e das conexões.");
    [HttpGet("/WhatsApp")]
    public IActionResult WhatsApp() => Tela("WhatsApp", "Contas WhatsApp", "Gerencie suas instâncias e conecte pelo QR Code.");
    [HttpGet("/Enviar")]
    public IActionResult Enviar() => Tela("Enviar", "Envio individual", "Envie mensagens e imagens pelo WhatsApp.");
    [HttpGet("/Massa")]
    public IActionResult Massa() => Tela("Massa", "Disparos em massa", "Importe contatos, personalize mensagens e acompanhe os envios.");
    [HttpGet("/Modelos")]
    public IActionResult Modelos() => Tela("Modelos", "Modelos de mensagem", "Organize mensagens reutilizáveis com variáveis e imagens.");
    [HttpGet("/Historico")]
    public IActionResult Historico() => Tela("Historico", "Histórico de envios", "Consulte mensagens enviadas, resultados e detalhes.");
    [HttpGet("/Atendimento")]
    public IActionResult Atendimento() => Tela("Atendimento", "Atendimento", "Acompanhe as conversas e responda seus contatos.");
    [HttpGet("/Usuarios")]
    public IActionResult Usuarios() => Tela("Usuarios", "Usuários", "Gerencie os acessos ao sistema.");
    [HttpGet("/Configuracoes")]
    public IActionResult Configuracoes() => Tela("Configuracoes", "Configurações", "Defina o intervalo padrão entre os envios.");
    private ViewResult Tela(string pasta, string title, string description)
    {
        ViewData["Title"] = title;
        ViewData["Description"] = description;
        return View($"~/Views/{pasta}/Index.cshtml");
    }
}
