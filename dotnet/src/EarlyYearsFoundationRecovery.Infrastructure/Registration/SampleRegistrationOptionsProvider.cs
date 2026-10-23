using EarlyYearsFoundationRecovery.Application.Contracts;

namespace EarlyYearsFoundationRecovery.Infrastructure.Registration;

public sealed class SampleRegistrationOptionsProvider(
    RegistrationReferenceData referenceData) : IRegistrationOptionsProvider
{
    private static readonly IReadOnlyList<RegistrationSetting> Settings =
    [
        new("childminder_agency", "Childminder as part of an agency", true, "childminder"),
        new("childminder_independent", "Independent childminder", true, "childminder"),
        new("nursery_local_authority", "Local authority maintained nursery school", true, "other"),
        new("nursery_private", "Private nursery", true, "other"),
        new("nurser_independent", "Independent nursery", true, "other"),
        new("daycare", "Daycare", true, "other"),
        new("preschool", "Preschool", true, "other"),
        new("playgroup", "Playgroup", true, "other"),
        new("childcare_centre", "Childcare centre", true, "other"),
        new("nursery_attached_to_school", "Nursery attached to a school", true, "other"),
        new("free_school", "Free School", true, "other"),
        new("special_school", "Special school", true, "other"),
        new("academy", "Academy", true, "other"),
        new("training_provider", "Training provider", false, "none"),
        new("central_government", "Central government", false, "none"),
        new("department_for_education", "Department for Education", false, "none"),
        new("local_authority", "Local authority", true, "none"),
        new("other", "Other", true, "other")
    ];

    public IReadOnlyList<RegistrationChoice> GetCountries() => referenceData.Countries;

    public IReadOnlyList<RegistrationChoice> GetLocalAuthorities() => referenceData.LocalAuthorities;

    public IReadOnlyList<RegistrationChoice> GetExperiences() => referenceData.Experiences;

    public IReadOnlyList<RegistrationChoice> GetRoles(string roleGroup) =>
        referenceData.Roles.Where(role => role.Group == roleGroup).ToArray();

    public Task<IReadOnlyList<RegistrationSetting>> GetSettingsAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Settings);
}
