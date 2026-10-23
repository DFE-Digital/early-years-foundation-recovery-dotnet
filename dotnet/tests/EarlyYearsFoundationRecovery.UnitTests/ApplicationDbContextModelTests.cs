using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EarlyYearsFoundationRecovery.UnitTests;

public sealed class ApplicationDbContextModelTests
{
    [Fact]
    public void ModelContainsCoreEntitiesAndOwnedResponses()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=eyfr_test;Username=test;Password=test")
            .Options;

        using var db = new ApplicationDbContext(options);
        var entityTypes = db.Model.GetEntityTypes().ToArray();

        Assert.Contains(entityTypes, entity => entity.GetTableName() == "UserAccounts");
        Assert.Contains(entityTypes, entity => entity.GetTableName() == "TrainingModuleProgress");
        Assert.Contains(entityTypes, entity => entity.GetTableName() == "AssessmentAttempts");
        Assert.Contains(entityTypes, entity => entity.GetTableName() == "TrainingResponses");
        Assert.Contains(entityTypes, entity => entity.GetTableName() == "assessment_answers");
        Assert.Contains(entityTypes, entity => entity.GetTableName() == "visited_training_pages");
        Assert.Contains(entityTypes, entity => entity.GetTableName() == "LearningNotes");
    }
}
