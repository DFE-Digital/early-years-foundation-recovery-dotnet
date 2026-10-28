using System.ComponentModel.DataAnnotations;

namespace EarlyYearsFoundationRecovery.Web.Models;

public sealed class TermsModel
{
    [Range(typeof(bool), "true", "true", ErrorMessage = "You must agree to the terms and conditions.")]
    public bool Accepted { get; set; }
}

public sealed class NameModel
{
    [Display(Name = "First name")]
    [Required]
    [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Display(Name = "Last name")]
    [Required]
    [StringLength(100)]
    public string LastName { get; set; } = string.Empty;
}

public sealed class LocationModel
{
    [Required]
    public string Country { get; set; } = string.Empty;
}

public sealed class LocalAuthorityModel
{
    [Required]
    public string LocalAuthority { get; set; } = string.Empty;
}

public sealed class SettingModel
{
    [Required]
    [StringLength(120)]
    public string SettingType { get; set; } = string.Empty;

    [StringLength(200)]
    public string? SettingTypeOther { get; set; }
}

public sealed class RoleModel
{
    [Required]
    [StringLength(120)]
    public string RoleType { get; set; } = string.Empty;

    [StringLength(200)]
    public string? RoleTypeOther { get; set; }
}

public sealed class ExperienceModel
{
    [Required]
    [StringLength(100)]
    public string EarlyYearsExperience { get; set; } = string.Empty;
}

public sealed class PreferencesModel
{
    [Required]
    public bool? TrainingEmails { get; set; }

    [Required]
    public bool? ResearchParticipant { get; set; }
}

public sealed class CloseAccountModel
{
    [Required]
    public string Reason { get; set; } = string.Empty;

    [StringLength(500)]
    public string? CustomReason { get; set; }
}
