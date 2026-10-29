using System.ComponentModel.DataAnnotations;
using EarlyYearsFoundationRecovery.Application.Contracts;

namespace EarlyYearsFoundationRecovery.Web.Models;

public sealed class AccountLocationModel
{
    [Required(ErrorMessage = "Select a country.")]
    public string Country { get; set; } = string.Empty;

    public string? LocalAuthority { get; set; }

    public bool SettingRequiresLocalAuthority { get; set; }

    public IReadOnlyList<RegistrationChoice> Countries { get; set; } = [];

    public IReadOnlyList<RegistrationChoice> LocalAuthorities { get; set; } = [];
}
