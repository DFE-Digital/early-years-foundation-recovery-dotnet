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
        Assert.Contains(data.LocalAuthorities, item => item.Id == "Birmingham" && item.Name == "Birmingham");
    }

    [Fact]
    public void AllReferenceChoicesHaveNonemptyUniqueIds()
    {
        var data = new RegistrationReferenceData();

        foreach (var choices in new[] { data.Countries, data.LocalAuthorities, data.Experiences, data.Roles })
        {
            Assert.All(choices, choice => Assert.False(string.IsNullOrWhiteSpace(choice.Id)));
            Assert.Equal(choices.Count, choices.Select(choice => choice.Id).Distinct().Count());
        }
    }
}
