using EarlyYearsFoundationRecovery.Domain.Accounts;

namespace EarlyYearsFoundationRecovery.UnitTests;

public sealed class UserAccountTests
{
    [Fact]
    public void RegistrationCannotCompleteUntilRequiredFieldsArePresent()
    {
        var account = new UserAccount("subject-1", "person@example.test");

        Assert.False(account.CompleteRegistration(DateTimeOffset.UtcNow));

        account.AcceptTerms(DateTimeOffset.UtcNow);
        account.UpdateName("Alex", "Example");
        account.UpdateLocation("England", "Leeds");
        account.UpdateSetting("nursery", null, requiresLocalAuthority: true, roleGroup: "other");
        account.UpdateLocalAuthority("Leeds");
        account.UpdateRole("Early years practitioner", null);
        account.UpdateExperience("0-2");
        account.UpdatePreferences(trainingEmails: false, researchParticipant: false);

        Assert.True(account.CompleteRegistration(DateTimeOffset.UtcNow));
        Assert.True(account.IsRegistrationComplete);
    }

    [Fact]
    public void UpdateLocationClearsLocalAuthorityOutsideEngland()
    {
        var account = new UserAccount("subject-1", "person@example.test");

        account.UpdateLocation("England", "Leeds");
        account.UpdateLocation("Scotland", "Leeds");

        Assert.Equal("Scotland", account.Country);
        Assert.Null(account.LocalAuthority);
    }

    [Fact]
    public void CloseIsIdempotent()
    {
        var account = new UserAccount("subject-1", "person@example.test");
        var closedAt = DateTimeOffset.UtcNow;

        account.SetCloseReason("not_using", null);
        account.Close(closedAt);
        account.Close(closedAt.AddMinutes(1));

        Assert.Equal(closedAt, account.ClosedAt);
    }
}
