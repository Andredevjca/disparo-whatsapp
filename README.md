# Disparo WhatsApp

Sistema recriado em **ASP.NET Core MVC / .NET 10**, com Razor, C#, Dapper e MySQL. A interface usa o CSS do projeto `C:\controle-alugueis`, Bootstrap, Nunito e Font Awesome 6.7.2. Não depende de Angular ou Node para executar.

## Executar

Requisito: SDK .NET 10 e acesso ao MySQL e à Evolution configurados.

```powershell
cd C:\Users\Dell\Desktop\projetos\disparo-whatsapp
dotnet run --project DisparoApi.csproj --launch-profile DisparoWhatsApp
```

Abra **http://localhost:5185**. Use o mesmo e-mail e senha do sistema original. Também é possível executar `Iniciar.ps1` ou abrir `DisparoWhatsApp.slnx` no Visual Studio.

## Módulos

- Dashboard com indicadores reais e últimos envios.
- Contas WhatsApp: listar, criar, conectar com QR Code, desconectar e excluir instâncias.
- Envio individual com texto, modelo e imagem PNG/JPEG de até 5 MB.
- Disparos em massa: importação CSV/XLSX, validação de números, duplicados, busca e paginação dos contatos, variáveis, imagens, intervalo, progresso e parada.
- Modelos: cadastro, edição e exclusão com imagem opcional.
- Histórico: filtros por nome, telefone, status, instância, período e grupo; detalhes e acompanhamento do envio.
- Atendimento: conversas, mensagens, imagens já armazenadas pelo sistema, paginação, respostas e sincronização com Evolution.
- Usuários e configuração do intervalo padrão.

## Banco e integração existentes

`appsettings.json` contém uma cópia das configurações reais do backend original: **mesmo banco, chave JWT, URL, chave e instância Evolution**. As credenciais não são expostas no navegador. Esses arquivos estão no `.gitignore`.

O projeto original em `C:\Developer\Dev-disparo` não foi modificado. Services, Repositories, DTOs, normalização de telefones e contratos `/api/...` foram reaproveitados. O nome interno `DisparoApi` foi mantido para compatibilidade. A autenticação das telas usa cookie HttpOnly e proteção CSRF; clientes da API continuam podendo usar JWT.

`appsettings.Development.json` ajusta a porta e os logs, sem sobrescrever as credenciais com os valores vazios presentes no arquivo de desenvolvimento original. A configuração da pasta `bin` não sobrescreve a configuração da raiz.

Por padrão, `Database:InitializeOnStartup` é `false`: iniciar o novo sistema **não cria banco, não executa migrações, não cria usuários e não pausa disparos do sistema antigo**. A rotina original de criação do esquema permanece disponível para habilitação explícita, quando necessária. Não é necessário habilitá-la para o banco existente.

Os trabalhos em massa continuam em memória, como no backend original. Um disparo iniciado na aplicação antiga deve ser parado nela. Após reiniciar o processo, o sistema não retoma envios automaticamente. Os detalhes do progresso exibem os últimos 500 registros, como na API original; o histórico permite consultar os demais com paginação.

## Evolution e recebimento

As rotas existentes foram preservadas:

```text
POST /api/evolution/webhook
POST /api/evolution/webhook/{evento}
```

A Evolution deve encaminhar os eventos `MESSAGES_UPSERT` e `MESSAGES_UPDATE` à URL pública do backend, com a mesma chave `Evolution:ApiKey`. São aceitos os formatos de evento já usados pelo sistema anterior, inclusive identificação alternativa `remoteJidAlt`.

Enquanto o backend antigo receber o webhook, os dados gravados por ele continuam visíveis neste projeto porque o banco é compartilhado. Ao substituir o backend antigo, mantenha o endereço público através do proxy ou aponte o webhook para a URL pública deste projeto terminada em `/api/evolution/webhook`. `localhost:5185` só é acessível na própria máquina; containers/servidores Evolution precisam de um endereço que alcance esta aplicação. A configuração remota do webhook não foi alterada durante a recriação.

Na validação de 21/09/2026, a Evolution respondeu com **uma instância desconectada**. Conecte-a em **Contas WhatsApp → Conectar**, escaneando o QR Code. Não foram enviados disparos reais durante os testes.

Imagens exibidas no atendimento são as já armazenadas pelo fluxo original de envios. O armazenamento de mídia externa recebida não foi ampliado.

## Verificação

```powershell
dotnet build DisparoApi.csproj
dotnet run --project tests/EnvioImagem.Checks
# Com o site em execução: somente SELECTs no banco e GETs na Evolution/API
dotnet run --project tests/Conexao.Checks
```

Os testes simulados cobrem validação de imagens, envio de texto e mídia, substituição/remoção da imagem em três fluxos, tratamento de falhas Evolution, envelopes de webhook, status e autorização de imagens. O diagnóstico de conexão verifica tabelas existentes, Evolution, bloqueio anônimo, CSRF no login, renderização das nove páginas Razor e APIs de leitura, sem alterar registros.

A compilação foi validada sem avisos nem erros. As páginas foram verificadas por HTTP; a inspeção visual no navegador ficou indisponível no ambiente de automação.

## Organização

- `Controllers/PainelController.cs`, `ContaController.cs`: navegação MVC e login.
- Demais `Controllers`: contratos de API originais.
- `Views`: páginas Razor, layout e componentes de composição/imagem.
- `wwwroot/css`: estilo do controle de aluguéis e adaptações para os módulos de disparos.
- `wwwroot/js/disparo.js`: interação das telas e chamadas autenticadas às APIs.
- `Services`, `Repositories`, `Data`, `Dtos`, `Options`: backend e integrações reaproveitados.
- `tests`: verificações simuladas e diagnóstico de leitura.

Bootstrap é servido localmente. Nunito e Font Awesome usam os mesmos provedores externos do projeto de referência. ClosedXML foi fixado em 0.104.0, versão disponível e utilizada na compilação, eliminando a resolução aproximada da versão 0.102.6 indicada no projeto original.
