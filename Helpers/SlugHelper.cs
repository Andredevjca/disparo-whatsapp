using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DisparoApi.Helpers;

public static class SlugHelper
{
    public static string GerarSlugInstancia(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
            nome = "instancia";

        var normalized = nome.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();

        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        var semAcentos = sb.ToString().Normalize(NormalizationForm.FormC);
        var lower = semAcentos.ToLowerInvariant();
        var slug = Regex.Replace(lower, @"[^a-z0-9]+", "-");
        slug = slug.Trim('-');

        if (slug.Length > 24)
            slug = slug.Substring(0, 24).Trim('-');

        if (string.IsNullOrWhiteSpace(slug))
            slug = "instancia";

        var sufixo = DateTime.Now.ToString("yyMMddHHmmssfff");
        return $"{slug}-{sufixo}";
    }
}
