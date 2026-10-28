using System.Globalization;
using System.Security.Claims;
using EarlyYearsFoundationRecovery.Application.Accounts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace EarlyYearsFoundationRecovery.Web.Authentication;

public sealed class AccountCookieEvents(
    AccountService accounts,
    ILogger<AccountCookieEvents> logger) : CookieAuthenticationEvents
{
    private const string AccountIdClaim = "eyfr_account_id";

    public override async Task SigningIn(CookieSigningInContext context)
    {
        var subject = context.Principal?.FindFirstValue("sub");
        var account = string.IsNullOrWhiteSpace(subject)
            ? null
            : await accounts.FindAsync(subject, context.HttpContext.RequestAborted);
        if (account is null || account.IsClosed || context.Principal?.Identity is not ClaimsIdentity identity)
        {
            throw new InvalidOperationException("An active account is required to create a session.");
        }

        foreach (var claim in identity.FindAll(AccountIdClaim).ToArray())
        {
            identity.RemoveClaim(claim);
        }
        identity.AddClaim(new Claim(AccountIdClaim, account.Id.ToString(CultureInfo.InvariantCulture)));
    }

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var subject = context.Principal?.FindFirstValue("sub");
        var accountId = context.Principal?.FindFirstValue(AccountIdClaim);
        var account = string.IsNullOrWhiteSpace(subject)
            ? null
            : await accounts.FindAsync(subject, context.HttpContext.RequestAborted);
        if (account is not null && !account.IsClosed &&
            accountId == account.Id.ToString(CultureInfo.InvariantCulture))
        {
            return;
        }

        logger.LogInformation("Rejecting a session that no longer identifies an active account.");
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(context.Scheme.Name);
    }
}
