const fs=require('fs');
const assert=require('assert').strict;
const source=fs.readFileSync('wwwroot/js/disparo.js','utf8');
new Function(source.replace('export function mountPage','function mountPage'));
const code=source.slice(source.indexOf('async function atendimento()'),source.indexOf('const pages='));
const handlers=new Map(),elements=new Map();
const form={texto:{value:''}};elements.set('#chat-form',form);
for(const id of ['#conversation-search','#chat-messages','#conversations','#chat-header','#chat-text','#sync-status','#sync','#sync-instance','#force-sync','button'])elements.set(id,{value:'',innerHTML:'',scrollHeight:100,scrollTop:100,clientHeight:100});
elements.get('#conversation-search').busca={value:''};
const $=s=>elements.get(s)||null;
let pollTask,resolveSend,sent=0,reads=[];
const conversations=[{id:1,nome:'A',telefone:'111',instancia:'conta'},{id:2,nome:'B',telefone:'222',instancia:'conta'}];
async function api(path,method='GET',body) {
    if(path.startsWith('atendimento/conversas?'))return {rows:conversations,page:1,perPage:30,total:2};
    if(path.endsWith('/ler')){reads.push(path);return {};}
    if(path.endsWith('/mensagens') && method==='POST'){sent++;return new Promise(r=>resolveSend=r);}
    if(path.includes('/mensagens?'))return {rows:[],tem_mais_antigas:false};
    if(path==='atendimento/sincronizar/status')return {status:'OK'};
    throw new Error(path);
}
const start=new Function('$','api','on','actions','poll','pager','accountOptions','feedback','escapeHtml','date','number','empty','document',code+'; return atendimento();');
(async()=>{
    await start($,api,(selector,event,fn)=>handlers.set(selector,fn),(selector,fn)=>handlers.set(selector,fn),fn=>pollTask=fn,()=>{},async()=>{throw new Error('Conta temporariamente indisponivel');},()=>{},String,String,String,String,{hidden:false});
    assert.equal(typeof pollTask,'function','Falha de contas nao interrompe atualizacao');
    await handlers.get('#conversations')('open','1');
    form.texto.value='Mensagem para A';
    const pending=handlers.get('#chat-form')();
    await handlers.get('#conversations')('open','2');
    form.texto.value='Rascunho de B';
    await handlers.get('#chat-form')();assert.equal(sent,1,'Bloqueia segundo envio durante requisicao');
    resolveSend({status:'ENVIADA'});await pending;
    assert.equal(form.texto.value,'Rascunho de B','Envio de A nao apaga texto de B');
    await handlers.get('#conversations')('open','1');assert.equal(form.texto.value,'','Texto enviado de A nao fica no rascunho');
    await handlers.get('#conversations')('open','2');assert.equal(form.texto.value,'Rascunho de B','Rascunho preservado por conversa');
    const count=reads.length;await pollTask();assert.ok(reads.length>count,'Conversa aberta atualiza leitura');
    console.log('OK: JavaScript, troca durante envio, rascunhos, envio concorrente, leitura e polling resiliente.');
})().catch(e=>{console.error(e);process.exitCode=1;});
