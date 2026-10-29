using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using EarlyYearsFoundationRecovery.Domain.Training;
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
        Assert.Contains(entityTypes, entity => entity.GetTableName() == "ConfidenceCheckProgress");
        var confidence = db.Model.FindEntityType(typeof(ConfidenceCheckProgress))!;
        var confidenceIdentity = Assert.Single(confidence.GetIndexes());
        Assert.True(confidenceIdentity.IsUnique);
        Assert.Equal(["UserAccountId", "ModuleName", "CheckType"],
            confidenceIdentity.Properties.Select(property => property.Name));
        var notes = db.Model.FindEntityType(typeof(LearningNote))!;
        var identity = Assert.Single(notes.GetIndexes());
        Assert.True(identity.IsUnique);
        Assert.Equal(["UserAccountId", "ModuleName", "PageName"], identity.Properties.Select(property => property.Name));
        Assert.False(notes.FindProperty(nameof(LearningNote.PageName))!.IsNullable);
    }
}
