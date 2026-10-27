namespace EarlyYearsFoundationRecovery.Application.Contracts;

public static class RegistrationOptionsExtensions
{
    public static IReadOnlyList<RegistrationChoice> GetRoleChoices(
        this IRegistrationOptionsProvider options, string roleGroup)
    {
        var roles = options.GetRoles(roleGroup);
        return roleGroup == "other" && !roles.Any(role => role.Name == "other")
            ? [.. roles, new RegistrationChoice("other", "other")]
            : roles;
    }
}
