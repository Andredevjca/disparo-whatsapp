using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Dapper;
using DisparoApi.Data;
using DisparoApi.Options;
using DisparoApi.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

// Diagnóstico explícito: somente SELECTs e GETs. Não envia mensagens nem aplica esquema.
var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
var config = new ConfigurationBuilder().SetBasePath(root).AddJsonFile("appsettings.json").AddJsonFile("appsettings.Development.json", true).AddEnvironmentVariables().Build();
var factory = new DbConnectionFactory(config.GetConnectionString("DefaultConnection")!);
await using var connection = factory.Create();
await connection.OpenAsync();
Console.WriteLine("OK: conexão com o banco MySQL existente.");
foreach (var table in new[] { "usuarios", "templates", "template_imagens", "envios", "envio_imagens", "envios_detalhes", "grupos_importacoes", "contatos_importados", "conversas", "mensagens", "configuracoes" })
{
    var count = await connection.ExecuteScalarAsync<long>($"SELECT COUNT(*) FROM `{table}`");
    Console.WriteLine($"OK: tabela {table} acessível ({count} registros).");
}
var evoOptions = config.GetSection("Evolution").Get<EvolutionOptions>()!;
using var evoClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
var evo = new EvolutionApiService(evoClient, Options.Create(evoOptions));
try
{
    var accounts = await evo.ListarContasAsync();
    Console.WriteLine($"OK: Evolution respondeu; {accounts.Count} instância(s), {accounts.Count(c => c.Connected)} conectada(s).");
}
catch (Exception ex) { Console.WriteLine($"FALHA: leitura da Evolution ({ex.GetType().Name})."); }

var user = await connection.QueryFirstOrDefaultAsync<(int Id, string Email, string Role)>("SELECT id, email, role FROM usuarios ORDER BY id LIMIT 1");
if (user.Id == 0) throw new Exception("Não há usuário existente para verificar as rotas autenticadas.");
var secret = config["Jwt:Secret"]!;
var token = new JwtSecurityToken(claims: new[] { new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Email, user.Email), new Claim(ClaimTypes.Name, "Verificação local"), new Claim(ClaimTypes.Role, user.Role) },
    expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), SecurityAlgorithms.HmacSha256));
using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri("http://localhost:5185"), Timeout = TimeSpan.FromSeconds(45) };
var anonymous = await client.GetAsync("/");
if (anonymous.StatusCode != HttpStatusCode.Redirect) throw new Exception("A página inicial deve exigir login.");
var apiAnonymous = await client.GetAsync("/api/modelos");
if (apiAnonymous.StatusCode != HttpStatusCode.Unauthorized) throw new Exception("A API deve exigir autenticação.");
var login = await client.GetStringAsync("/Conta/Entrar");
if (!login.Contains("__RequestVerificationToken")) throw new Exception("Login sem proteção CSRF.");
Console.WriteLine("OK: login Razor, CSRF e bloqueio de acesso anônimo.");
client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
foreach (var route in new[] { "/", "/WhatsApp", "/Enviar", "/Massa", "/Modelos", "/Historico", "/Atendimento", "/Usuarios", "/Configuracoes" })
{
    using var response = await client.GetAsync(route);
    var html = await response.Content.ReadAsStringAsync();
    if (!response.IsSuccessStatusCode || !html.Contains("csrf-token") || !html.Contains("sidebar-menu")) throw new Exception($"Falha na renderização de {route}: {response.StatusCode}");
    Console.WriteLine($"OK: Razor {route} renderizado com layout e CSRF.");
}
foreach (var route in new[] { "modelos", "historico?perPage=1", "importacoes", "usuarios", "configuracao", "atendimento/conversas?perPage=1", "atendimento/sincronizar/status" })
{
    using var response = await client.GetAsync("/api/" + route);
    if (!response.IsSuccessStatusCode) throw new Exception($"Falha na leitura de /api/{route}: {response.StatusCode}");
    using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    Console.WriteLine($"OK: GET /api/{route}.");
}
Console.WriteLine("Diagnóstico concluído. Nenhuma mensagem enviada ou dado alterado.");
