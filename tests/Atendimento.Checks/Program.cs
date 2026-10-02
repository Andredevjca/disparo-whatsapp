using System.Net;
using System.Text;
using System.Text.Json;
using System.Reflection;
using DisparoApi.Services;
using DisparoApi.Options;
using DisparoApi.Dtos;
using DisparoApi.Data;
using DisparoApi.Repositories;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;

void Check(bool ok, string text) { if (!ok) throw new Exception(text); }
T Stub<T>(Func<MethodInfo, object?[], object?> call) where T : class { var x=DispatchProxy.Create<T, Proxy>(); ((Proxy)(object)x).Call=call; return x; }
var handler=new Handler();
var api=new EvolutionApiService(new HttpClient(handler), Options.Create(new EvolutionOptions { Url="https://example.test", ApiKey="test" }));
handler.Json="[{\"remoteJid\":\"5585999999999@s.whatsapp.net\",\"pushName\":\"Cliente\"}]";
Check((await api.ListarContatosEvolutionAsync("conta")).Count==1,"Contatos em array direto");
Check(handler.Path=="/chat/findContacts/conta" && handler.Method==HttpMethod.Post,"POST de contatos");
Check((await api.ListarConversasEvolutionAsync("conta")).Count==1,"Conversas em array direto");
const string jid="5585999999999@s.whatsapp.net";
var message=new {key=new {id="m1",remoteJid=jid,fromMe=false},message=new {conversation="Resposta"}};
foreach(var envelope in new object[] {new[]{message},new {messages=new[]{message}},new {data=new {messages=new {records=new[]{message},pages=2}}},new {rows=new[]{message}}}) {
    handler.Json=JsonSerializer.Serialize(envelope);
    var (rows,more)=await api.ListarPaginaMensagensEvolutionAsync("conta",jid,1,100);
    Check(rows.Count==1 && rows[0].GetProperty("key").GetProperty("id").GetString()=="m1","Historico em formatos suportados");
    using var body=JsonDocument.Parse(handler.Body);
    Check(body.RootElement.GetProperty("offset").GetInt32()==100,"Tamanho da pagina enviado como offset");
    if(handler.Json.Contains("pages"))Check(more,"Paginas do envelope respeitadas mesmo com menos de 100 mensagens");
}
handler.Json=JsonSerializer.Serialize(new {messages=new {records=new[]{message},pages=1}});
Check(!(await api.ListarPaginaMensagensEvolutionAsync("conta",jid,1)).temMais,"Ultima pagina");
Check((await api.ListarPaginaMensagensEvolutionAsync("conta","5511999999999@s.whatsapp.net",1)).mensagens.Count==0,"Nao misturar contatos se Evolution ignorar filtro");
handler.Json="[]"; handler.MissingPrimary=true;
Check((await api.ListarContatosEvolutionAsync("conta")).Count==0 && handler.Method==HttpMethod.Get,"Fallback para rota legada");
var calls=new List<object?[]>(); var names=new List<object?>(); var updates=0;
var repo=Stub<IAtendimentoRepository>((m,a)=>m.Name switch {
    "CriarOuObterContatoAsync" => Contact(a),
    "CriarOuObterConversaAsync" => Task.FromResult(new ConversaResponse { Id=2 }),
    "ObterContatoImportadoPorTelefoneAsync" => Task.FromResult<(int? contatoImportadoId,string? nomeImportado)?>(null),
    "InserirMensagemSeNaoExistirAsync" => Insert(a),
    "AtualizarConversaPosMensagemAsync" => Task.CompletedTask,
    "AtualizarStatusMensagemPorEvolutionIdAsync" => Update(),
    _ => throw new Exception(m.Name)
});
Task<ContatoWhatsAppResponse> Contact(object?[] a) { names.Add(a[2]);return Task.FromResult(new ContatoWhatsAppResponse {Id=1}); }
Task<(bool,int?)> Insert(object?[] a) { calls.Add(a);return Task.FromResult<(bool,int?)>((true,1)); }
Task Update() { updates++;return Task.CompletedTask; }
var db=Stub<IDbConnectionFactory>((m,a)=>throw new InvalidOperationException("No database"));
var service=new AtendimentoService(repo,api,new SincroniaMonitor(),Options.Create(new EvolutionOptions()),db);
async Task Webhook(object data,string ev="messages.upsert") { using var doc=JsonDocument.Parse(JsonSerializer.Serialize(new {@event=ev,data}));await service.ProcessarWebhookEventoAsync("conta",doc,NullLogger.Instance); }
foreach(var address in new[]{"123@g.us","123@lid","status@broadcast","123@newsletter"})
    await Webhook(new {key=new {id="ignored",remoteJid=address},message=new {conversation="Ignorar"}});
Check(calls.Count==0,"Grupos, LID sem telefone e status nao viram contatos");
await Webhook(new {key=new {id="sent",remoteJid=jid,fromMe=true},pushName="Meu nome",message=new {conversation="Oi"}});
Check(names.Last()==null,"Mensagem enviada nao substitui nome do cliente pelo meu");
await Webhook(new {key=new {id="image",remoteJid=jid,fromMe=false},sender="conta",message=new {imageMessage=new {caption="Foto"}}});
Check((string)calls.Last()[6]! == "IMAGEM" && (string)calls.Last()[8]! == "Foto","Imagem identificada com legenda");
await Webhook(new {id="sent",status="READ"},"messages.update");Check(updates==1,"Update com id na raiz");
var monitor=new SincroniaMonitor();monitor.Restaurar(new SincroniaStatusResponse {Status="EM_ANDAMENTO"});
Check(!monitor.EstaRodando && monitor.UltimoStatus.Status=="ERRO","Reinicio nao trava sincronizacao");
Check(monitor.MarcarInicio() && !monitor.MarcarInicio(),"Inicio atomico");
monitor.Restaurar(new SincroniaStatusResponse {Status="OK"});Check(monitor.EstaRodando,"Restauracao tardia nao sobrescreve execucao");
Console.WriteLine("OK: rotas, arrays, envelopes, paginacao, isolamento de contatos, webhook, tipos, nomes e monitor.");
public class Handler:HttpMessageHandler {
    public string Json="[]",Body="",Path="";public HttpMethod? Method;public bool MissingPrimary;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req,CancellationToken ct) {
        Path=req.RequestUri!.AbsolutePath;Method=req.Method;Body=req.Content==null?"":await req.Content.ReadAsStringAsync(ct);
        return new HttpResponseMessage(MissingPrimary && req.Method==HttpMethod.Post ? HttpStatusCode.MethodNotAllowed : HttpStatusCode.OK){Content=new StringContent(Json,Encoding.UTF8,"application/json")};
    }
}
public class Proxy:DispatchProxy {public Func<MethodInfo,object?[],object?> Call=null!;protected override object? Invoke(MethodInfo? m,object?[]? a)=>Call(m!,a??[]);}
