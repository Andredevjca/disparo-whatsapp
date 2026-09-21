using System.Security.Claims;
using DisparoApi.Repositories;
using DisparoApi.Services;
using DisparoApi.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DisparoApi.Controllers;

public class ContaController(IAuthService auth, IUsuarioRepository usuarios, ILogger<ContaController> logger) : Controller
{
    [AllowAnonymous, HttpGet]
    public IActionResult Entrar(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Painel");
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Entrar(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        try
        {
            await auth.LoginAsync(model.Email, model.Password);
            var usuario = (await usuarios.GetByEmailAsync(model.Email.Trim().ToLowerInvariant()))!;
            var identity = new ClaimsIdentity(new[] {
                new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                new Claim(ClaimTypes.Name, usuario.Nome ?? usuario.Email),
                new Claim(ClaimTypes.Email, usuario.Email),
                new Claim(ClaimTypes.Role, usuario.Role ?? "user")
            }, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
            return Url.IsLocalUrl(model.ReturnUrl) ? LocalRedirect(model.ReturnUrl!) : RedirectToAction("Index", "Painel");
        }
        catch (UnauthorizedAccessException)
        {
            ModelState.AddModelError(string.Empty, "E-mail ou senha inválidos.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao autenticar no banco existente");
            ModelState.AddModelError(string.Empty, "Não foi possível acessar o sistema. Verifique a conexão com o banco de dados.");
        }
        model.Password = string.Empty;
        ModelState.Remove(nameof(model.Password));
        return View(model);
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Sair()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Entrar));
    }

    [AllowAnonymous]
    public IActionResult Erro() { Response.StatusCode = 500; return View("Erro"); }
    [AllowAnonymous]
    public IActionResult AcessoNegado() { Response.StatusCode = 403; return View("AcessoNegado"); }
}
