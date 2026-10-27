using System.ComponentModel.DataAnnotations;
using EarlyYearsFoundationRecovery.Application.Contracts;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace EarlyYearsFoundationRecovery.Web.Models;

public sealed class AccountSettingModel
{
    [Required]
    [StringLength(120)]
    public string SettingType { get; set; } = string.Empty;

    [StringLength(200)]
    public string? SettingTypeOther { get; set; }

    [StringLength(120)]
    public string? LocalAuthority { get; set; }

    [StringLength(120)]
    public string? RoleType { get; set; }

    [StringLength(200)]
    public string? RoleTypeOther { get; set; }

    [StringLength(100)]
    public string? EarlyYearsExperience { get; set; }

    [BindNever]
    public bool ShowDetails { get; set; }

    [BindNever]
    public bool RequiresLocalAuthority { get; set; }

    [BindNever]
    public RegistrationSetting? SelectedSetting { get; set; }

    [BindNever]
    public IReadOnlyList<RegistrationSetting> Settings { get; set; } = [];

    [BindNever]
    public IReadOnlyList<RegistrationChoice> LocalAuthorities { get; set; } = [];

    [BindNever]
    public IReadOnlyList<RegistrationChoice> Roles { get; set; } = [];

    [BindNever]
    public IReadOnlyList<RegistrationChoice> Experiences { get; set; } = [];
}
