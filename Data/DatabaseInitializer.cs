using Dapper;
using MySqlConnector;
using Microsoft.Extensions.Options;
using DisparoApi.Options;

namespace DisparoApi.Data;

public interface IDatabaseInitializer
{
    Task InitializeAsync();
}

public class DatabaseInitializer : IDatabaseInitializer
{
    private readonly IDbConnectionFactory _factory;
    private readonly DefaultsOptions _defaults;

    public DatabaseInitializer(IDbConnectionFactory factory, IOptions<DefaultsOptions> defaults)
    {
        _factory = factory;
        _defaults = defaults.Value;
    }

    public async Task InitializeAsync()
    {
        var connFull = _factory.Create();
        var csb = new MySqlConnectionStringBuilder(connFull.ConnectionString);
        var databaseName = string.IsNullOrWhiteSpace(csb.Database) ? "disparo" : csb.Database;

        if (!string.IsNullOrWhiteSpace(databaseName))
        {
            var csbSemDb = new MySqlConnectionStringBuilder(csb.ConnectionString) { Database = null, Pooling = false };
            await using var connSemDb = new MySqlConnection(csbSemDb.ConnectionString);
            await connSemDb.OpenAsync();
            await connSemDb.ExecuteAsync(
                $"CREATE DATABASE IF NOT EXISTS `{databaseName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
            await connSemDb.CloseAsync();
        }

        await using var conn = _factory.Create();
        await conn.OpenAsync();

        await ExecuteAsync(conn, @"
            CREATE TABLE IF NOT EXISTS usuarios (
                id INT PRIMARY KEY AUTO_INCREMENT,
                nome VARCHAR(255) NULL,
                email VARCHAR(255) NOT NULL UNIQUE,
                password_hash VARCHAR(255) NOT NULL,
                role VARCHAR(50) NOT NULL DEFAULT 'user',
                created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        ");

        await ExecuteAsync(conn, @"
            CREATE TABLE IF NOT EXISTS templates (
                id INT PRIMARY KEY AUTO_INCREMENT,
                nome VARCHAR(255) NOT NULL,
                mensagem TEXT NOT NULL,
                created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        ");

        await ExecuteAsync(conn, @"
            CREATE TABLE IF NOT EXISTS template_imagens (
                template_id INT PRIMARY KEY,
                base64 LONGTEXT NOT NULL,
                mime_type VARCHAR(50) NOT NULL,
                nome_arquivo VARCHAR(255) NOT NULL,
                FOREIGN KEY (template_id) REFERENCES templates(id) ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        ");

        await ExecuteAsync(conn, @"
            CREATE TABLE IF NOT EXISTS envios (
                id INT PRIMARY KEY AUTO_INCREMENT,
                usuario_id INT NULL,
                tipo VARCHAR(20) NOT NULL,
                template_id INT NULL,
                template_nome VARCHAR(255) NULL,
                intervalo_ms INT NULL,
                total INT NOT NULL DEFAULT 0,
                enviados INT NOT NULL DEFAULT 0,
                erros INT NOT NULL DEFAULT 0,
                pendentes INT NOT NULL DEFAULT 0,
                status VARCHAR(20) NOT NULL,
                instancia VARCHAR(255) NULL,
                numero_origem VARCHAR(50) NULL,
                grupo_importacao_id INT NULL,
                created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                finished_at DATETIME NULL,
                INDEX idx_envios_usuario (usuario_id),
                INDEX idx_envios_status (status),
                INDEX idx_envios_created (created_at)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        ");

        await ExecuteAsync(conn, @"
            CREATE TABLE IF NOT EXISTS envio_imagens (
                envio_id INT PRIMARY KEY,
                base64 LONGTEXT NOT NULL,
                mime_type VARCHAR(50) NOT NULL,
                nome_arquivo VARCHAR(255) NOT NULL,
                FOREIGN KEY (envio_id) REFERENCES envios(id) ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        ");

        await ExecuteAsync(conn, @"
            CREATE TABLE IF NOT EXISTS envios_detalhes (
                id INT PRIMARY KEY AUTO_INCREMENT,
                envio_id INT NOT NULL,
                usuario_id INT NULL,
                grupo_importacao_id INT NULL,
                nome VARCHAR(255) NULL,
                telefone VARCHAR(50) NOT NULL,
                mensagem TEXT NULL,
                status VARCHAR(20) NOT NULL,
                erro TEXT NULL,
                evolution_id VARCHAR(255) NULL,
                numero_origem VARCHAR(50) NULL,
                created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                enviado_em DATETIME NULL,
                CONSTRAINT fk_detalhes_envio FOREIGN KEY (envio_id) REFERENCES envios(id) ON DELETE CASCADE,
                INDEX idx_detalhes_envio (envio_id),
                INDEX idx_detalhes_usuario (usuario_id),
                INDEX idx_detalhes_grupo (grupo_importacao_id),
                INDEX idx_detalhes_telefone (telefone),
                INDEX idx_detalhes_status (status),
                INDEX idx_detalhes_created (created_at),
                INDEX idx_detalhes_enviado_em (enviado_em)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        ");

        await ExecuteAsync(conn, @"
            CREATE TABLE IF NOT EXISTS configuracoes (
                chave VARCHAR(100) PRIMARY KEY,
                valor TEXT NOT NULL
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        ");

        await ExecuteAsync(conn, @"
            CREATE TABLE IF NOT EXISTS grupos_importacoes (
                id INT PRIMARY KEY AUTO_INCREMENT,
                usuario_id INT NOT NULL,
                nome VARCHAR(255) NULL,
                arquivo_nome VARCHAR(255) NULL,
                total_linhas INT NOT NULL DEFAULT 0,
                validos INT NOT NULL DEFAULT 0,
                invalidos INT NOT NULL DEFAULT 0,
                duplicados INT NOT NULL DEFAULT 0,
                created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                CONSTRAINT fk_grupos_usuario FOREIGN KEY (usuario_id) REFERENCES usuarios(id) ON DELETE CASCADE,
                INDEX idx_grupos_usuario (usuario_id),
                INDEX idx_grupos_created (created_at)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        ");

        await ExecuteAsync(conn, @"
            CREATE TABLE IF NOT EXISTS contatos_importados (
                id INT PRIMARY KEY AUTO_INCREMENT,
                grupo_id INT NOT NULL,
                usuario_id INT NOT NULL,
                nome VARCHAR(255) NULL,
                email VARCHAR(255) NULL,
                telefone_normalizado VARCHAR(50) NULL,
                telefone_original VARCHAR(100) NULL,
                dados JSON NULL,
                status_validacao VARCHAR(20) NOT NULL DEFAULT 'VALIDO',
                created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                CONSTRAINT fk_contatos_grupo FOREIGN KEY (grupo_id) REFERENCES grupos_importacoes(id) ON DELETE CASCADE,
                CONSTRAINT fk_contatos_usuario FOREIGN KEY (usuario_id) REFERENCES usuarios(id) ON DELETE CASCADE,
                INDEX idx_contatos_grupo (grupo_id),
                INDEX idx_contatos_usuario (usuario_id),
                INDEX idx_contatos_telefone (telefone_normalizado),
                INDEX idx_contatos_status (status_validacao)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        ");

        await ExecuteAsync(conn, @"
            CREATE TABLE IF NOT EXISTS contatos_whatsapp (
                id INT PRIMARY KEY AUTO_INCREMENT,
                usuario_id INT NULL,
                instancia VARCHAR(255) NOT NULL,
                telefone VARCHAR(50) NOT NULL,
                nome VARCHAR(255) NULL,
                nome_whatsapp VARCHAR(255) NULL,
                foto_url VARCHAR(1024) NULL,
                ultimo_acesso DATETIME NULL,
                created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                UNIQUE KEY uk_contatos_instancia_telefone (instancia, telefone),
                INDEX idx_contatos_instancia (instancia),
                INDEX idx_contatos_usuario (usuario_id),
                INDEX idx_contatos_telefone (telefone)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        ");

        await ExecuteAsync(conn, @"
            CREATE TABLE IF NOT EXISTS conversas (
                id INT PRIMARY KEY AUTO_INCREMENT,
                contato_whatsapp_id INT NULL,
                usuario_id INT NULL,
                instancia VARCHAR(255) NOT NULL,
                telefone VARCHAR(50) NOT NULL,
                ultima_mensagem TEXT NULL,
                ultima_mensagem_em DATETIME NULL,
                mensagens_nao_lidas INT NOT NULL DEFAULT 0,
                status VARCHAR(20) NOT NULL DEFAULT 'ABERTA',
                created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                UNIQUE KEY uk_conversas_instancia_telefone (instancia, telefone),
                CONSTRAINT fk_conversas_contato FOREIGN KEY (contato_whatsapp_id) REFERENCES contatos_whatsapp(id) ON DELETE SET NULL,
                INDEX idx_conversas_usuario (usuario_id),
                INDEX idx_conversas_instancia (instancia),
                INDEX idx_conversas_telefone (telefone),
                INDEX idx_conversas_updated (updated_at),
                INDEX idx_conversas_ultima_em (ultima_mensagem_em),
                INDEX idx_conversas_nao_lidas (mensagens_nao_lidas)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        ");

        await ExecuteAsync(conn, @"
            CREATE TABLE IF NOT EXISTS mensagens (
                id INT PRIMARY KEY AUTO_INCREMENT,
                conversa_id INT NOT NULL,
                usuario_id INT NULL,
                envio_detalhe_id INT NULL,
                evolution_id VARCHAR(255) NULL,
                telefone VARCHAR(50) NOT NULL,
                instancia VARCHAR(255) NOT NULL,
                tipo VARCHAR(20) NOT NULL DEFAULT 'TEXTO',
                direcao VARCHAR(20) NOT NULL,
                conteudo TEXT NULL,
                status VARCHAR(20) NOT NULL DEFAULT 'PENDENTE',
                data_mensagem DATETIME NULL,
                created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                erro TEXT NULL,
                CONSTRAINT fk_mensagens_conversa FOREIGN KEY (conversa_id) REFERENCES conversas(id) ON DELETE CASCADE,
                UNIQUE KEY uk_mensagens_instancia_evolution (instancia, evolution_id),
                INDEX idx_mensagens_conversa (conversa_id),
                INDEX idx_mensagens_usuario (usuario_id),
                INDEX idx_mensagens_envio_detalhe (envio_detalhe_id),
                INDEX idx_mensagens_instancia (instancia),
                INDEX idx_mensagens_telefone (telefone),
                INDEX idx_mensagens_data (data_mensagem),
                INDEX idx_mensagens_created (created_at),
                INDEX idx_mensagens_status (status)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        ");

        await AdicionarColunaAsync(conn, "envios", "grupo_importacao_id", "INT NULL");
        await AdicionarColunaAsync(conn, "envios", "numero_origem", "VARCHAR(50) NULL");
        await AdicionarColunaAsync(conn, "envios", "usuario_id", "INT NULL");
        await AdicionarColunaAsync(conn, "envios_detalhes", "numero_origem", "VARCHAR(50) NULL");
        await AdicionarColunaAsync(conn, "envios_detalhes", "usuario_id", "INT NULL");
        await AdicionarColunaAsync(conn, "envios_detalhes", "grupo_importacao_id", "INT NULL");
        await AdicionarColunaAsync(conn, "envios_detalhes", "mensagem_id", "INT NULL");

        var intervalo = await conn.QueryFirstOrDefaultAsync<string?>(
            "SELECT valor FROM configuracoes WHERE chave = @chave", new { chave = "intervalo_ms" });
        if (intervalo == null)
        {
            await conn.ExecuteAsync(
                "INSERT INTO configuracoes (chave, valor) VALUES (@chave, @valor)",
                new { chave = "intervalo_ms", valor = _defaults.IntervaloMs.ToString() });
        }

        var qtdTemplates = await conn.QueryFirstOrDefaultAsync<int>("SELECT COUNT(*) FROM templates");
        if (qtdTemplates == 0)
        {
            await conn.ExecuteAsync(
                "INSERT INTO templates (nome, mensagem) VALUES (@nome, @mensagem)",
                new
                {
                    nome = "Informativo de cobrança",
                    mensagem = "Olá {{nome}},\n\nSeu pagamento no valor de R$ {{valor}} vence em {{vencimento}}.\n\nEntre em contato conosco para mais informações."
                });
        }

        var qtdUsuarios = await conn.QueryFirstOrDefaultAsync<int>("SELECT COUNT(*) FROM usuarios");
        if (qtdUsuarios == 0)
        {
            var hash = BCrypt.Net.BCrypt.EnhancedHashPassword("admin", workFactor: 10);
            await conn.ExecuteAsync(
                "INSERT INTO usuarios (nome, email, password_hash, role) VALUES (@nome, @email, @passwordHash, @role)",
                new
                {
                    nome = "Administrador",
                    email = "admin@admin.com",
                    passwordHash = hash,
                    role = "admin"
                });
        }
    }

    private static Task ExecuteAsync(MySqlConnection conn, string sql)
        => conn.ExecuteAsync(sql);

    private static async Task AdicionarColunaAsync(MySqlConnection conn, string tabela, string coluna, string definicao)
    {
        var existe = await conn.QueryFirstOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @tabela AND COLUMN_NAME = @coluna",
            new { tabela, coluna });
        if (existe == 0)
        {
            await conn.ExecuteAsync($"ALTER TABLE `{tabela}` ADD COLUMN `{coluna}` {definicao}");
        }
    }
}
