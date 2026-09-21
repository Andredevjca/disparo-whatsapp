using DisparoApi.Dtos;
using DisparoApi.Helpers;
using DisparoApi.Models;
using DisparoApi.Options;
using DisparoApi.Repositories;
using Microsoft.Extensions.Options;

namespace DisparoApi.Services;

public class EnvioService : IEnvioService
{
    private readonly IEnvioRepository _envios;
    private readonly ITemplateRepository _templates;
    private readonly IConfiguracaoRepository _config;
    private readonly IEvolutionApiService _evolution;
    private readonly IEnvioMassaJobManager _jobs;
    private readonly IImportacaoRepository _importacoes;
    private readonly IAtendimentoService _atendimento;
    private readonly DefaultsOptions _defaults;

    public EnvioService(
        IEnvioRepository envios,
        ITemplateRepository templates,
        IConfiguracaoRepository config,
        IEvolutionApiService evolution,
        IEnvioMassaJobManager jobs,
        IImportacaoRepository importacoes,
        IAtendimentoService atendimento,
        IOptions<DefaultsOptions> defaults)
    {
        _envios = envios;
        _templates = templates;
        _config = config;
        _evolution = evolution;
        _jobs = jobs;
        _importacoes = importacoes;
        _atendimento = atendimento;
        _defaults = defaults.Value;
    }

    public async Task<EnvioUnitarioResponse> EnviarUnitarioAsync(int usuarioId, EnvioUnitarioRequest req)
    {
        req.Imagem?.Validar();
        var instance = string.IsNullOrWhiteSpace(req.Instance) ? "meu-whatsapp" : req.Instance;
        string? templateNome = req.TemplateNome;
        var mensagem = req.Mensagem ?? string.Empty;

        if (req.TemplateId.HasValue)
        {
            var tpl = await _templates.GetByIdAsync(req.TemplateId.Value);
            if (tpl != null)
            {
                mensagem = tpl.Mensagem;
                templateNome ??= tpl.Nome;
                if (req.Imagem == null && req.UsarImagemTemplate) req.Imagem = tpl.Imagem;
            }
        }

        var dados = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(req.Nome))
            dados["nome"] = req.Nome;
        mensagem = InterpolacaoHelper.Interpolar(mensagem, dados);

        if (string.IsNullOrWhiteSpace(mensagem) && req.Imagem == null)
            throw new ArgumentException("Informe uma mensagem ou uma imagem.");

        var (telOk, telefone, telMotivo) = TelefoneHelper.NormalizarTelefone(req.Telefone);

        string statusDetalhe;
        string? erroInicial = null;

        if (!telOk)
        {
            statusDetalhe = StatusDetalhe.Erro;
            erroInicial = telMotivo;
        }
        else
        {
            statusDetalhe = StatusDetalhe.Pendente;
        }

        var (envioId, detalheId) = await _envios.CriarEnvioUnitarioAsync(
            usuarioId,
            TipoEnvio.Unitario,
            req.TemplateId,
            templateNome,
            null,
            telOk ? StatusEnvio.EmAndamento : StatusEnvio.Concluido,
            instance,
            null,
            req.Nome,
            telOk ? telefone : req.Telefone,
            mensagem,
            statusDetalhe);

        if (req.Imagem != null) await _envios.SalvarImagemAsync(envioId, req.Imagem);

        if (!telOk)
        {
            await _envios.AtualizarDetalheUnitarioAsync(detalheId, StatusDetalhe.Erro, erroInicial, null);
            return new EnvioUnitarioResponse
            {
                Id = envioId,
                Status = StatusDetalhe.Erro,
                Telefone = req.Telefone,
                Mensagem = mensagem,
                Erro = erroInicial
            };
        }

        var (ok, evolutionId, numeroOrigem, erro) = await _evolution.EnviarMensagemAsync(instance, telefone, mensagem, req.Imagem);
        var statusFinal = ok ? StatusDetalhe.Enviado : StatusDetalhe.Erro;

        await _envios.AtualizarDetalheUnitarioAsync(detalheId, statusFinal, ok ? null : erro, evolutionId);

        try
        {
            await _atendimento.AssociarMensagemDisparoAsync(
                instance,
                telefone,
                detalheId,
                evolutionId,
                DirecaoMensagem.Enviada,
                mensagem,
                ok ? StatusMensagem.Enviada : StatusMensagem.Erro,
                DateTime.Now,
                ok ? null : erro);
        }
        catch
        {
            // Ignora erros de associação para não quebrar o fluxo de disparo
        }

        return new EnvioUnitarioResponse
        {
            Id = envioId,
            Status = statusFinal,
            Telefone = telefone,
            Mensagem = mensagem,
            Erro = ok ? null : erro,
            EvolucaoId = evolutionId,
            EvolutionId = evolutionId
        };
    }

    public async Task<EnvioMassaResponse> CriarEIniciarEnvioMassaAsync(int usuarioId, EnvioMassaRequest req)
    {
        if (req.Contatos == null || req.Contatos.Count == 0)
            throw new ArgumentException("Nenhum contato informado");

        req.Imagem?.Validar();
        var instance = string.IsNullOrWhiteSpace(req.Instance) ? "meu-whatsapp" : req.Instance;

        int intervaloMs;
        if (req.IntervaloMs.HasValue && req.IntervaloMs.Value > 0)
            intervaloMs = req.IntervaloMs.Value;
        else
        {
            var cfg = await _config.ObterAsync("intervalo_ms");
            intervaloMs = int.TryParse(cfg, out var v) && v > 0 ? v : _defaults.IntervaloMs;
        }

        string? templateNome = req.TemplateNome;
        var mensagemBase = req.Mensagem ?? string.Empty;

        if (req.TemplateId.HasValue)
        {
            var tpl = await _templates.GetByIdAsync(req.TemplateId.Value);
            if (tpl != null)
            {
                mensagemBase = tpl.Mensagem;
                templateNome ??= tpl.Nome;
                if (req.Imagem == null && req.UsarImagemTemplate) req.Imagem = tpl.Imagem;
            }
        }

        if (string.IsNullOrWhiteSpace(mensagemBase) && req.Imagem == null)
            throw new ArgumentException("Informe uma mensagem ou uma imagem.");

        var detalhes = new List<(string? nome, string telefone, string mensagem, string status, string numeroOrigem)>();
        foreach (var c in req.Contatos)
        {
            var (telOk, telefone, _) = TelefoneHelper.NormalizarTelefone(c.Telefone ?? string.Empty);
            var telFinal = telOk ? telefone : c.Telefone ?? string.Empty;

            var dados = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(c.Nome))
                dados["nome"] = c.Nome;
            if (c.Dados != null)
            {
                foreach (var kvp in c.Dados)
                    dados[kvp.Key] = kvp.Value;
            }

            var msg = InterpolacaoHelper.Interpolar(mensagemBase, dados);
            detalhes.Add((c.Nome, telFinal, msg, telOk ? StatusDetalhe.Pendente : StatusDetalhe.Erro, string.Empty));
        }

        var envioId = await _envios.CriarEnvioMassaCabecalhoAsync(
            usuarioId,
            TipoEnvio.Massa,
            req.TemplateId,
            templateNome,
            intervaloMs,
            detalhes.Count,
            StatusEnvio.EmAndamento,
            instance,
            string.Empty);

        if (req.Imagem != null) await _envios.SalvarImagemAsync(envioId, req.Imagem);

        if (detalhes.Count > 0)
            await _envios.InserirDetalhesMassaAsync(envioId, usuarioId, null, detalhes);

        var temPendentes = detalhes.Any(d => d.status == StatusDetalhe.Pendente);
        if (temPendentes)
            _jobs.IniciarProcessamento(envioId, intervaloMs, instance);
        else
            await _envios.FinalizarEnvioAsync(envioId, StatusEnvio.Concluido);

        return new EnvioMassaResponse { Id = envioId };
    }

    public async Task<EnvioMassaResponse> CriarEIniciarEnvioMassaPorGrupoAsync(int grupoId, int usuarioId, string role, EnvioPorGrupoRequest req)
    {
        var grupo = await _importacoes.ObterGrupoAsync(grupoId, usuarioId, role);
        if (grupo == null)
            throw new KeyNotFoundException("Grupo de importação não encontrado");

        var contatos = await _importacoes.ListarContatosValidosParaEnvioAsync(grupoId);
        if (contatos.Count == 0)
            throw new ArgumentException("Nenhum contato válido neste grupo para envio");

        req.Imagem?.Validar();
        var instance = string.IsNullOrWhiteSpace(req.Instance) ? "meu-whatsapp" : req.Instance;

        int intervaloMs;
        if (req.IntervaloMs.HasValue && req.IntervaloMs.Value > 0)
            intervaloMs = req.IntervaloMs.Value;
        else
        {
            var cfg = await _config.ObterAsync("intervalo_ms");
            intervaloMs = int.TryParse(cfg, out var v) && v > 0 ? v : _defaults.IntervaloMs;
        }

        string? templateNome = req.TemplateNome;
        var mensagemBase = req.Mensagem ?? string.Empty;

        if (req.TemplateId.HasValue)
        {
            var tpl = await _templates.GetByIdAsync(req.TemplateId.Value);
            if (tpl != null)
            {
                mensagemBase = tpl.Mensagem;
                templateNome ??= tpl.Nome;
                if (req.Imagem == null && req.UsarImagemTemplate) req.Imagem = tpl.Imagem;
            }
        }

        if (string.IsNullOrWhiteSpace(mensagemBase) && req.Imagem == null)
            throw new ArgumentException("Informe uma mensagem ou uma imagem.");

        var detalhes = new List<(string? nome, string telefone, string mensagem, string status, string numeroOrigem)>();
        foreach (var c in contatos)
        {
            var (telOk, telefone, _) = TelefoneHelper.NormalizarTelefone(c.Telefone ?? string.Empty);
            var telFinal = telOk ? telefone : c.Telefone ?? string.Empty;

            var dados = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(c.Nome))
                dados["nome"] = c.Nome;
            if (c.Dados != null)
            {
                foreach (var kvp in c.Dados)
                    dados[kvp.Key] = kvp.Value;
            }

            var msg = InterpolacaoHelper.Interpolar(mensagemBase, dados);
            detalhes.Add((c.Nome, telFinal, msg, telOk ? StatusDetalhe.Pendente : StatusDetalhe.Erro, string.Empty));
        }

        var envioId = await _envios.CriarEnvioMassaCabecalhoAsync(
            usuarioId,
            TipoEnvio.Massa,
            req.TemplateId,
            templateNome,
            intervaloMs,
            detalhes.Count,
            StatusEnvio.EmAndamento,
            instance,
            string.Empty,
            grupoId);

        await _importacoes.AtualizarEnvioGrupoIdAsync(envioId, grupoId);

        if (req.Imagem != null) await _envios.SalvarImagemAsync(envioId, req.Imagem);

        if (detalhes.Count > 0)
            await _envios.InserirDetalhesMassaAsync(envioId, usuarioId, grupoId, detalhes);

        var temPendentes = detalhes.Any(d => d.status == StatusDetalhe.Pendente);
        if (temPendentes)
            _jobs.IniciarProcessamento(envioId, intervaloMs, instance);
        else
            await _envios.FinalizarEnvioAsync(envioId, StatusEnvio.Concluido);

        return new EnvioMassaResponse { Id = envioId };
    }
}
