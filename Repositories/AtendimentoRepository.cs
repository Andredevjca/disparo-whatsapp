using Dapper;
using DisparoApi.Data;
using DisparoApi.Dtos;
using DisparoApi.Models;

namespace DisparoApi.Repositories;

public class AtendimentoRepository : IAtendimentoRepository
{
    private readonly IDbConnectionFactory _factory;

    public AtendimentoRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<ConversaPaginadaResponse> ListarConversasAsync(int usuarioId, string role, string? busca, int page, int perPage)
    {
        page = Math.Max(1, page);
        perPage = Math.Clamp(perPage, 1, 200);
        var offset = (page - 1) * perPage;
        var admin = string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase);

        using var conn = _factory.Create();
        await conn.OpenAsync();

        var sqlCount = @"
            SELECT COUNT(*)
            FROM conversas c
            LEFT JOIN contatos_whatsapp ct ON ct.id = c.contato_whatsapp_id
            WHERE (@admin = TRUE OR c.usuario_id = @usuarioId OR c.usuario_id IS NULL)
              AND (
                  @busca IS NULL OR @busca = ''
                  OR ct.nome LIKE @buscaLike
                  OR ct.nome_whatsapp LIKE @buscaLike
                  OR c.telefone LIKE @buscaLike
              )";

        var total = await conn.QueryFirstOrDefaultAsync<int>(sqlCount, new
        {
            admin,
            usuarioId,
            busca,
            buscaLike = string.IsNullOrWhiteSpace(busca) ? null : $"%{busca}%"
        });

        var sql = @"
            SELECT
                c.id, c.contato_whatsapp_id AS ContatoWhatsAppId, c.usuario_id AS UsuarioId,
                c.instancia, c.telefone, c.ultima_mensagem AS UltimaMensagem,
                c.ultima_mensagem_em AS UltimaMensagemEm,
                c.mensagens_nao_lidas AS MensagensNaoLidas, c.status,
                c.created_at AS CreatedAt, c.updated_at AS UpdatedAt,
                ct.nome, ct.nome_whatsapp AS NomeWhatsApp, ct.foto_url AS FotoUrl
            FROM conversas c
            LEFT JOIN contatos_whatsapp ct ON ct.id = c.contato_whatsapp_id
            WHERE (@admin = TRUE OR c.usuario_id = @usuarioId OR c.usuario_id IS NULL)
              AND (
                  @busca IS NULL OR @busca = ''
                  OR ct.nome LIKE @buscaLike
                  OR ct.nome_whatsapp LIKE @buscaLike
                  OR c.telefone LIKE @buscaLike
              )
            ORDER BY COALESCE(c.ultima_mensagem_em, c.updated_at, c.created_at) DESC
            LIMIT @perPage OFFSET @offset";

        var rows = (await conn.QueryAsync<ConversaResponse>(sql, new
        {
            admin,
            usuarioId,
            busca,
            buscaLike = string.IsNullOrWhiteSpace(busca) ? null : $"%{busca}%",
            perPage,
            offset
        })).AsList();

        return new ConversaPaginadaResponse
        {
            Page = page,
            PerPage = perPage,
            Total = total,
            Rows = rows
        };
    }

    public async Task<ConversaDetalheResponse?> ObterConversaAsync(int id, int usuarioId, string role)
    {
        var admin = string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase);
        using var conn = _factory.Create();
        await conn.OpenAsync();

        var sql = @"
            SELECT
                c.id, c.contato_whatsapp_id AS ContatoWhatsAppId, c.usuario_id AS UsuarioId,
                c.instancia, c.telefone, c.ultima_mensagem AS UltimaMensagem,
                c.ultima_mensagem_em AS UltimaMensagemEm,
                c.mensagens_nao_lidas AS MensagensNaoLidas, c.status,
                c.created_at AS CreatedAt, c.updated_at AS UpdatedAt,
                ct.nome, ct.nome_whatsapp AS NomeWhatsApp, ct.foto_url AS FotoUrl
            FROM conversas c
            LEFT JOIN contatos_whatsapp ct ON ct.id = c.contato_whatsapp_id
            WHERE c.id = @id
              AND (@admin = TRUE OR c.usuario_id = @usuarioId OR c.usuario_id IS NULL)
            LIMIT 1";

        var conversa = await conn.QueryFirstOrDefaultAsync<ConversaResponse>(sql, new { id, admin, usuarioId });
        if (conversa == null) return null;

        var sqlContato = @"
            SELECT id, instancia, telefone, nome, nome_whatsapp AS NomeWhatsApp, foto_url AS FotoUrl,
                   ultimo_acesso AS UltimoAcesso, created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM contatos_whatsapp WHERE id = @ctid LIMIT 1";
        var contato = conversa.ContatoWhatsAppId.HasValue
            ? await conn.QueryFirstOrDefaultAsync<ContatoWhatsAppResponse>(sqlContato, new { ctid = conversa.ContatoWhatsAppId.Value })
            : null;

        return new ConversaDetalheResponse { Conversa = conversa, Contato = contato };
    }

    public async Task<ImagemEnvioDto?> ObterImagemMensagemAsync(int conversaId, int mensagemId)
    {
        using var conn = _factory.Create();
        return await conn.QueryFirstOrDefaultAsync<ImagemEnvioDto>(@"
            SELECT i.base64 AS Base64, i.mime_type AS MimeType, i.nome_arquivo AS NomeArquivo
            FROM mensagens m
            JOIN envios_detalhes d ON (d.id = m.envio_detalhe_id OR d.evolution_id = m.evolution_id)
            JOIN envios e ON e.id = d.envio_id AND e.instancia = m.instancia
            JOIN envio_imagens i ON i.envio_id = e.id
            WHERE m.id = @mensagemId AND m.conversa_id = @conversaId LIMIT 1", new { conversaId, mensagemId });
    }

    public async Task<MensagemPaginadaResponse> ListarMensagensAsync(int conversaId, int page, int perPage, DateTime? antesDe = null)
    {
        page = Math.Max(1, page);
        perPage = Math.Clamp(perPage, 1, 200);
        var offset = (page - 1) * perPage;

        using var conn = _factory.Create();
        await conn.OpenAsync();

        var sqlCount = @"
            SELECT COUNT(*) FROM mensagens m
            WHERE m.conversa_id = @conversaId
              AND (@antesDe IS NULL OR m.data_mensagem < @antesDe)";

        var total = await conn.QueryFirstOrDefaultAsync<int>(sqlCount, new { conversaId, antesDe });

        var sql = @"
            SELECT id, conversa_id AS ConversaId, usuario_id AS UsuarioId, envio_detalhe_id AS EnvioDetalheId,
                   evolution_id AS EvolutionId, telefone, instancia, tipo, direcao, conteudo, status,
                   data_mensagem AS DataMensagem, created_at AS CreatedAt, erro,
                   EXISTS(SELECT 1 FROM envios_detalhes d
                          JOIN envios e ON e.id = d.envio_id
                          JOIN envio_imagens i ON i.envio_id = e.id
                          WHERE (d.id = m.envio_detalhe_id OR (d.evolution_id = m.evolution_id AND e.instancia = m.instancia))) AS TemImagem
            FROM mensagens m
            WHERE m.conversa_id = @conversaId
              AND (@antesDe IS NULL OR m.data_mensagem < @antesDe)
            ORDER BY COALESCE(m.data_mensagem, m.created_at) DESC, m.id DESC
            LIMIT @perPage OFFSET @offset";

        var rows = (await conn.QueryAsync<MensagemResponse>(sql, new { conversaId, antesDe, perPage, offset })).AsList();
        rows.Reverse();

        return new MensagemPaginadaResponse
        {
            Page = page,
            PerPage = perPage,
            Total = total,
            Rows = rows,
            TemMaisAntigas = total > offset + perPage
        };
    }

    public async Task<ContatoWhatsAppResponse> CriarOuObterContatoAsync(
        string instancia,
        string telefone,
        string? nomeWhatsApp = null,
        string? fotoUrl = null,
        int? usuarioId = null)
    {
        using var conn = _factory.Create();
        await conn.OpenAsync();

        var sqlGet = @"
            SELECT id, instancia, telefone, nome, nome_whatsapp AS NomeWhatsApp, foto_url AS FotoUrl,
                   ultimo_acesso AS UltimoAcesso, created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM contatos_whatsapp
            WHERE instancia = @instancia AND telefone = @telefone
            LIMIT 1";

        var existente = await conn.QueryFirstOrDefaultAsync<ContatoWhatsAppResponse>(sqlGet, new { instancia, telefone });
        if (existente != null)
        {
            var atualizar =
                (!string.IsNullOrWhiteSpace(nomeWhatsApp) && existente.NomeWhatsApp != nomeWhatsApp) ||
                (!string.IsNullOrWhiteSpace(fotoUrl) && existente.FotoUrl != fotoUrl);

            if (atualizar)
            {
                await conn.ExecuteAsync(@"
                    UPDATE contatos_whatsapp
                    SET nome_whatsapp = COALESCE(NULLIF(@nomeWhatsApp, ''), nome_whatsapp),
                        foto_url = COALESCE(NULLIF(@fotoUrl, ''), foto_url)
                    WHERE id = @id",
                    new { id = existente.Id, nomeWhatsApp, fotoUrl });
            }

            return existente;
        }

        var sqlInsert = @"
            INSERT INTO contatos_whatsapp (usuario_id, instancia, telefone, nome_whatsapp, foto_url, ultimo_acesso)
            VALUES (@usuarioId, @instancia, @telefone, @nomeWhatsApp, @fotoUrl, NOW())";

        await conn.ExecuteAsync(sqlInsert, new { usuarioId, instancia, telefone, nomeWhatsApp, fotoUrl });

        return (await conn.QueryFirstOrDefaultAsync<ContatoWhatsAppResponse>(sqlGet, new { instancia, telefone }))!;
    }

    public async Task<ConversaResponse> CriarOuObterConversaAsync(
        string instancia,
        string telefone,
        int contatoWhatsAppId,
        int? usuarioId = null)
    {
        using var conn = _factory.Create();
        await conn.OpenAsync();

        var sqlGet = @"
            SELECT
                c.id, c.contato_whatsapp_id AS ContatoWhatsAppId, c.usuario_id AS UsuarioId,
                c.instancia, c.telefone, c.ultima_mensagem AS UltimaMensagem,
                c.ultima_mensagem_em AS UltimaMensagemEm,
                c.mensagens_nao_lidas AS MensagensNaoLidas, c.status,
                c.created_at AS CreatedAt, c.updated_at AS UpdatedAt,
                ct.nome, ct.nome_whatsapp AS NomeWhatsApp, ct.foto_url AS FotoUrl
            FROM conversas c
            LEFT JOIN contatos_whatsapp ct ON ct.id = c.contato_whatsapp_id
            WHERE c.instancia = @instancia AND c.telefone = @telefone
            LIMIT 1";

        var existente = await conn.QueryFirstOrDefaultAsync<ConversaResponse>(sqlGet, new { instancia, telefone });
        if (existente != null) return existente;

        var sqlInsert = @"
            INSERT INTO conversas (contato_whatsapp_id, usuario_id, instancia, telefone, status)
            VALUES (@ctid, @usuarioId, @instancia, @telefone, @status)";

        await conn.ExecuteAsync(sqlInsert, new
        {
            ctid = contatoWhatsAppId,
            usuarioId,
            instancia,
            telefone,
            status = StatusConversa.Aberta
        });

        return (await conn.QueryFirstOrDefaultAsync<ConversaResponse>(sqlGet, new { instancia, telefone }))!;
    }

    public async Task<(bool inseriu, int? id)> InserirMensagemSeNaoExistirAsync(
        int conversaId,
        int? usuarioId,
        int? envioDetalheId,
        string? evolutionId,
        string telefone,
        string instancia,
        string tipo,
        string direcao,
        string? conteudo,
        string status,
        DateTime? dataMensagem,
        string? erro = null)
    {
        using var conn = _factory.Create();
        await conn.OpenAsync();

        if (!string.IsNullOrWhiteSpace(evolutionId))
        {
            var existente = await conn.QueryFirstOrDefaultAsync<int?>(@"
                SELECT id FROM mensagens WHERE instancia = @instancia AND evolution_id = @evolutionId LIMIT 1",
                new { instancia, evolutionId });

            if (existente.HasValue) return (false, existente.Value);
        }

        var sqlInsert = @"
            INSERT INTO mensagens (conversa_id, usuario_id, envio_detalhe_id, evolution_id, telefone, instancia,
                                   tipo, direcao, conteudo, status, data_mensagem, erro)
            VALUES (@conversaId, @usuarioId, @envioDetalheId, @evolutionId, @telefone, @instancia,
                    @tipo, @direcao, @conteudo, @status, @dataMensagem, @erro);
            SELECT LAST_INSERT_ID();";

        var novoId = await conn.QueryFirstOrDefaultAsync<int>(sqlInsert, new
        {
            conversaId,
            usuarioId,
            envioDetalheId,
            evolutionId,
            telefone,
            instancia,
            tipo,
            direcao,
            conteudo,
            status,
            dataMensagem,
            erro
        });

        return (true, novoId == 0 ? null : novoId);
    }

    public async Task AtualizarStatusMensagemPorEvolutionIdAsync(string instancia, string evolutionId, string status)
    {
        using var conn = _factory.Create();
        await conn.OpenAsync();
        await conn.ExecuteAsync(@"
            UPDATE mensagens SET status = @status WHERE instancia = @instancia AND evolution_id = @evolutionId",
            new { instancia, evolutionId, status });
    }

    public async Task AtualizarMensagemSeIncompletaAsync(string instancia, string evolutionId, string? novoConteudo, string? direcao, string? status, DateTime? dataMensagem)
    {
        using var conn = _factory.Create();
        await conn.OpenAsync();
        await conn.ExecuteAsync(@"
            UPDATE mensagens
            SET conteudo = COALESCE(NULLIF(@novoConteudo, ''), conteudo),
                direcao = COALESCE(NULLIF(@direcao, ''), direcao),
                status = COALESCE(NULLIF(@status, ''), status),
                data_mensagem = COALESCE(@dataMensagem, data_mensagem)
            WHERE instancia = @instancia AND evolution_id = @evolutionId",
            new
            {
                instancia,
                evolutionId,
                novoConteudo,
                direcao,
                status,
                dataMensagem
            });
    }

    public async Task MarcarConversaLidaAsync(int conversaId)
    {
        using var conn = _factory.Create();
        await conn.OpenAsync();
        await conn.ExecuteAsync(@"
            UPDATE conversas SET mensagens_nao_lidas = 0 WHERE id = @conversaId",
            new { conversaId });
    }

    public async Task AtualizarConversaPosMensagemAsync(
        int conversaId,
        string ultimaMensagem,
        DateTime ultimaMensagemEm,
        int incrementoNaoLidas)
    {
        using var conn = _factory.Create();
        await conn.OpenAsync();
        await conn.ExecuteAsync(@"
            UPDATE conversas
            SET ultima_mensagem   = CASE WHEN @ultimaMensagemEm >= COALESCE(ultima_mensagem_em, '1970-01-01 00:00:00')
                                         THEN @ultimaMensagem ELSE ultima_mensagem END,
                ultima_mensagem_em = GREATEST(COALESCE(ultima_mensagem_em, @ultimaMensagemEm), @ultimaMensagemEm),
                mensagens_nao_lidas = mensagens_nao_lidas + @incrementoNaoLidas
            WHERE id = @conversaId",
            new { conversaId, ultimaMensagem, ultimaMensagemEm, incrementoNaoLidas });
    }

    public async Task<List<ContatoWhatsAppResponse>> ListarContatosAsync(int usuarioId, string role, string? busca)
    {
        var admin = string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase);
        using var conn = _factory.Create();
        await conn.OpenAsync();

        var sql = @"
            SELECT id, instancia, telefone, nome, nome_whatsapp AS NomeWhatsApp, foto_url AS FotoUrl,
                   ultimo_acesso AS UltimoAcesso, created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM contatos_whatsapp
            WHERE (@admin = TRUE OR usuario_id = @usuarioId OR usuario_id IS NULL)
              AND (
                  @busca IS NULL OR @busca = ''
                  OR nome LIKE @buscaLike
                  OR nome_whatsapp LIKE @buscaLike
                  OR telefone LIKE @buscaLike
              )
            ORDER BY COALESCE(nome, nome_whatsapp, telefone) ASC
            LIMIT 500";

        return (await conn.QueryAsync<ContatoWhatsAppResponse>(sql, new
        {
            admin,
            usuarioId,
            busca,
            buscaLike = string.IsNullOrWhiteSpace(busca) ? null : $"%{busca}%"
        })).AsList();
    }

    public async Task<(int? contatoImportadoId, string? nomeImportado)?> ObterContatoImportadoPorTelefoneAsync(
        int usuarioId, string telefoneNormalizado)
    {
        using var conn = _factory.Create();
        await conn.OpenAsync();

        var sql = @"
            SELECT id, nome FROM contatos_importados
            WHERE telefone_normalizado = @telefone
              AND status_validacao = 'VALIDO'
              AND (usuario_id = @usuarioId OR EXISTS (SELECT 1 FROM usuarios WHERE id = @usuarioId AND role = 'admin'))
            ORDER BY id DESC
            LIMIT 1";

        var res = await conn.QueryFirstOrDefaultAsync(sql, new { telefone = telefoneNormalizado, usuarioId });
        if (res == null) return null;
        return (res.id, res.nome);
    }

    public async Task AtualizarEnvioDetalheMensagemIdAsync(int envioDetalheId, int mensagemId)
    {
        using var conn = _factory.Create();
        await conn.OpenAsync();
        await conn.ExecuteAsync(@"
            UPDATE envios_detalhes SET mensagem_id = @mensagemId WHERE id = @envioDetalheId",
            new { envioDetalheId, mensagemId });
    }
}
