using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DisparoApi.Helpers;

public static class InterpolacaoHelper
{
    public static string NormalizarCabecalho(string chave)
    {
        if (string.IsNullOrWhiteSpace(chave))
            return string.Empty;

        var normalized = chave.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();

        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC).Trim().ToLowerInvariant();
    }

    public static string Interpolar(string template, IDictionary<string, object?>? dados)
    {
        if (string.IsNullOrWhiteSpace(template))
            return string.Empty;

        if (dados == null || dados.Count == 0)
            return template;

        var dadosNormalizados = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in dados)
        {
            var chaveNorm = NormalizarCabecalho(kvp.Key);
            dadosNormalizados[chaveNorm] = kvp.Value;
        }

        var pattern = @"\{\{\s*([^{}]+?)\s*\}\}";
        return Regex.Replace(template, pattern, match =>
        {
            var chave = NormalizarCabecalho(match.Groups[1].Value);
            if (dadosNormalizados.TryGetValue(chave, out var valor) && valor != null)
                return valor.ToString() ?? match.Value;
            return match.Value;
        });
    }
}
