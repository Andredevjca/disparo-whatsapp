import { mountPage } from './disparo.js';

let dispose = mountPage();
let pending;
let currentUrl = location.href;
const routes = new Set(['/', '/WhatsApp', '/Enviar', '/Massa', '/Atendimento', '/Modelos', '/Historico', '/Usuarios', '/Configuracoes'].map(path => path.toLowerCase()));
const internal = url => url.origin === location.origin && routes.has(url.pathname.replace(/\/$/, '').toLowerCase() || '/');

function prepareStyles(next, signal) {
    const previous = [...document.head.querySelectorAll('link[data-page-style]')];
    const staged = [];
    const ready = Promise.all([...next.head.querySelectorAll('link[data-page-style]')].map(source => {
        const link = source.cloneNode(true);
        const media = source.media;
        // Download without applying destination styles to the current screen.
        link.media = 'not all';
        staged.push({ link, media });
        return new Promise((resolve, reject) => {
            const finish = error => {
                clearTimeout(timeout);
                link.onload = link.onerror = null;
                signal.removeEventListener('abort', abort);
                if (error) reject(error);
                else resolve();
            };
            const abort = () => finish(new DOMException('Navigation cancelled', 'AbortError'));
            const timeout = setTimeout(() => finish(new Error('Stylesheet timeout')), 15000);
            link.onload = () => finish();
            link.onerror = () => finish(new Error('Stylesheet failed to load'));
            signal.addEventListener('abort', abort, { once: true });
            if (signal.aborted) { abort(); return; }
            document.head.append(link);
        });
    }));
    return {
        ready,
        commit() {
            staged.forEach(({ link, media }) => { link.media = media; });
            previous.forEach(link => link.remove());
        },
        discard() { staged.forEach(({ link }) => link.remove()); }
    };
}

async function navigate(url, pop = false) {
    pending?.abort();
    const request = new AbortController();
    pending = request;
    let styles;
    let stylesCommitted = false;
    const main = document.getElementById('main-content');
    main.setAttribute('aria-busy', 'true');
    try {
        const response = await fetch(url, { credentials: 'same-origin', cache: 'no-store', signal: request.signal, headers: { 'X-Requested-With': 'fetch' } });
        const destination = new URL(response.url);
        if (!internal(destination)) { location.assign(destination.href); return; }
        if (!response.ok || !response.headers.get('content-type')?.includes('text/html')) throw new Error('Resposta inesperada');
        const next = new DOMParser().parseFromString(await response.text(), 'text/html');
        const content = next.getElementById('main-content');
        if (!content || content.querySelector('script')) throw new Error('Conteúdo inválido');
        request.signal.throwIfAborted();
        styles = prepareStyles(next, request.signal);
        await styles.ready;
        for (const element of main.querySelectorAll('.modal.show')) {
            await new Promise(resolve => {
                element.addEventListener('hidden.bs.modal', resolve, { once: true });
                bootstrap.Modal.getInstance(element)?.hide();
            });
        }
        request.signal.throwIfAborted();
        dispose();
        styles.commit();
        stylesCommitted = true;
        main.replaceWith(content);
        document.title = next.title;
        document.body.dataset.page = next.body.dataset.page;
        document.querySelector('.header-breadcrumb').innerHTML = next.querySelector('.header-breadcrumb').innerHTML;
        document.querySelector('meta[name="csrf-token"]').content = next.querySelector('meta[name="csrf-token"]').content;
        const active = new Set([...next.querySelectorAll('.sidebar-menu a.active')].map(link => link.getAttribute('href')));
        document.querySelectorAll('.sidebar-menu a').forEach(link => {
            const selected = active.has(link.getAttribute('href'));
            link.classList.toggle('active', selected);
            if (selected) link.setAttribute('aria-current', 'page');
            else link.removeAttribute('aria-current');
        });
        if (!pop && location.href !== destination.href) history.pushState(null, '', destination.href);
        else history.replaceState(null, '', destination.href);
        currentUrl = destination.href;
        document.body.classList.remove('sidebar-open');
        dispose = mountPage();
        content.focus({ preventScroll: true });
        window.scrollTo(0, 0);
    } catch (error) {
        if (request.signal.aborted) return;
        const feedback = document.getElementById('feedback');
        feedback.hidden = false;
        feedback.className = 'alert alert-danger';
        feedback.textContent = 'Não foi possível carregar a página. Tente novamente.';
        if (pop) history.replaceState(null, '', currentUrl);
    } finally {
        if (!stylesCommitted) styles?.discard();
        if (pending === request) {
            pending = null;
            document.getElementById('main-content').removeAttribute('aria-busy');
        }
    }
}

document.addEventListener('click', event => {
    if (event.defaultPrevented || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
    const link = event.target.closest('a[href]');
    if (!link || link.hasAttribute('download') || link.hasAttribute('data-no-spa') || link.hasAttribute('data-bs-toggle') || (link.target && link.target !== '_self')) return;
    const url = new URL(link.href);
    if (!internal(url) || url.hash) return;
    event.preventDefault();
    navigate(url.href);
});
window.addEventListener('popstate', () => navigate(location.href, true));
window.addEventListener('pagehide', () => { pending?.abort(); dispose(); });
window.addEventListener('pageshow', event => { if (event.persisted) dispose = mountPage(); });
