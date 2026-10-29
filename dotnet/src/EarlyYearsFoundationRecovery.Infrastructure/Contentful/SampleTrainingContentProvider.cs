using EarlyYearsFoundationRecovery.Application.Contracts;

namespace EarlyYearsFoundationRecovery.Infrastructure.Contentful;

public sealed class SampleTrainingContentProvider : ITrainingContentProvider
{
    private static readonly IReadOnlyList<TrainingModule> Modules =
    [
        new TrainingModule(
            "getting-started",
            "Getting started with child development",
            1,
            false,
            [
                new TrainingPage(
                    "welcome",
                    "Welcome",
                    "topic_intro",
                    "This local sample demonstrates the training journey. Connect a Contentful environment to load service content.")
            ])
    ];

    public Task<IReadOnlyList<TrainingModule>> GetModulesAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Modules);

    public Task<TrainingModule?> GetModuleAsync(string moduleName, CancellationToken cancellationToken) =>
        Task.FromResult(Modules.SingleOrDefault(module => module.Name == moduleName));
}
