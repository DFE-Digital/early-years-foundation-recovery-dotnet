using EarlyYearsFoundationRecovery.Application.Contracts;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace EarlyYearsFoundationRecovery.Infrastructure.Registration;

public sealed class RegistrationReferenceData
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();

    public IReadOnlyList<RegistrationChoice> Countries { get; } =
        Load("location.yml").Select(item => new RegistrationChoice(item.Id ?? item.Name, item.Name)).ToArray();

    public IReadOnlyList<RegistrationChoice> LocalAuthorities { get; } =
        Load("local-authority.yml").Select(item => new RegistrationChoice(item.Id ?? item.Name, item.Name)).ToArray();

    public IReadOnlyList<RegistrationChoice> Experiences { get; } =
        Load("early-years-experience.yml").Select(item => new RegistrationChoice(item.Id ?? item.Name, item.Name)).ToArray();

    public IReadOnlyList<RegistrationChoice> Roles { get; } =
        Load("role-type.yml")
            .Select(item => new RegistrationChoice(item.Id ?? item.Name, item.Name, item.Group))
            .ToArray();

    private static IReadOnlyList<ReferenceDataRow> Load(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", fileName);
        var yaml = File.ReadAllText(path);
        return Deserializer.Deserialize<List<ReferenceDataRow>>(yaml);
    }

    private sealed class ReferenceDataRow
    {
        public string? Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public string? Group { get; init; }

        public string? HintText { get; init; }
    }
}
