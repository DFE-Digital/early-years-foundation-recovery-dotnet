using EarlyYearsFoundationRecovery.Application.Contracts;
using EarlyYearsFoundationRecovery.Domain.Accounts;

namespace EarlyYearsFoundationRecovery.Application.Accounts;

public sealed class AccountService(
    IUserAccountStore accounts,
    ILearningNoteStore notes,
    TimeProvider timeProvider)
{
    public Task<UserAccount?> FindAsync(string subject, CancellationToken cancellationToken) =>
        accounts.FindByGovOneSubjectAsync(subject, cancellationToken);

    public async Task<UserAccount> EnsureAccountAsync(
        string subject,
        string email,
        CancellationToken cancellationToken)
    {
        var account = await accounts.FindByGovOneSubjectAsync(subject, cancellationToken);
        if (account is not null)
        {
            if (account.IsClosed)
            {
                throw new InvalidOperationException("This account has been closed.");
            }

            account.UpdateEmail(email);
            await accounts.SaveChangesAsync(cancellationToken);
            return account;
        }

        account = new UserAccount(subject, email);
        await accounts.AddAsync(account, cancellationToken);
        await accounts.SaveChangesAsync(cancellationToken);
        return account;
    }

    public async Task UpdateTermsAsync(
        UserAccount account,
        CancellationToken cancellationToken)
    {
        account.AcceptTerms(timeProvider.GetUtcNow());
        await accounts.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateNameAsync(
        UserAccount account,
        string firstName,
        string lastName,
        CancellationToken cancellationToken)
    {
        account.UpdateName(firstName, lastName);
        await accounts.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateLocationAsync(
        UserAccount account,
        string country,
        string? localAuthority,
        CancellationToken cancellationToken)
    {
        account.UpdateLocation(country, localAuthority);
        await accounts.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateSettingAsync(
        UserAccount account,
        string settingType,
        string? other,
        bool requiresLocalAuthority,
        string roleGroup,
        CancellationToken cancellationToken)
    {
        account.UpdateSetting(settingType, other, requiresLocalAuthority, roleGroup);
        await accounts.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateLocalAuthorityAsync(
        UserAccount account,
        string localAuthority,
        CancellationToken cancellationToken)
    {
        account.UpdateLocalAuthority(localAuthority);
        await accounts.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateRoleAsync(
        UserAccount account,
        string? roleType,
        string? other,
        CancellationToken cancellationToken)
    {
        account.UpdateRole(roleType, other);
        await accounts.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateExperienceAsync(
        UserAccount account,
        string? experience,
        CancellationToken cancellationToken)
    {
        account.UpdateExperience(experience);
        await accounts.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdatePreferencesAsync(
        UserAccount account,
        bool trainingEmails,
        bool researchParticipant,
        CancellationToken cancellationToken)
    {
        account.UpdatePreferences(trainingEmails, researchParticipant);
        await accounts.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> CompleteRegistrationAsync(
        UserAccount account,
        CancellationToken cancellationToken)
    {
        var complete = account.CompleteRegistration(timeProvider.GetUtcNow());
        if (complete)
        {
            await accounts.SaveChangesAsync(cancellationToken);
        }

        return complete;
    }

    public async Task SetCloseReasonAsync(
        UserAccount account,
        string reason,
        string? customReason,
        CancellationToken cancellationToken)
    {
        account.SetCloseReason(reason, customReason);
        await accounts.SaveChangesAsync(cancellationToken);
    }

    public async Task CloseAsync(UserAccount account, CancellationToken cancellationToken)
    {
        account.Close(timeProvider.GetUtcNow());
        await notes.DeleteForUserAsync(account.Id, cancellationToken);
        await accounts.SaveChangesAsync(cancellationToken);
    }
}
