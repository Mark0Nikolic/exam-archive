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
    public DbSet<PaperFile> PaperFiles => Set<PaperFile>();

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

            entity.Property(s => s.Code)
                .HasMaxLength(20);

            // Unique, so the database itself rejects a duplicate code no matter
            // what inserts it. A code that can repeat is not an identifier, just a
            // second name. Rows without a code are exempt: SQLite treats NULLs as
            // distinct from one another in a unique index, which is what allows
            // subjects to exist before their code is known.
            entity.HasIndex(s => s.Code)
                .IsUnique();
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

            entity.ToTable(t =>
            {
                // Six covers a 3-4 year bachelor's and a 1-2 year master's. Without
                // this the column accepts year 47 or -3 as readily as year 2.
                t.HasCheckConstraint(
                    "CK_MajorSubject_YearOfStudy",
                    "[YearOfStudy] >= 1 AND [YearOfStudy] <= 6");
            });
        });

        modelBuilder.Entity<Paper>(entity =>
        {
            // Stored as the enum's name for the same reasons as Status below.
            entity.Property(p => p.ExamType)
                .IsRequired()
                .HasConversion<string>()
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
            //
            // The conversion on the way out is what makes that true in practice.
            // SQLite has no date type, so a DateTime read back from TEXT arrives
            // with Kind = Unspecified, and System.Text.Json then writes it without
            // a trailing Z. A browser parsing "2026-08-14T11:42:47" treats it as
            // local time, so every timestamp was landing hours off. Stamping the
            // kind on read makes the serialized form say what the column means.
            entity.Property(p => p.UploadedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasConversion(
                    value => value,
                    value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

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

        modelBuilder.Entity<PaperFile>(entity =>
        {
            entity.Property(f => f.StoredPath)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(f => f.ContentType)
                .IsRequired()
                .HasMaxLength(100);

            // Cascade: a file has no meaning once its paper is gone. Note this
            // deletes rows, not the bytes on disk — whatever removes a paper is
            // responsible for the files, or they leak.
            entity.HasOne(f => f.Paper)
                .WithMany(p => p.Files)
                .HasForeignKey(f => f.PaperId)
                .OnDelete(DeleteBehavior.Cascade);

            // Unique rather than merely indexed: two rows claiming the same page
            // of the same paper would make the reading order ambiguous, and the
            // database is the only place that can actually rule it out.
            entity.HasIndex(f => new { f.PaperId, f.PageNumber })
                .IsUnique();

            entity.ToTable(t =>
            {
                // Built from PaperFileTypes so the constraint cannot drift from the
                // validation. Adding a format there registers as a model change and
                // EF will ask for a migration, which is the intended nudge.
                var contentTypes = string.Join(
                    ", ",
                    PaperFileTypes.All.Select(type => $"'{type.ContentType}'"));

                t.HasCheckConstraint(
                    "CK_PaperFile_ContentType",
                    $"[ContentType] IN ({contentTypes})");

                t.HasCheckConstraint(
                    "CK_PaperFile_PageNumber",
                    "[PageNumber] >= 1");

                // Zero is permitted and means "size not recorded" — rows migrated
                // from the single-FilePath schema predate size tracking, and SQL
                // cannot measure a file on disk to backfill them. Uploads always
                // write a real size, so only historical rows carry 0.
                t.HasCheckConstraint(
                    "CK_PaperFile_SizeBytes",
                    "[SizeBytes] >= 0");
            });
        });
    }
}
