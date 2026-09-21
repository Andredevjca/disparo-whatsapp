namespace DisparoApi.Dtos;
public class ImagemEnvioDto
{
    public string Base64 { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public string NomeArquivo { get; set; } = string.Empty;
    public void Validar()
    {
        const int limite = 5 * 1024 * 1024;
        if (string.IsNullOrWhiteSpace(Base64)) throw new ArgumentException("Selecione uma imagem válida.");
        if (Base64.Length > 4 * ((limite + 2) / 3))
            throw new ArgumentException("A imagem deve ter no máximo 5 MB.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(Base64); }
        catch (FormatException) { throw new ArgumentException("Imagem em base64 inválida."); }
        if (bytes.Length == 0 || bytes.Length > limite)
            throw new ArgumentException("A imagem deve ter entre 1 byte e 5 MB.");
        var png = bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10});
        var jpeg = bytes.Length >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255;
        if (!(MimeType == "image/png" && png) && !(MimeType == "image/jpeg" && jpeg))
            throw new ArgumentException("Selecione uma imagem PNG ou JPEG válida.");
        Base64 = Convert.ToBase64String(bytes);
        NomeArquivo = MimeType == "image/png" ? "imagem.png" : "imagem.jpg";
    }
}
