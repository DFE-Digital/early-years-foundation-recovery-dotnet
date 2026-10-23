using EarlyYearsFoundationRecovery.Infrastructure.Registration;

namespace EarlyYearsFoundationRecovery.UnitTests;

public sealed class RegistrationReferenceDataTests
{
    [Fact]
    public void LoadsCountriesExperiencesRolesAndEnglishLocalAuthorities()
    {
        var data = new RegistrationReferenceData();

        Assert.Contains(data.Countries, item => item.Id == "england" && item.Name == "England");
        Assert.Contains(data.Experiences, item => item.Id == "0-2" && item.Name == "Less than 2 years");
        Assert.Contains(data.Roles, item => item.Name == "Childminder" && item.Group == "childminder");
        Assert.Contains(data.LocalAuthorities, item => item.Name == "Birmingham");
    }
}
