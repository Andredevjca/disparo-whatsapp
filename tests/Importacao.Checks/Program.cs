using ClosedXML.Excel;
using DisparoApi.Models;
using DisparoApi.Repositories;
using DisparoApi.Services;
using Microsoft.AspNetCore.Http;
using System.Reflection;
using System.Text;

void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
foreach (var extension in new[] { "csv", "xlsx" })
foreach (var header in new[] { "FONE", " fone ", "TELEFONE", "CELULAR", "WHATSAPP" })
{
    using var stream = new MemoryStream();
    if (extension == "csv")
    {
        var bytes = Encoding.UTF8.GetBytes($"NOME;{header};SETOR\nTeste;85986641932;A\nDuplicado;5585986641932;B\nInvalido;123;C");
        stream.Write(bytes);
    }
    else
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Contatos");
        sheet.Cell(1, 1).Value = "NOME"; sheet.Cell(1, 2).Value = header; sheet.Cell(1, 3).Value = "SETOR";
        sheet.Cell(2, 1).Value = "Teste"; sheet.Cell(2, 2).Value = 85986641932d; sheet.Cell(2, 3).Value = "A";
        sheet.Cell(3, 1).Value = "Duplicado"; sheet.Cell(3, 2).Value = "5585986641932";
        sheet.Cell(4, 1).Value = "Invalido"; sheet.Cell(4, 2).Value = "123";
        workbook.SaveAs(stream);
    }
    stream.Position = 0;
    var repo = DispatchProxy.Create<IImportacaoRepository, CaptureRepository>();
    var capture = (CaptureRepository)repo;
    var result = await new ImportacaoService(repo).ImportarArquivoAsync(1, new FormFile(stream, 0, stream.Length, "arquivo", $"teste.{extension}"), "Teste");
    Check(result.Total == 3 && result.Validos == 1 && result.Duplicados == 1 && result.Invalidos == 1, $"Contagens: {extension}/{header}");
    Check(capture.Contatos![0].TelefoneNormalizado == "5585986641932", "Normalizacao com DDI");
    Check(capture.Contatos[0].Dados!.Contains("SETOR") && !capture.Contatos[0].Dados!.Contains(header.Trim()), "Coluna telefone nao deve ficar nos extras");
}
var missingRepo = DispatchProxy.Create<IImportacaoRepository, CaptureRepository>();
using var missing = new MemoryStream(Encoding.UTF8.GetBytes("NOME;SETOR\nTeste;A"));
try
{
    await new ImportacaoService(missingRepo).ImportarArquivoAsync(1, new FormFile(missing, 0, missing.Length, "arquivo", "teste.csv"), "Teste");
    throw new Exception("Planilha sem telefone aceita");
}
catch (ArgumentException e) { Check(e.Message.Contains("Coluna de telefone"), "Mensagem clara"); }
Check(((CaptureRepository)missingRepo).Calls == 0, "Nao deve criar grupo sem coluna telefone");
Console.WriteLine("OK: CSV/XLSX, aliases, DDI, duplicados, invalidos, extras e coluna ausente.");

public class CaptureRepository : DispatchProxy
{
    public List<ContatoImportado>? Contatos;
    public int Calls;
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        Calls++;
        switch (method!.Name)
        {
            case "CriarGrupoAsync": return Task.FromResult(1);
            case "InserirContatosBulkAsync": Contatos = (List<ContatoImportado>)args![2]!; return Task.CompletedTask;
            case "AtualizarGrupoContagemAsync": return Task.CompletedTask;
            default: throw new NotSupportedException(method.Name);
        }
    }
}
