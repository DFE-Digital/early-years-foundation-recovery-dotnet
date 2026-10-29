using System.ComponentModel.DataAnnotations;

namespace EarlyYearsFoundationRecovery.Web.Models;

public sealed class AccountPreferenceModel
{
    [Required(ErrorMessage = "Select yes or no.")]
    public bool? Selected { get; set; }
}
