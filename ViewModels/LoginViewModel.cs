using System.ComponentModel.DataAnnotations;
namespace DisparoApi.ViewModels;
public class LoginViewModel
{
    [Required(ErrorMessage = "Informe seu e-mail."), EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    public string Email { get; set; } = "";
    [Required(ErrorMessage = "Informe sua senha."), DataType(DataType.Password)]
    public string Password { get; set; } = "";
    public string? ReturnUrl { get; set; }
}
