using System.ComponentModel.DataAnnotations;
using EarlyYearsFoundationRecovery.Application.Contracts;

namespace EarlyYearsFoundationRecovery.Web.Models;

public sealed class AccountRoleModel
{
    [Required]
    [StringLength(120)]
    public string RoleType { get; set; } = string.Empty;

    [StringLength(200)]
    public string? RoleTypeOther { get; set; }

    [Required]
    [StringLength(100)]
    public string EarlyYearsExperience { get; set; } = string.Empty;

    public IReadOnlyList<RegistrationChoice> Roles { get; set; } = [];

    public IReadOnlyList<RegistrationChoice> Experiences { get; set; } = [];
}
