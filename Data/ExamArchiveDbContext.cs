using ExamArchive.Models;
using Microsoft.EntityFrameworkCore;

namespace ExamArchive.Data;

public class ExamArchiveDbContext : DbContext
{
    public ExamArchiveDbContext(DbContextOptions<ExamArchiveDbContext> options)
        : base(options)
    {
    }

    public DbSet<Studies> Studies => Set<Studies>();
    public DbSet<Major> Majors => Set<Major>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<MajorSubject> MajorSubjects => Set<MajorSubject>();
    public DbSet<Paper> Papers => Set<Paper>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Studies>(entity =>
        {
            entity.Property(s => s.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.HasData(
                new Studies { Id = 1, Name = "Bachelor's" },
                new Studies { Id = 2, Name = "Master's" });
        });

        modelBuilder.Entity<Major>(entity =>
        {
            entity.Property(m => m.Name)
                .IsRequired()
                .HasMaxLength(200);

            // Restrict: a Studies level cannot be deleted while majors still reference it.
            entity.HasOne(m => m.Studies)
                .WithMany(s => s.Majors)
                .HasForeignKey(m => m.StudiesId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Subject>(entity =>
        {
            entity.Property(s => s.Name)
                .IsRequired()
                .HasMaxLength(200);
        });

        modelBuilder.Entity<MajorSubject>(entity =>
        {
            // Composite primary key — no surrogate Id column.
            entity.HasKey(ms => new { ms.MajorId, ms.SubjectId });

            // Cascade: deleting either side removes the link row, which has no
            // meaning on its own.
            entity.HasOne(ms => ms.Major)
                .WithMany(m => m.MajorSubjects)
                .HasForeignKey(ms => ms.MajorId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ms => ms.Subject)
                .WithMany(s => s.MajorSubjects)
                .HasForeignKey(ms => ms.SubjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Paper>(entity =>
        {
            entity.Property(p => p.FilePath)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(p => p.ExamType)
                .IsRequired()
                .HasMaxLength(20);

            // Stored as the enum's name, not its number: it keeps the existing
            // text column and CK_Paper_Status constraint working, and leaves the
            // table readable by eye in a SQLite browser.
            entity.Property(p => p.Status)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasDefaultValue(PaperStatus.Pending);

            // Filled in by SQLite on insert; stored as UTC.
            entity.Property(p => p.UploadedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Restrict: a subject cannot be deleted while it still has papers.
            entity.HasOne(p => p.Subject)
                .WithMany(s => s.Papers)
                .HasForeignKey(p => p.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);

            // Common lookup: papers for a subject, newest first.
            entity.HasIndex(p => new { p.SubjectId, p.Year, p.Month });

            entity.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_Paper_Month",
                    "[Month] >= 1 AND [Month] <= 12");

                t.HasCheckConstraint(
                    "CK_Paper_ExamType",
                    "[ExamType] IN ('Midterm', 'Final', 'Resit')");

                t.HasCheckConstraint(
                    "CK_Paper_Status",
                    "[Status] IN ('Pending', 'Approved', 'Rejected')");
            });
        });
    }
}
