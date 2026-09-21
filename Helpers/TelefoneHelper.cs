using System.Text.RegularExpressions;

namespace DisparoApi.Helpers;

public static class TelefoneHelper
{
    public static string ApenasDigitos(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return Regex.Replace(value, @"\D", "");
    }

    public static (bool ok, string phone, string? motivo) NormalizarTelefone(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return (false, string.Empty, "Telefone vazio");

        var digitos = ApenasDigitos(value);

        if (string.IsNullOrWhiteSpace(digitos))
            return (false, string.Empty, "Telefone vazio");

        digitos = digitos.TrimStart('0');

        if (digitos.Length == 10 || digitos.Length == 11)
            digitos = "55" + digitos;

        if (digitos.Length < 12 || digitos.Length > 13)
            return (false, digitos, "Número inválido");

        if (!digitos.StartsWith("55"))
            return (false, digitos, "Informe o DDI (ex.: 55)");

        return (true, digitos, null);
    }
}
