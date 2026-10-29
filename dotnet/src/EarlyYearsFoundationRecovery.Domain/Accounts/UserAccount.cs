namespace EarlyYearsFoundationRecovery.Domain.Accounts;

public sealed class UserAccount
{
    private static readonly HashSet<string> ClosureReasons =
        ["not_using", "technical_issue", "other"];

    private UserAccount()
    {
    }

    public UserAccount(string govOneSubject, string email)
    {
        GovOneSubject = Required(govOneSubject, nameof(govOneSubject));
        Email = Required(email, nameof(email));
    }

    public long Id { get; private set; }

    public string GovOneSubject { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public string? FirstName { get; private set; }

    public string? LastName { get; private set; }

    public string? Country { get; private set; }

    public string? LocalAuthority { get; private set; }

    public string? SettingType { get; private set; }

    public string? SettingTypeOther { get; private set; }

    public bool SettingRequiresLocalAuthority { get; private set; }

    public string? RoleGroup { get; private set; }

    public string? RoleType { get; private set; }

    public string? RoleTypeOther { get; private set; }

    public string? EarlyYearsExperience { get; private set; }

    public bool? TrainingEmails { get; private set; }

    public bool? ResearchParticipant { get; private set; }

    public DateTimeOffset? TermsAndConditionsAgreedAt { get; private set; }

    public DateTimeOffset? RegistrationCompletedAt { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public string? ClosedReason { get; private set; }

    public string? ClosedReasonCustom { get; private set; }

    public bool IsRegistrationComplete => RegistrationCompletedAt is not null;

    public bool IsClosed => ClosedAt is not null;

    public void UpdateEmail(string email) =>
        Email = Required(email, nameof(email));

    public void AcceptTerms(DateTimeOffset agreedAt) =>
        TermsAndConditionsAgreedAt = agreedAt;

    public void UpdateName(string firstName, string lastName)
    {
        FirstName = Required(firstName, nameof(firstName));
        LastName = Required(lastName, nameof(lastName));
    }

    public void UpdateLocation(string country, string? localAuthority)
    {
        Country = Required(country, nameof(country));
        LocalAuthority = SettingRequiresLocalAuthority &&
                         string.Equals(country, "England", StringComparison.OrdinalIgnoreCase)
            ? localAuthority
            : null;
    }

    public void UpdateLocalAuthority(string localAuthority)
    {
        if (!SettingRequiresLocalAuthority ||
            !string.Equals(Country, "England", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("A local authority is not required for this account.");
        }

        LocalAuthority = Required(localAuthority, nameof(localAuthority));
    }

    public void UpdateSetting(
        string settingType,
        string? other,
        bool requiresLocalAuthority,
        string roleGroup)
    {
        if (SettingType != settingType)
        {
            LocalAuthority = null;
            RoleType = null;
            RoleTypeOther = null;
            EarlyYearsExperience = null;
        }

        SettingType = Required(settingType, nameof(settingType));
        SettingTypeOther = settingType == "other" ? other : null;
        SettingRequiresLocalAuthority = requiresLocalAuthority;
        RoleGroup = Required(roleGroup, nameof(roleGroup));

        if (RoleGroup == "none")
        {
            RoleType = "N/A";
            RoleTypeOther = null;
            EarlyYearsExperience = null;
        }
    }

    public void UpdateRole(string? roleType, string? other)
    {
        RoleType = roleType;
        RoleTypeOther = string.Equals(roleType, "other", StringComparison.OrdinalIgnoreCase) ? other : null;
    }

    public void UpdateExperience(string? experience) =>
        EarlyYearsExperience = experience;

    public void UpdatePreferences(bool trainingEmails, bool researchParticipant)
    {
        TrainingEmails = trainingEmails;
        ResearchParticipant = researchParticipant;
    }

    public bool CompleteRegistration(DateTimeOffset completedAt)
    {
        if (TermsAndConditionsAgreedAt is null ||
            string.IsNullOrWhiteSpace(FirstName) ||
            string.IsNullOrWhiteSpace(LastName) ||
            string.IsNullOrWhiteSpace(Country) ||
            string.IsNullOrWhiteSpace(SettingType) ||
            (string.Equals(SettingType, "other", StringComparison.OrdinalIgnoreCase) &&
             string.IsNullOrWhiteSpace(SettingTypeOther)) ||
            (SettingRequiresLocalAuthority &&
             string.Equals(Country, "England", StringComparison.OrdinalIgnoreCase) &&
             string.IsNullOrWhiteSpace(LocalAuthority)) ||
            (RoleGroup != "none" &&
             (string.IsNullOrWhiteSpace(RoleType) || string.IsNullOrWhiteSpace(EarlyYearsExperience))) ||
            (string.Equals(RoleType, "other", StringComparison.OrdinalIgnoreCase) &&
             string.IsNullOrWhiteSpace(RoleTypeOther)) ||
            TrainingEmails is null ||
            ResearchParticipant is null)
        {
            return false;
        }

        RegistrationCompletedAt ??= completedAt;
        return true;
    }

    public void SetCloseReason(string reason, string? customReason)
    {
        if (!ClosureReasons.Contains(reason))
        {
            throw new ArgumentException("Select a valid reason for closing the account.", nameof(reason));
        }

        ClosedReason = reason;
        ClosedReasonCustom = reason == "other"
            ? string.IsNullOrWhiteSpace(customReason) ? "No reason provided" : customReason.Trim()
            : null;
    }

    public void Close(DateTimeOffset closedAt)
    {
        if (IsClosed)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(ClosedReason))
        {
            throw new InvalidOperationException("Choose a reason before closing the account.");
        }

        GovOneSubject = $"closed-{Id}-{GovOneSubject}";
        FirstName = "Redacted";
        LastName = "User";
        Email = $"redacted_user{Id}@example.com";
        ClosedAt = closedAt;
    }

    private static string Required(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A value is required.", parameterName)
            : value.Trim();
}
