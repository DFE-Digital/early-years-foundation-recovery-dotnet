using System.Text.Json;
using EarlyYearsFoundationRecovery.Application.Contracts;
using EarlyYearsFoundationRecovery.Infrastructure.Contentful;

namespace EarlyYearsFoundationRecovery.Infrastructure.Registration;

public sealed class ContentfulRegistrationOptionsProvider(
    ContentfulDeliveryClient contentful,
    RegistrationReferenceData referenceData) : IRegistrationOptionsProvider
{
    public IReadOnlyList<RegistrationChoice> GetCountries() => referenceData.Countries;

    public IReadOnlyList<RegistrationChoice> GetLocalAuthorities() => referenceData.LocalAuthorities;

    public IReadOnlyList<RegistrationChoice> GetExperiences() => referenceData.Experiences;

    public IReadOnlyList<RegistrationChoice> GetRoles(string roleGroup) =>
        referenceData.Roles.Where(role => role.Group == roleGroup).ToArray();

    public async Task<IReadOnlyList<RegistrationSetting>> GetSettingsAsync(CancellationToken cancellationToken)
    {
        var entries = await contentful.GetEntriesAsync("userSetting", cancellationToken);
        return entries
            .Where(entry => ContentfulDeliveryClient.ContentType(entry) == "userSetting")
            .Select(MapSetting)
            .Where(setting => setting is not null)
            .Cast<RegistrationSetting>()
            .OrderBy(setting => setting.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static RegistrationSetting? MapSetting(JsonElement entry)
    {
        var fields = ContentfulDeliveryClient.Fields(entry);
        if (fields.TryGetProperty("active", out _) &&
            !ContentfulDeliveryClient.BooleanField(fields, "active"))
        {
            return null;
        }

        var id = ContentfulDeliveryClient.StringField(fields, "name");
        var name = ContentfulDeliveryClient.StringField(fields, "title");
        var roleGroup = ContentfulDeliveryClient.StringField(fields, "role_type");
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return new RegistrationSetting(
            id,
            name,
            ContentfulDeliveryClient.BooleanField(fields, "local_authority"),
            string.IsNullOrWhiteSpace(roleGroup) ? "none" : roleGroup);
    }
}
