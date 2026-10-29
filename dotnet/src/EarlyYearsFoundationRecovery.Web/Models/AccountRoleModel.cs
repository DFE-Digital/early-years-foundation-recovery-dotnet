using System.ComponentModel.DataAnnotations;
using EarlyYearsFoundationRecovery.Application.Contracts;

namespace EarlyYearsFoundationRecovery.Web.Models;

public sealed class AccountRoleModel
{
    [Display(Name = "Role")]
    [Required(ErrorMessage = "Select a role.")]
    [StringLength(120)]
    public string RoleType { get; set; } = string.Empty;

    [Display(Name = "Role description")]
    [StringLength(200)]
    public string? RoleTypeOther { get; set; }

    [Display(Name = "Early years experience")]
    [Required(ErrorMessage = "Select your early years experience.")]
    [StringLength(100)]
    public string EarlyYearsExperience { get; set; } = string.Empty;

    public IReadOnlyList<RegistrationChoice> Roles { get; set; } = [];

    public IReadOnlyList<RegistrationChoice> Experiences { get; set; } = [];
}
