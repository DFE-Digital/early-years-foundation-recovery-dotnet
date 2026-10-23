using System.Security.Claims;
using EarlyYearsFoundationRecovery.Application.Accounts;
using EarlyYearsFoundationRecovery.Domain.Accounts;
using EarlyYearsFoundationRecovery.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EarlyYearsFoundationRecovery.Web.Controllers;

[Route("account")]
public sealed class AccountController(
    AccountService accounts,
    IWebHostEnvironment environment) : Controller
{
    [AllowAnonymous]
    [HttpGet("sign-in")]
    public IActionResult SignIn()
    {
        if (!environment.IsDevelopment())
        {
            return Challenge(
                new AuthenticationProperties { RedirectUri = Url.Action(nameof(Index)) },
                OpenIdConnectDefaults.AuthenticationScheme);
        }

        return View();
    }

    [AllowAnonymous]
    [HttpPost("development-sign-in")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DevelopmentSignIn(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        const string subject = "local-development-user";
        const string email = "local-user@example.test";
        await accounts.EnsureAccountAsync(subject, email, cancellationToken);
        var identity = new ClaimsIdentity(
        [
            new Claim("sub", subject),
            new Claim("email", email)
        ],
        CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));
        return RedirectToAction(nameof(Index));
    }

    [Authorize]
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var account = await GetCurrentAccountAsync(cancellationToken);
        return account is null ? NotFound() : View(account);
    }

    [Authorize]
    [HttpGet("close")]
    public IActionResult Close() => View(new CloseAccountModel());

    [Authorize]
    [HttpPost("close")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Close(CloseAccountModel model, CancellationToken cancellationToken)
    {
        if (model.Reason is not ("not_using" or "technical_issue" or "other"))
        {
            ModelState.AddModelError(nameof(model.Reason), "Select a reason for closing your account.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var account = await GetCurrentAccountAsync(cancellationToken);
        if (account is null)
        {
            return NotFound();
        }

        await accounts.SetCloseReasonAsync(account, model.Reason, model.CustomReason, cancellationToken);
        return RedirectToAction(nameof(ConfirmClose));
    }

    [Authorize]
    [HttpGet("close/confirm")]
    public async Task<IActionResult> ConfirmClose(CancellationToken cancellationToken)
    {
        var account = await GetCurrentAccountAsync(cancellationToken);
        if (account is null)
        {
            return NotFound();
        }

        return string.IsNullOrWhiteSpace(account.ClosedReason)
            ? RedirectToAction(nameof(Close))
            : View(account);
    }

    [Authorize]
    [HttpPost("close/confirm")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmClosePost(CancellationToken cancellationToken)
    {
        var account = await GetCurrentAccountAsync(cancellationToken);
        if (account is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(account.ClosedReason))
        {
            return RedirectToAction(nameof(Close));
        }

        await accounts.CloseAsync(account, cancellationToken);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Closed));
    }

    [AllowAnonymous]
    [HttpGet("closed")]
    public IActionResult Closed() => View();

    [Authorize]
    [HttpPost("sign-out")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SignOutUser()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (!environment.IsDevelopment())
        {
            return SignOut(
                new AuthenticationProperties { RedirectUri = Url.Action(nameof(SignIn), "Account", null, Request.Scheme) },
                OpenIdConnectDefaults.AuthenticationScheme);
        }

        return RedirectToAction(nameof(SignIn));
    }

    private Task<UserAccount?> GetCurrentAccountAsync(CancellationToken cancellationToken)
    {
        var subject = User.FindFirstValue("sub");
        return string.IsNullOrWhiteSpace(subject)
            ? Task.FromResult<UserAccount?>(null)
            : accounts.FindAsync(subject, cancellationToken);
    }
}
