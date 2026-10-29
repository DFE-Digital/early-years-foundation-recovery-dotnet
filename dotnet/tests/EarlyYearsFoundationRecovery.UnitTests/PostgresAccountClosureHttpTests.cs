using System.Net;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EarlyYearsFoundationRecovery.UnitTests;

public sealed partial class PostgresPersistenceTests
{
    [PostgresFact]
    public async Task HttpClosedAccountCannotBeRecoveredByAnotherSessionOrSubsequentSignIn()
    {
        await SeedEditableAccountAsync();
        long closedId;
        await using (var db = CreateContext())
        {
            var account = await db.UserAccounts.SingleAsync();
            closedId = account.Id;
            var service = Journey(db);
            await service.SavePageNoteAsync(account, Module.Name, "first", "Old private reflection", CancellationToken.None);
            await service.RecordPageViewAsync(account, Module.Name, "learning", CancellationToken.None);
            await service.SubmitAnswerAsync(account, Module.Name, "test-one", ["a", "b"], CancellationToken.None);
            await service.SubmitAnswerAsync(account, Module.Name, "test-two", ["yes"], CancellationToken.None);
            Assert.True(await service.CompleteModuleAsync(account, Module.Name, CancellationToken.None));
        }
        using var factory = CreateHttpFactory();
        using var client = CreateRegistrationClient(factory);
        using var otherSession = CreateRegistrationClient(factory);
        using var dormantSession = CreateRegistrationClient(factory);
        AssertHttpRedirect(await SubmitFormAsync(client, "/account/sign-in", "/account/development-sign-in"), "/account");
        AssertHttpRedirect(await SubmitFormAsync(otherSession, "/account/sign-in", "/account/development-sign-in"), "/account");
        AssertHttpRedirect(await SubmitFormAsync(dormantSession, "/account/sign-in", "/account/development-sign-in"), "/account");
        AssertHttpRedirect(await client.GetAsync("/account/close/confirm"), "/account/close");
        using (var invalid = await SubmitFormAsync(client, "/account/close", "/account/close", ("Reason", "unknown")))
        {
            Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
            Assert.Contains("Select a reason for closing your account.", await invalid.Content.ReadAsStringAsync());
        }
        await using (var db = CreateContext())
        {
            var account = await db.UserAccounts.SingleAsync();
            Assert.Null(account.ClosedReason);
            Assert.False(account.IsClosed);
        }
        AssertHttpRedirect(await SubmitFormAsync(client, "/account/close", "/account/close",
            ("Reason", "not_using")), "/account/close/confirm");
        AssertHttpRedirect(await SubmitFormAsync(client, "/account/close/confirm", "/account/close/confirm"), "/account/closed");
        DateTimeOffset closedAt;
        string closedSubject;
        await using (var db = CreateContext())
        {
            var account = await db.UserAccounts.SingleAsync();
            closedAt = Assert.IsType<DateTimeOffset>(account.ClosedAt);
            closedSubject = account.GovOneSubject;
            Assert.Empty(await db.LearningNotes.ToListAsync());
        }
        foreach (var path in new[] { "/account", "/my-account/learning-log", "/training/integration-module/certificate.pdf" })
        {
            using var signedOut = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.Redirect, signedOut.StatusCode);
            Assert.StartsWith("/account/sign-in", signedOut.Headers.Location?.PathAndQuery);
            using var staleSession = await otherSession.GetAsync(path);
            Assert.Equal(HttpStatusCode.Redirect, staleSession.StatusCode);
            Assert.StartsWith("/account/sign-in", staleSession.Headers.Location?.PathAndQuery);
            if (path == "/account")
            {
                Assert.Contains(staleSession.Headers.GetValues("Set-Cookie"),
                    cookie => cookie.StartsWith("__Host-EYFR.Session=;", StringComparison.Ordinal));
            }
        }
        using (var repeated = await SubmitFormAsync(client, "/account/sign-in", "/account/close/confirm"))
        {
            Assert.Equal(HttpStatusCode.Redirect, repeated.StatusCode);
            Assert.StartsWith("/account/sign-in", repeated.Headers.Location?.PathAndQuery);
        }
        AssertHttpRedirect(await SubmitFormAsync(client, "/account/sign-in", "/account/development-sign-in"), "/account");
        using (var dormant = await dormantSession.GetAsync("/account"))
        {
            Assert.Equal(HttpStatusCode.Redirect, dormant.StatusCode);
            Assert.StartsWith("/account/sign-in", dormant.Headers.Location?.PathAndQuery);
        }
        AssertHttpRedirect(await client.GetAsync("/training/integration-module/certificate.pdf"), "/registration/terms-and-conditions");
        AssertHttpRedirect(await client.GetAsync("/my-account/learning-log"), "/registration/terms-and-conditions");
        Assert.DoesNotContain("Old private reflection", await client.GetStringAsync("/account"));
        await using (var db = CreateContext())
        {
            var accounts = await db.UserAccounts.OrderBy(account => account.Id).ToListAsync();
            Assert.Equal(2, accounts.Count);
            var closed = accounts.Single(account => account.Id == closedId);
            Assert.True(closed.IsClosed);
            Assert.Equal(closedAt, closed.ClosedAt);
            Assert.Equal(closedSubject, closed.GovOneSubject);
            Assert.Equal("Redacted", closed.FirstName);
            var fresh = accounts.Single(account => account.Id != closedId);
            Assert.Equal("local-development-user", fresh.GovOneSubject);
            Assert.False(fresh.IsClosed);
            Assert.False(fresh.IsRegistrationComplete);
            Assert.Null(fresh.FirstName);
            var store = new TrainingProgressStore(db);
            Assert.Empty(await store.GetForUserAsync(fresh.Id, CancellationToken.None));
            Assert.Empty(await store.GetAssessmentsAsync(fresh.Id, Module.Name, CancellationToken.None));
            Assert.Empty(await db.LearningNotes.ToListAsync());
            Assert.NotNull((await store.FindAsync(closedId, Module.Name, CancellationToken.None))!.CompletedAt);
            Assert.True(Assert.Single(await store.GetAssessmentsAsync(closedId, Module.Name, CancellationToken.None)).Passed);
        }
    }
}
