export function mountPage() {
const pageRoot = document.getElementById('main-content');
const pageName = document.body.dataset.page;
const controller = new AbortController();
const timers = new Set();
const $ = (s, root = pageRoot) => root.querySelector(s) || (['#connection-badge', 'meta[name="csrf-token"]'].includes(s) ? document.querySelector(s) : null);
const escapeHtml = value => String(value ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const date = value => value ? new Date(value).toLocaleString('pt-BR') : '—';
const number = value => Number(value || 0).toLocaleString('pt-BR');
const empty = (text = 'Nenhum registro encontrado.') => `<div class="empty-state"><i class="fa-regular fa-folder-open"></i><p class="mb-0">${escapeHtml(text)}</p></div>`;
const badge = status => {
    const tone = ['ENVIADO','ENVIADA','ENTREGUE','LIDA','VALIDO','CONCLUIDO','open','Conectada'].includes(status) ? 'ativo'
        : ['ERRO','INVALIDO','close','Desconectada'].includes(status) ? 'atrasado'
        : ['PENDENTE','PAUSADO','DUPLICADO'].includes(status) ? 'pendente'
        : ['EM_ANDAMENTO','ENVIANDO'].includes(status) ? 'info' : 'cancelado';
    return `<span class="status-badge status-${tone}">${escapeHtml(status || '—')}</span>`;
};
const table = (headers, rows) => {
    const actionColumn = headers.indexOf('Ações');
    return rows.length ? `<div class="table-wrap table-responsive"><table class="table table-modern table-hover mb-0"><thead><tr>${headers.map((h,index)=>`<th scope="col"${index===actionColumn?' class="table-actions"':''}>${h}</th>`).join('')}</tr></thead><tbody>${rows.map(r=>`<tr>${r.map((c,index)=>`<td${index===actionColumn?' class="table-actions"':''}>${index===actionColumn?`<div class="table-action-buttons">${c ?? ''}</div>`:c ?? '—'}</td>`).join('')}</tr>`).join('')}</tbody></table></div>` : empty();
};
function feedback(message, error = false) {
    if (controller.signal.aborted) return;
    const el = $('#feedback'); el.hidden = false; el.className = `alert alert-${error ? 'danger' : 'success'}`; el.textContent = message;
    if (error) el.scrollIntoView({behavior:'smooth', block:'nearest'});
}
async function api(path, method = 'GET', data) {
    controller.signal.throwIfAborted();
    const headers = {'Accept':'application/json'};
    if (method !== 'GET') headers['X-CSRF-TOKEN'] = $('meta[name="csrf-token"]').content;
    let body;
    if (data instanceof FormData) body = data;
    else if (data !== undefined) { headers['Content-Type'] = 'application/json'; body = JSON.stringify(data); }
    const response = await fetch(`/api/${path}`, {method, headers, body, credentials:'same-origin', signal:controller.signal});
    if (response.status === 401) { location.assign('/Conta/Entrar?returnUrl=' + encodeURIComponent(location.pathname + location.search)); throw new Error('Sua sessão expirou.'); }
    const text = await response.text();
    controller.signal.throwIfAborted();
    let result; try { result = text ? JSON.parse(text) : null; } catch { result = null; }
    if (!response.ok) throw new Error(result?.message || result?.erro || (result?.errors && Object.values(result.errors).flat().join(' ')) || `Não foi possível concluir a operação (${response.status}).`);
    return result;
}
function on(selector, event, handler) {
    $(selector)?.addEventListener(event, async e => {
        if (event === 'submit') e.preventDefault();
        const button = event === 'submit' ? $('button[type="submit"], button:not([type])', e.currentTarget) : e.currentTarget.closest('button');
        if (button?.disabled) return;
        if (button) button.disabled = true;
        try { await handler(e); } catch (err) { feedback(err.message || 'Falha na operação.', true); }
        finally { if (button) button.disabled = false; }
    });
}
function actions(selector, handler) {
    on(selector, 'click', async e => {
        const button = e.target.closest('[data-action]');
        if (!button || button.disabled) return;
        button.disabled = true;
        try { await handler(button.dataset.action, button.dataset.id, button); } finally { button.disabled = false; }
    });
}
const actionButton = (action, id, icon, label, danger = false) => {
    const style = danger ? 'btn-outline-danger' : action === 'edit' ? 'btn-icone-editar' : 'btn-outline-secondary';
    return `<button type="button" class="btn btn-sm btn-action ${style}" data-action="${action}" data-id="${escapeHtml(id)}" title="${escapeHtml(label)}" aria-label="${escapeHtml(label)}"><i class="fa-solid fa-${icon}" aria-hidden="true"></i></button>`;
};
function pager(selector, page, perPage, total, change) {
    const max = Math.max(1, Math.ceil(total / perPage)); const el = $(selector);
    el.innerHTML = `<div class="pager"><span>${number(total)} registros · Página ${page} de ${max}</span><div><button type="button" class="btn btn-outline-secondary me-1" data-page="${page-1}" ${page<=1?'disabled':''}>Anterior</button><button type="button" class="btn btn-outline-secondary" data-page="${page+1}" ${page>=max?'disabled':''}>Próxima</button></div></div>`;
    el.onclick = async e => { const b=e.target.closest('[data-page]'); if (!b || b.disabled) return; b.disabled=true; try { await change(Number(b.dataset.page)); } catch(err) { feedback(err.message,true); b.disabled=false; } };
}
function poll(task, delay) {
    if (controller.signal.aborted) return null;
    let busy = false;
    const id = setInterval(async()=>{if(busy || document.hidden) return; busy=true; try {await task();} catch {} finally {busy=false;}},delay);
    timers.add(id); return id;
}
let accounts = [];
async function loadAccounts() {
    accounts = await api('whatsapp/contas');
    const connected=accounts.filter(c=>c.connected).length;
    $('#connection-badge').textContent = `${connected} de ${accounts.length} conta(s) conectada(s)`;
    $('#connection-badge').className = `badge rounded-pill text-decoration-none text-bg-${connected?'success':'secondary'}`;
    return accounts;
}
async function accountOptions(selector, onlyConnected = true) {
    const list = await loadAccounts(); const el=$(selector); if(!el) return;
    const current=el.value; const available=list.filter(c=>!onlyConnected || c.connected);
    el.innerHTML = `<option value="">${available.length?'Selecione uma conta':'Nenhuma conta conectada'}</option>` + available.map(c=>`<option value="${escapeHtml(c.instance)}">${escapeHtml(c.profileName || c.instance)}${c.number?' · '+escapeHtml(c.number):''}</option>`).join('');
    if (available.some(c=>c.instance===current)) el.value=current; else if (available.length===1) el.value=available[0].instance;
}
let image = null;
function setImage(value) {
    image=value; if(!$('#image-area')) return;
    $('#image-area').hidden=!value;
    if(value && ['image/png','image/jpeg'].includes(value.mimeType)) $('#image-preview').src=`data:${value.mimeType};base64,${value.base64}`;
    else $('#image-preview').removeAttribute('src');
    $('#image-file').value='';
}
function imageInput() {
    on('#image-file','change',async e=>{
        const file=e.target.files[0]; if(!file) return;
        if(!['image/png','image/jpeg'].includes(file.type) || file.size>5*1024*1024 || !file.size) {e.target.value=''; throw new Error('Selecione uma imagem PNG ou JPEG de até 5 MB.');}
        const buttons=[...pageRoot.querySelectorAll('#send-form button, #bulk-form button, #template-form button')]; buttons.forEach(b=>b.disabled=true);
        try {
            const data=await new Promise((resolve,reject)=>{const reader=new FileReader();reader.onload=()=>resolve(reader.result);reader.onerror=()=>reject(new Error('Não foi possível ler a imagem.'));reader.readAsDataURL(file);});
            setImage({base64:data.split(',')[1],mimeType:file.type,nomeArquivo:file.name});
        } finally {buttons.forEach(b=>b.disabled=false);}
    });
    on('#remove-image','click',()=>setImage(null));
}
let templates=[];
let previewData={};
function preview() {
    const target=$('#message-preview'); if(!target) return;
    const data={...previewData, ...($('#send-form') ? Object.fromEntries(new FormData($('#send-form'))) : {})};
    const normalized=Object.fromEntries(Object.entries(data).map(([key,value])=>[key.toLowerCase(),value]));
    target.textContent=($('#message')?.value || '').replace(/{{\s*([^{}]+?)\s*}}/g,(match,key)=>normalized[key.toLowerCase()] ?? match) || 'Sua mensagem aparecerá aqui.';
}
async function composer() {
    imageInput();
    const results=await Promise.allSettled([accountOptions('#instance'),api('modelos')]);
    if(results[1].status==='fulfilled') templates=results[1].value;
    $('#template').innerHTML='<option value="">Mensagem personalizada</option>'+templates.map(t=>`<option value="${t.id}">${escapeHtml(t.nome)}</option>`).join('');
    on('#template','change',e=>{const t=templates.find(t=>t.id===Number(e.target.value));if(t){$('#message').value=t.mensagem;setImage(t.imagem || null);}preview();});
    on('#message','input',preview); on('#contact-name','input',preview);on('#phone','input',preview);
    for(const result of results) if(result.status==='rejected') feedback(result.reason.message,true);
}
function composeData() {
    if(!$('#instance').value) throw new Error('Selecione uma conta WhatsApp conectada.');
    if(!$('#message').value.trim() && !image) throw new Error('Digite uma mensagem ou selecione uma imagem.');
    const template=templates.find(t=>t.id===Number($('#template').value));
    return {instance:$('#instance').value,mensagem:$('#message').value,templateId:template?.id || null,templateNome:template?.nome || null,imagem:image,usarImagemTemplate:false};
}
const historyRows = (rows, details = false) => table(['Contato','Telefone','Mensagem','Status','Data',...(details?['Ações']:[])],rows.map(r=>[
    escapeHtml(r.nome || '—'),escapeHtml(r.telefone),`<div class="message-excerpt" title="${escapeHtml(r.mensagem)}">${escapeHtml(r.mensagem || 'Imagem / mídia')}</div>`,badge(r.status),escapeHtml(date(r.enviado_em || r.created_at)),...(details?[actionButton('detail',r.id,'eye','Ver detalhes')]:[])
]));
async function dashboard() {
    const results=await Promise.allSettled([api('historico?perPage=8&page=1'),api('importacoes'),loadAccounts()]);
    const history=results[0].status==='fulfilled'?results[0].value:null;
    const groups=results[1].status==='fulfilled'?results[1].value:null;
    const connected=results[2].status==='fulfilled'?accounts.filter(c=>c.connected).length:null;
    const stats=[['fa-paper-plane','Registros no histórico',history?.total],['fa-address-book','Grupos de contatos',groups?.length],['fa-users','Contatos válidos',groups?.reduce((s,g)=>s+g.validos,0)],['fa-link','Contas conectadas',connected]];
    $('#dashboard-stats').innerHTML=stats.map(([icon,label,value])=>`<div class="col-sm-6 col-xl-3"><div class="card stat-card"><div class="stat-top"><span class="text-muted">${label}</span><div class="stat-icon"><i class="fa-solid ${icon}" aria-hidden="true"></i></div></div><div class="stat-bottom"><div class="stat-value">${value==null?'—':number(value)}</div><span class="stat-caption">${value==null?'Indisponível':'No workspace'}</span></div></div></div>`).join('');
    $('#recent-history').innerHTML=history?historyRows(history.rows):empty('Não foi possível carregar o histórico.');
    $('#dashboard-accounts').innerHTML=results[2].status==='fulfilled'?(accounts.map(c=>`<div class="d-flex justify-content-between gap-2 py-2"><span>${escapeHtml(c.profileName || c.instance)}</span>${badge(c.connected?'Conectada':'Desconectada')}</div>`).join('') || empty('Adicione sua primeira conta WhatsApp.')):empty('Evolution indisponível.');
    results.filter(r=>r.status==='rejected').forEach(r=>feedback(r.reason.message,true));
}
async function whatsapp() {
    let qrInstance=null; const modal=new bootstrap.Modal($('#qr-modal'));
    const refresh=async()=>{
        await loadAccounts();
        $('#accounts').innerHTML=accounts.length?accounts.map(c=>`<div class="col-md-6 col-xl-4"><section class="card card-body"><div class="d-flex justify-content-between mb-3"><div class="stat-icon"><i class="fa-brands fa-whatsapp"></i></div>${badge(c.connected?'Conectada':'Desconectada')}</div><h2>${escapeHtml(c.profileName || c.instance)}</h2><p class="text-muted">${escapeHtml(c.number || 'Número não conectado')}</p><p class="small">Instância: ${escapeHtml(c.instance)}</p><div>${actionButton(c.connected?'disconnect':'connect',c.instance,c.connected?'unlink':'qrcode',c.connected?'Desconectar':'Conectar')}${actionButton('delete',c.instance,'trash-can','Excluir conta',true)}<span class="text-muted">${c.connected?'Conexão ativa':'Conecte pelo QR Code'}</span></div></section></div>`).join(''):`<div class="col-12">${empty('Nenhuma conta cadastrada. Adicione uma conta acima.')}</div>`;
        if(qrInstance && accounts.some(c=>c.instance===qrInstance && c.connected)){modal.hide();feedback('WhatsApp conectado com sucesso.');qrInstance=null;}
    };
    const showQr=c=>{
        if(c.connected){feedback('Conta já conectada.');return;}
        qrInstance=c.instance;$('#qr-content').replaceChildren();
        if(c.qrcode){
            if(c.qrcode.length>100){const img=document.createElement('img');img.className='qr-image';img.alt='QR Code para conectar WhatsApp';img.src=c.qrcode.startsWith('data:image/')?c.qrcode:`data:image/png;base64,${c.qrcode}`;$('#qr-content').append(img);}
            else $('#qr-content').textContent=c.qrcode;
        }else $('#qr-content').textContent='QR Code ainda indisponível. Feche e clique em conectar para tentar novamente.';
        modal.show();
    };
    on('#account-form','submit',async e=>{const c=await api('whatsapp/contas','POST',{nome:e.target.nome.value});e.target.reset();showQr(c);await refresh();});
    on('#refresh-accounts','click',refresh);
    actions('#accounts',async(action,id)=>{
        const path=`whatsapp/contas/${encodeURIComponent(id)}`;
        if(action==='connect')showQr(await api(path+'/conectar','POST',{}));
        if(action==='disconnect' && confirm(`Desconectar a conta ${id}?`))await api(path+'/desconectar','POST',{});
        if(action==='delete' && confirm(`Excluir a instância ${id} da Evolution?`))await api(path,'DELETE');
        await refresh();
    });
    $('#qr-modal').addEventListener('hidden.bs.modal',()=>qrInstance=null);
    poll(refresh,8000);await refresh();
}
async function send() {
    await composer();
    on('#send-form','submit',async e=>{
        const data={...composeData(),nome:e.target.nome.value,telefone:e.target.telefone.value};
        const result=await api('envios/unitario','POST',data);
        $('#send-result').innerHTML=`<div class="card card-body"><h2>Resultado do envio</h2><p>${badge(result.status)}</p><p>${escapeHtml(result.telefone)}</p><p class="mb-0">${escapeHtml(result.erro || 'Mensagem processada. Consulte o histórico para mais detalhes.')}</p></div>`;
        feedback(result.erro || 'Envio processado.',result.status==='ERRO');
    });
}
async function modelos() {
    imageInput();const form=$('#template-form');
    const reset=()=>{form.reset();form.elements.id.value='';setImage(null);$('#template-title').textContent='Novo modelo';};
    const refresh=async()=>{templates=await api('modelos');$('#templates').innerHTML=table(['Nome','Mensagem','Imagem','Ações'],templates.map(t=>[escapeHtml(t.nome),`<div class="message-excerpt">${escapeHtml(t.mensagem)}</div>`,t.imagem?'<i class="fa-regular fa-image" aria-label="Com imagem"></i>':'—',actionButton('edit',t.id,'pen','Editar')+actionButton('delete',t.id,'trash-can','Excluir',true)]));};
    on('#template-form','submit',async()=>{const id=form.elements.id.value;await api(`modelos${id?'/'+id:''}`,id?'PUT':'POST',{nome:form.nome.value,mensagem:form.mensagem.value,imagem:image});reset();await refresh();feedback('Modelo salvo.');});
    on('#reset-template','click',reset);
    actions('#templates',async(action,id)=>{if(action==='edit'){const t=templates.find(t=>t.id===Number(id));form.elements.id.value=t.id;form.nome.value=t.nome;form.mensagem.value=t.mensagem;setImage(t.imagem || null);$('#template-title').textContent='Editar modelo';form.nome.focus();}else if(confirm('Excluir este modelo?')){await api('modelos/'+id,'DELETE');reset();await refresh();feedback('Modelo excluído.');}});
    await refresh();
}
async function massa() {
    let groupId=null, group=null, page=1, jobId=null, jobTimer=null;
    await composer();
    const settings=await api('configuracao');$('#interval').value=settings.intervaloMs/1000;
    const refresh=async()=>{const groups=await api('importacoes');$('#groups').innerHTML=table(['Grupo','Total','Válidos','Inválidos','Duplicados','Importação','Ações'],groups.map(g=>[escapeHtml(g.nome || g.arquivo_nome),number(g.total_linhas),number(g.validos),number(g.invalidos),number(g.duplicados),date(g.created_at),actionButton('open',g.id,'folder-open','Abrir grupo')+actionButton('delete',g.id,'trash-can','Excluir grupo',true)]));};
    const contacts=async(p=1)=>{
        page=p;const data=await api(`importacoes/${groupId}?page=${p}&perPage=20&busca=${encodeURIComponent($('#contact-search').busca.value)}`);group=data.grupo;
        $('#group-detail').hidden=false;$('#group-title').textContent=`${group.nome || group.arquivo_nome} · ${number(group.validos)} contatos válidos`;
        $('#contacts').innerHTML=table(['Nome','Telefone','Status','Outros dados'],data.contatos.map(c=>[escapeHtml(c.nome),escapeHtml(c.telefone_normalizado || c.telefone_original),badge(c.status_validacao),escapeHtml(Object.entries(c.dados || {}).map(([k,v])=>`${k}: ${v}`).join(' · '))]));
        const contact=data.contatos.find(c=>c.status_validacao==='VALIDO'); previewData=contact?{...contact.dados,nome:contact.nome,email:contact.email,telefone:contact.telefone_normalizado}:{};preview();
        pager('#contacts-pager',page,20,data.total,contacts);
    };
    const updateJob=async()=>{
        const data=await api(`envios/massa/${jobId}`);const envio=data.envio;
        $('#bulk-progress').hidden=false;
        $('#bulk-summary').innerHTML=`${badge(envio.status)} <span class="ms-2">${number(envio.enviados)} enviados · ${number(envio.erros)} erros · ${number(envio.pendentes)} pendentes · Total ${number(envio.total)}</span>`;
        const progress=envio.total?Math.round((envio.enviados+envio.erros)/envio.total*100):0;
        $('#bulk-bar').style.width=progress+'%';$('#bulk-bar').parentElement.setAttribute('aria-valuenow',String(progress));
        $('#bulk-details').innerHTML=table(['Nome','Telefone','Status','Erro'],data.detalhes.map(d=>[escapeHtml(d.nome),escapeHtml(d.telefone),badge(d.status),escapeHtml(d.erro)]));
        const terminal=['CONCLUIDO','PARADO','PAUSADO','ERRO'].includes(envio.status);$('#stop-bulk').disabled=terminal;
        if(terminal && jobTimer){clearInterval(jobTimer);jobTimer=null;}
    };
    on('#import-form','submit',async e=>{const data=await api('importacoes/importar','POST',new FormData(e.target));e.target.reset();await refresh();feedback(`Importação concluída: ${data.validos} válidos, ${data.invalidos} inválidos e ${data.duplicados} duplicados.`);});
    actions('#groups',async(action,id)=>{if(action==='open'){groupId=Number(id);$('#contact-search').reset();await contacts();$('#group-detail').scrollIntoView({behavior:'smooth'});}else if(confirm('Excluir este grupo e seus contatos importados?')){await api('importacoes/'+id,'DELETE');if(groupId===Number(id)){$('#group-detail').hidden=true;groupId=null;}await refresh();feedback('Grupo excluído.');}});
    on('#contact-search','submit',()=>contacts());
    on('#bulk-form','submit',async()=>{
        if(!groupId || !group?.validos)throw new Error('Selecione um grupo com contatos válidos.');
        const data={...composeData(),intervaloMs:Number($('#interval').value)*1000};
        if(!confirm(`Iniciar o envio para ${group.validos} contatos pela conta ${data.instance}?`))return;
        const result=await api(`importacoes/${groupId}/enviar`,'POST',data);jobId=result.id;
        history.replaceState(null,'',`/Massa?envio=${jobId}`);if(jobTimer)clearInterval(jobTimer);jobTimer=poll(updateJob,2000);await updateJob();$('#bulk-progress').scrollIntoView({behavior:'smooth'});feedback('Disparo iniciado.');
    });
    on('#stop-bulk','click',async()=>{if(confirm('Parar os próximos envios deste disparo?')){await api(`envios/massa/${jobId}/parar`,'POST',{});await updateJob();}});
    await refresh();
    const existing=Number(new URLSearchParams(location.search).get('envio'));if(existing>0){jobId=existing;jobTimer=poll(updateJob,2000);await updateJob();}
}
async function historico() {
    const form=$('#history-filter');const modal=new bootstrap.Modal($('#detail-modal'));
    const refresh=async(page=1)=>{const params=new URLSearchParams();for(const [k,v] of new FormData(form))if(v)params.set(k,v);params.set('page',page);params.set('perPage',20);const data=await api('historico?'+params);$('#history').innerHTML=historyRows(data.rows,true);pager('#history-pager',data.page,data.perPage,data.total,refresh);};
    on('#history-filter','submit',()=>refresh());on('#history-filter','reset',()=>{setTimeout(()=>refresh().catch(e=>feedback(e.message,true)),0);});
    actions('#history',async(_,id)=>{const data=await api('historico/'+id);$('#detail-content').innerHTML=`<dl class="row">${[['Contato',data.nome],['Telefone',data.telefone],['Instância',data.instancia],['Origem',data.numero_origem],['Modelo',data.template_nome],['Status',data.status],['Data',date(data.enviado_em || data.created_at)],['ID Evolution',data.evolution_id],['Erro',data.erro]].map(([k,v])=>`<dt class="col-sm-3">${k}</dt><dd class="col-sm-9">${escapeHtml(v || '—')}</dd>`).join('')}</dl><div class="message-bubble">${escapeHtml(data.mensagem || 'Mensagem de mídia')}</div>${data.envio_id?`<a class="btn btn-outline-primary mt-3" href="/Massa?envio=${Number(data.envio_id)}">Acompanhar envio</a>`:''}`;modal.show();});
    await refresh();const groups=await api('importacoes');$('#filter-group').innerHTML='<option value="">Todos</option>'+groups.map(g=>`<option value="${g.id}">${escapeHtml(g.nome || g.arquivo_nome)}</option>`).join('');
}
async function usuarios() {
    const form=$('#user-form');let users=[];
    const reset=()=>{form.reset();form.elements.id.value='';form.password.required=true;$('#user-title').textContent='Novo usuário';};
    const refresh=async()=>{users=await api('usuarios');$('#users').innerHTML=table(['Nome','E-mail','Perfil','Ações'],users.map(u=>[escapeHtml(u.nome),escapeHtml(u.email),escapeHtml(u.role),actionButton('edit',u.id,'pen','Editar')+actionButton('delete',u.id,'trash-can','Excluir',true)]));};
    on('#user-form','submit',async()=>{const id=form.elements.id.value;await api('usuarios'+(id?'/'+id:''),id?'PUT':'POST',{nome:form.nome.value,email:form.email.value,password:form.password.value || null});reset();await refresh();feedback('Usuário salvo.');});
    on('#reset-user','click',reset);
    actions('#users',async(action,id)=>{if(action==='edit'){const u=users.find(u=>u.id===Number(id));form.elements.id.value=u.id;form.nome.value=u.nome || '';form.email.value=u.email;form.password.value='';form.password.required=false;$('#user-title').textContent='Editar usuário';form.nome.focus();}else if(confirm('Excluir este usuário?')){await api('usuarios/'+id,'DELETE');reset();await refresh();feedback('Usuário excluído.');}});await refresh();
}
async function configuracoes() {
    const result=await api('configuracao');$('#settings-interval').value=result.intervaloMs/1000;
    on('#settings-form','submit',async()=>{await api('configuracao','PUT',{intervaloMs:Number($('#settings-interval').value)*1000});feedback('Configurações salvas.');});
}
async function atendimento() {
    let conversations=[],selected=null,conversationPage=1,messagePage=1,messages=new Map();
    const form=$('#chat-form');
    const refresh=async(page=conversationPage)=>{
        conversationPage=page;const data=await api(`atendimento/conversas?page=${page}&perPage=30&busca=${encodeURIComponent($('#conversation-search').busca.value)}`);conversations=data.rows;
        $('#conversations').innerHTML=conversations.map(c=>`<button type="button" class="conversation ${selected?.id===c.id?'active':''}" data-action="open" data-id="${c.id}"><div class="d-flex justify-content-between gap-2"><strong>${escapeHtml(c.nome || c.nome_whatsapp || c.telefone)}</strong>${c.mensagens_nao_lidas?`<span class="badge text-bg-success">${number(c.mensagens_nao_lidas)}</span>`:''}</div><span class="conversation-preview">${escapeHtml(c.ultima_mensagem || 'Sem mensagens')}</span><small class="text-muted">${escapeHtml(c.instancia)} · ${escapeHtml(date(c.ultima_mensagem_em))}</small></button>`).join('') || empty('Nenhuma conversa encontrada.');
        pager('#conversations-pager',data.page,data.perPage,data.total,refresh);
    };
    const loadMessages=async(older=false)=>{
        if(!selected)return;const id=selected.id;
        const requestedPage=older?messagePage+1:1;
        const data=await api(`atendimento/conversas/${id}/mensagens?page=${requestedPage}&perPage=50`);if(selected?.id!==id)return;
        if(older)messagePage=requestedPage;
        const target=$('#chat-messages');const atBottom=target.scrollHeight-target.scrollTop-target.clientHeight<80;const previousHeight=target.scrollHeight;
        for(const m of data.rows)messages.set(m.id,m);
        const hasOlder=older?data.tem_mais_antigas:(messagePage===1?data.tem_mais_antigas:$('#older-messages')!==null);
        target.innerHTML=(hasOlder?'<button id="older-messages" type="button" class="btn btn-light align-self-center">Carregar mensagens anteriores</button>':'')+[...messages.values()].sort((a,b)=>new Date(a.data_mensagem || a.created_at)-new Date(b.data_mensagem || b.created_at)||a.id-b.id).map(m=>`<article class="chat-message ${m.direcao==='ENVIADA'?'sent':''}">${m.tem_imagem?`<img src="/api/atendimento/conversas/${id}/mensagens/${m.id}/imagem" alt="Imagem da conversa" loading="lazy" />`:''}${escapeHtml(m.conteudo || (m.tipo!=='TEXTO'?`[${m.tipo}]`:''))}<small>${escapeHtml(date(m.data_mensagem || m.created_at))} · ${escapeHtml(m.status)}</small>${m.erro?`<small class="text-danger">${escapeHtml(m.erro)}</small>`:''}</article>`).join('');
        if(older)target.scrollTop+=target.scrollHeight-previousHeight;else if(atBottom || messages.size<=50)target.scrollTop=target.scrollHeight;
        on('#older-messages','click',()=>loadMessages(true));
    };
    on('#conversation-search','submit',()=>refresh(1));
    actions('#conversations',async(_,id)=>{
        selected=conversations.find(c=>c.id===Number(id));messages=new Map();messagePage=1;
        $('#chat-header').textContent=`${selected.nome || selected.nome_whatsapp || selected.telefone} · ${selected.telefone} · ${selected.instancia}`;
        $('#chat-messages').innerHTML=empty('Carregando mensagens…');$('#chat-text').disabled=false;$('button',form).disabled=false;
        await loadMessages();await api(`atendimento/conversas/${id}/ler`,'POST',{});await refresh();
    });
    on('#chat-form','submit',async()=>{if(!selected)return;const id=selected.id;const result=await api(`atendimento/conversas/${id}/mensagens`,'POST',{texto:form.texto.value});if(result.status==='ERRO')throw new Error(result.erro || 'Não foi possível enviar a mensagem.');form.reset();await loadMessages();await refresh();});
    const syncStatus=async()=>{const result=await api('atendimento/sincronizar/status');$('#sync-status').textContent=`${result.status} · ${number(result.total_conversas)} conversas · ${number(result.total_mensagens)} mensagens${result.erro?' · '+result.erro:''}`;$('#sync').disabled=['DISPARADO','EM_ANDAMENTO'].includes(result.status);};
    on('#sync','click',async()=>{await api('atendimento/sincronizar?forcar='+$('#force-sync').checked,'POST',{instancia:$('#sync-instance').value || null});feedback('Sincronização iniciada.');await syncStatus();});
    await refresh();await accountOptions('#sync-instance',false);await syncStatus();
    poll(async()=>{try{await refresh();await loadMessages();await syncStatus();}catch(e){$('#sync-status').textContent='Falha ao atualizar: '+e.message;}},5000);
}
const pages={Index:dashboard,WhatsApp:whatsapp,Enviar:send,Massa:massa,Modelos:modelos,Historico:historico,Usuarios:usuarios,Configuracoes:configuracoes,Atendimento:atendimento};
async function init(){try{await pages[pageName]?.();}catch(e){feedback(e.message,true);}if(!['Index','WhatsApp'].includes(pageName)){try{await loadAccounts();}catch{if(controller.signal.aborted)return;$('#connection-badge').textContent='Conexão indisponível';}}poll(async()=>{try{await loadAccounts();}catch{if(controller.signal.aborted)return;$('#connection-badge').textContent='Conexão indisponível';}},30000);}
init();
return () => {
    controller.abort();
    timers.forEach(clearInterval);
    pageRoot.querySelectorAll('.modal').forEach(el => bootstrap.Modal.getInstance(el)?.dispose());
};
}
