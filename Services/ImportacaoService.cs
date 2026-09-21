using ClosedXML.Excel;
using DisparoApi.Dtos;
using DisparoApi.Helpers;
using DisparoApi.Models;
using DisparoApi.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualBasic.FileIO;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace DisparoApi.Services;

public class ImportacaoService : IImportacaoService
{
    private readonly IImportacaoRepository _repo;

    public ImportacaoService(IImportacaoRepository repo)
    {
        _repo = repo;
    }

    public async Task<ImportarPlanilhaResponse> ImportarArquivoAsync(int usuarioId, IFormFile arquivo, string? nomeGrupo)
    {
        if (arquivo == null || arquivo.Length == 0)
            throw new ArgumentException("Arquivo vazio");

        var ext = Path.GetExtension(arquivo.FileName)?.ToLowerInvariant();
        if (ext != ".csv" && ext != ".xlsx")
            throw new ArgumentException("Formato não suportado. Use CSV ou XLSX.");

        List<Dictionary<string, string?>> rows;
        if (ext == ".xlsx")
            rows = ParseXlsx(arquivo);
        else
            rows = ParseCsv(arquivo);

        if (rows.Count == 0)
            throw new ArgumentException("Nenhuma linha encontrada na planilha");

        var headerKeys = rows[0].Keys.ToList();
        var fTelefone = DetectarColuna(headerKeys, "telefone", "numero", "phone", "celular", "number", "whatsapp", "tel");
        var fNome = DetectarColuna(headerKeys, "nome", "name", "cliente", "pessoa");
        var fEmail = DetectarColuna(headerKeys, "email", "e-mail", "e_mail", "mail", "e mail");

        var grupoNome = string.IsNullOrWhiteSpace(nomeGrupo)
            ? $"Importação {DateTime.Now:dd/MM/yyyy HH:mm}"
            : nomeGrupo;

        var grupoId = await _repo.CriarGrupoAsync(usuarioId, grupoNome, arquivo.FileName);

        var telefonesNoGrupo = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var contatos = new List<ContatoImportado>();
        var validos = 0;
        var invalidos = 0;
        var duplicados = 0;

        foreach (var row in rows)
        {
            var telOriginal = (fTelefone != null && row.ContainsKey(fTelefone)) ? row[fTelefone] : null;
            telOriginal = string.IsNullOrWhiteSpace(telOriginal) ? null : telOriginal.Trim();

            var (telOk, telNorm, _) = TelefoneHelper.NormalizarTelefone(telOriginal ?? string.Empty);
            string status;
            if (!telOk)
            {
                status = StatusValidacaoContato.Invalido;
                invalidos++;
            }
            else if (!string.IsNullOrWhiteSpace(telNorm) && telefonesNoGrupo.Add(telNorm))
            {
                status = StatusValidacaoContato.Valido;
                validos++;
            }
            else
            {
                status = StatusValidacaoContato.Duplicado;
                duplicados++;
            }

            var nome = fNome != null && row.ContainsKey(fNome) ? row[fNome]?.Trim() : null;
            if (string.IsNullOrWhiteSpace(nome)) nome = null;

            var email = fEmail != null && row.ContainsKey(fEmail) ? row[fEmail]?.Trim() : null;
            if (string.IsNullOrWhiteSpace(email)) email = null;

            var extras = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var k in headerKeys)
            {
                if (k == fTelefone || k == fNome || k == fEmail) continue;
                if (row.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v))
                    extras[k] = v?.Trim();
            }
            string? dadosJson = extras.Count > 0 ? JsonSerializer.Serialize(extras) : null;

            contatos.Add(new ContatoImportado
            {
                GrupoId = grupoId,
                UsuarioId = usuarioId,
                Nome = nome,
                Email = email,
                TelefoneNormalizado = telOk ? telNorm : null,
                TelefoneOriginal = telOriginal,
                Dados = dadosJson,
                StatusValidacao = status
            });
        }

        await _repo.InserirContatosBulkAsync(grupoId, usuarioId, contatos);
        await _repo.AtualizarGrupoContagemAsync(grupoId, contatos.Count, validos, invalidos, duplicados);

        return new ImportarPlanilhaResponse
        {
            GrupoId = grupoId,
            Total = contatos.Count,
            Validos = validos,
            Invalidos = invalidos,
            Duplicados = duplicados
        };
    }

    private static string? DetectarColuna(List<string> headers, params string[] candidatos)
    {
        foreach (var c in candidatos)
        {
            var m = headers.FirstOrDefault(h => string.Equals(h.Trim(), c, StringComparison.OrdinalIgnoreCase));
            if (m != null) return m;
        }
        foreach (var c in candidatos)
        {
            var m = headers.FirstOrDefault(h => h.Trim().Contains(c, StringComparison.OrdinalIgnoreCase));
            if (m != null) return m;
        }
        return null;
    }

    private static List<Dictionary<string, string?>> ParseXlsx(IFormFile arquivo)
    {
        var list = new List<Dictionary<string, string?>>();
        using var ms = new MemoryStream();
        arquivo.CopyTo(ms);
        ms.Position = 0;
        using var wb = new XLWorkbook(ms);
        var ws = wb.Worksheets.First();
        var rows = ws.RowsUsed().ToList();
        if (rows.Count == 0) return list;
        var headerRow = rows[0];
        var headers = new List<string>();
        var cells = headerRow.CellsUsed().ToList();
        var lastCol = cells.Count > 0 ? cells.Max(c => c.Address.ColumnNumber) : 1;
        for (var i = 1; i <= lastCol; i++)
        {
            var v = headerRow.Cell(i).GetString();
            headers.Add(string.IsNullOrWhiteSpace(v) ? $"col{i}" : v.Trim());
        }
        for (var r = 1; r < rows.Count; r++)
        {
            var row = rows[r];
            var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var vazia = true;
            for (var i = 1; i <= headers.Count; i++)
            {
                var v = row.Cell(i).GetString();
                dict[headers[i - 1]] = string.IsNullOrWhiteSpace(v) ? null : v.Trim();
                if (!string.IsNullOrWhiteSpace(v)) vazia = false;
            }
            if (vazia) continue;
            list.Add(dict);
        }
        return list;
    }

    private static List<Dictionary<string, string?>> ParseCsv(IFormFile arquivo)
    {
        var list = new List<Dictionary<string, string?>>();
        using var ms = new MemoryStream();
        arquivo.CopyTo(ms);
        ms.Position = 0;
        using var reader = new StreamReader(ms, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var separadores = new[] { ',', ';', '\t' };
        string? primeiraLinha = null;
        var separador = ',';
        if ((primeiraLinha = reader.ReadLine()) != null)
        {
            var counts = separadores.ToDictionary(s => s, s => primeiraLinha.Count(c => c == s));
            separador = counts.OrderByDescending(kv => kv.Value).First().Key;
        }
        ms.Position = 0;
        reader.DiscardBufferedData();
        using var parser = new TextFieldParser(reader.BaseStream, Encoding.UTF8, detectEncoding: true)
        {
            TextFieldType = FieldType.Delimited,
            Delimiters = new[] { separador.ToString(CultureInfo.InvariantCulture) },
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = true
        };
        var headers = parser.ReadFields();
        if (headers == null) return list;
        var headerList = headers.Select((h, i) => string.IsNullOrWhiteSpace(h) ? $"col{i + 1}" : h.Trim()).ToList();
        while (!parser.EndOfData)
        {
            var campos = parser.ReadFields();
            if (campos == null) continue;
            var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var vazia = true;
            for (var i = 0; i < headerList.Count; i++)
            {
                var v = i < campos.Length ? campos[i] : null;
                dict[headerList[i]] = string.IsNullOrWhiteSpace(v) ? null : v.Trim();
                if (!string.IsNullOrWhiteSpace(v)) vazia = false;
            }
            if (vazia) continue;
            list.Add(dict);
        }
        return list;
    }
}
