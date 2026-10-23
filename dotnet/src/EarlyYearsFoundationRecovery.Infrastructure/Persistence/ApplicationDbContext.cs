using EarlyYearsFoundationRecovery.Domain.Accounts;
using EarlyYearsFoundationRecovery.Domain.Training;
using Microsoft.EntityFrameworkCore;

namespace EarlyYearsFoundationRecovery.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options)
{
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();

    public DbSet<TrainingModuleProgress> TrainingModuleProgress => Set<TrainingModuleProgress>();

    public DbSet<AssessmentAttempt> AssessmentAttempts => Set<AssessmentAttempt>();

    public DbSet<TrainingResponse> TrainingResponses => Set<TrainingResponse>();

    public DbSet<LearningNote> LearningNotes => Set<LearningNote>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserAccount>(entity =>
        {
            entity.HasKey(account => account.Id);
            entity.HasIndex(account => account.GovOneSubject).IsUnique();
            entity.Property(account => account.GovOneSubject).HasMaxLength(200).IsRequired();
            entity.Property(account => account.Email).HasMaxLength(320).IsRequired();
            entity.Property(account => account.FirstName).HasMaxLength(100);
            entity.Property(account => account.LastName).HasMaxLength(100);
            entity.Property(account => account.Country).HasMaxLength(100);
            entity.Property(account => account.LocalAuthority).HasMaxLength(120);
            entity.Property(account => account.SettingType).HasMaxLength(120);
            entity.Property(account => account.SettingTypeOther).HasMaxLength(200);
            entity.Property(account => account.RoleType).HasMaxLength(120);
            entity.Property(account => account.RoleTypeOther).HasMaxLength(200);
            entity.Property(account => account.EarlyYearsExperience).HasMaxLength(100);
        });

        modelBuilder.Entity<TrainingModuleProgress>(entity =>
        {
            entity.HasKey(progress => progress.Id);
            entity.HasIndex(progress => new { progress.UserAccountId, progress.ModuleName }).IsUnique();
            entity.HasOne<UserAccount>()
                .WithMany()
                .HasForeignKey(progress => progress.UserAccountId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Property(progress => progress.ModuleName).HasMaxLength(200).IsRequired();
            entity.OwnsMany(progress => progress.VisitedPages, pages =>
            {
                pages.ToTable("visited_training_pages");
                pages.WithOwner().HasForeignKey("TrainingModuleProgressId");
                pages.Property<long>("Id");
                pages.HasKey("Id");
                pages.Property(page => page.Name).HasMaxLength(200).IsRequired();
                pages.Property(page => page.VisitedAt).IsRequired();
                pages.HasIndex("TrainingModuleProgressId", nameof(VisitedPage.Name)).IsUnique();
            });
        });

        modelBuilder.Entity<AssessmentAttempt>(entity =>
        {
            entity.HasKey(attempt => attempt.Id);
            entity.HasIndex(attempt => new { attempt.UserAccountId, attempt.ModuleName, attempt.StartedAt });
            entity.HasOne<UserAccount>()
                .WithMany()
                .HasForeignKey(attempt => attempt.UserAccountId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Property(attempt => attempt.ModuleName).HasMaxLength(200).IsRequired();
            entity.OwnsMany(attempt => attempt.Answers, answers =>
            {
                answers.ToTable("assessment_answers");
                answers.WithOwner().HasForeignKey("AssessmentAttemptId");
                answers.Property<long>("Id");
                answers.HasKey("Id");
                answers.Property(answer => answer.QuestionName).HasMaxLength(200).IsRequired();
                answers.Property(answer => answer.IsCorrect).IsRequired();
                answers.Property(answer => answer.SelectedAnswerIds)
                    .HasColumnName("selected_answer_ids")
                    .HasConversion(
                        values => string.Join('\n', values),
                        value => value.Length == 0 ? new List<string>() : value.Split('\n').ToList())
                    .Metadata.SetValueComparer(
                        new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<IReadOnlyCollection<string>>(
                            (left, right) => left != null && right != null && left.SequenceEqual(right),
                            value => value == null ? 0 : value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
                            value => value == null ? Array.Empty<string>() : value.ToArray()));
            });
        });

        modelBuilder.Entity<LearningNote>(entity =>
        {
            entity.HasKey(note => note.Id);
            entity.HasIndex(note => new { note.UserAccountId, note.ModuleName }).IsUnique();
            entity.HasOne<UserAccount>()
                .WithMany()
                .HasForeignKey(note => note.UserAccountId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Property(note => note.ModuleName).HasMaxLength(200).IsRequired();
            entity.Property(note => note.Content).HasMaxLength(20_000).IsRequired();
        });

        modelBuilder.Entity<TrainingResponse>(entity =>
        {
            entity.HasKey(response => response.Id);
            entity.HasIndex(response => new
            {
                response.UserAccountId,
                response.ModuleName,
                response.QuestionName,
                response.CreatedAt
            });
            entity.HasOne<UserAccount>()
                .WithMany()
                .HasForeignKey(response => response.UserAccountId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Property(response => response.ModuleName).HasMaxLength(200).IsRequired();
            entity.Property(response => response.QuestionName).HasMaxLength(200).IsRequired();
            entity.Property(response => response.ResponseType).HasMaxLength(40).IsRequired();
            entity.Property(response => response.SelectedAnswerIdsJson).HasColumnName("selected_answer_ids").IsRequired();
        });
    }
}
