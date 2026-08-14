using ExamArchive.Models;
using Microsoft.EntityFrameworkCore;

namespace ExamArchive.Data;

/// <summary>
/// Development-only sample data. Deliberately kept out of the migrations so it
/// never reaches a real deployment, and written in plain EF Core so it survives
/// a change of database provider.
/// </summary>
public static class SeedData
{
    /// <summary>
    /// Course codes for the sample subjects, keyed by name.
    /// </summary>
    /// <remarks>
    /// Kept as a map rather than written inline so it can serve twice: once when
    /// creating subjects on an empty database, and once to backfill databases
    /// that were seeded before the Code column existed.
    /// </remarks>
    private static readonly Dictionary<string, string> SubjectCodes = new()
    {
        ["Mathematics I"] = "MAT101",
        ["Linear Algebra"] = "MAT120",
        ["Statistics"] = "MAT210",
        ["Programming Fundamentals"] = "IT101",
        ["Data Structures and Algorithms"] = "IT230",
        ["Databases"] = "IT240",
        ["Operating Systems"] = "IT310",
        ["Computer Networks"] = "IT320",
        ["Software Architecture"] = "IT330",
        ["Machine Learning"] = "IT410",
        ["Cryptography"] = "IT450",
        ["Digital Electronics"] = "EE110"
    };

    public static async Task SeedAsync(ExamArchiveDbContext db)
    {
        // Runs before the early return below, because a database that was already
        // seeded is exactly the one whose subjects have no code yet.
        await BackfillSubjectCodesAsync(db);

        // Idempotent: if majors already exist, assume seeding has run.
        if (await db.Majors.AnyAsync())
        {
            return;
        }

        // Studies come from the migration's HasData, so look them up rather than insert.
        var bachelors = await db.Studies.SingleAsync(s => s.Name == "Bachelor's");
        var masters = await db.Studies.SingleAsync(s => s.Name == "Master's");

        // ---- Majors ----
        var cs = new Major { Name = "Computer Science", Studies = bachelors };
        var se = new Major { Name = "Software Engineering", Studies = bachelors };
        var ee = new Major { Name = "Electrical Engineering", Studies = bachelors };
        var ds = new Major { Name = "Data Science", Studies = masters };
        var cy = new Major { Name = "Cybersecurity", Studies = masters };

        db.Majors.AddRange(cs, se, ee, ds, cy);

        // ---- Subjects ----
        // Codes come from SubjectCodes so the two places that need them cannot
        // disagree. Their shape is the usual one: department prefix, then a number
        // whose leading digit is roughly the year the course is taught in.
        static Subject Sub(string name) => new() { Name = name, Code = SubjectCodes[name] };

        var math1 = Sub("Mathematics I");
        var linAlg = Sub("Linear Algebra");
        var progFund = Sub("Programming Fundamentals");
        var dsa = Sub("Data Structures and Algorithms");
        var databases = Sub("Databases");
        var os = Sub("Operating Systems");
        var networks = Sub("Computer Networks");
        var ml = Sub("Machine Learning");
        var crypto = Sub("Cryptography");
        var stats = Sub("Statistics");
        var digital = Sub("Digital Electronics");
        var arch = Sub("Software Architecture");

        db.Subjects.AddRange(
            math1, linAlg, progFund, dsa, databases,
            os, networks, ml, crypto, stats, digital, arch);

        // ---- Major <-> Subject links ----
        // Several subjects are shared across majors, and some are taught in a
        // different year depending on the major — that is what YearOfStudy is for.
        static MajorSubject Link(Major major, Subject subject, int year) =>
            new() { Major = major, Subject = subject, YearOfStudy = year };

        db.MajorSubjects.AddRange(
            // Computer Science
            Link(cs, math1, 1),
            Link(cs, progFund, 1),
            Link(cs, linAlg, 1),
            Link(cs, dsa, 2),
            Link(cs, databases, 2),
            Link(cs, os, 3),
            Link(cs, networks, 3),
            Link(cs, ml, 4),

            // Software Engineering — shares most of the CS core,
            // but takes Databases a year later.
            Link(se, math1, 1),
            Link(se, progFund, 1),
            Link(se, dsa, 2),
            Link(se, databases, 3),
            Link(se, arch, 3),

            // Electrical Engineering
            Link(ee, math1, 1),
            Link(ee, digital, 1),
            Link(ee, stats, 2),
            Link(ee, os, 3),

            // Data Science (Master's)
            Link(ds, linAlg, 1),
            Link(ds, stats, 1),
            Link(ds, ml, 1),
            Link(ds, databases, 2),

            // Cybersecurity (Master's)
            Link(cy, networks, 1),
            Link(cy, crypto, 1),
            Link(cy, os, 2),
            Link(cy, arch, 2));

        // ---- Papers ----
        // Months follow a plausible academic calendar: midterms in Nov/Apr,
        // finals in Jan/Jun, resits in Sep.
        static Paper Doc(
            Subject subject,
            ExamType examType,
            int month,
            int year,
            PaperStatus status,
            DateTime uploadedAt,
            string? rejectionReason = null)
        {
            var slug = subject.Name.ToLowerInvariant().Replace(' ', '-');
            return new Paper
            {
                Subject = subject,
                ExamType = examType,
                Month = month,
                Year = year,
                Status = status,
                UploadedAt = uploadedAt,

                // Decided a couple of days after upload — plausible for a queue
                // worked through in batches, and it satisfies CK_Paper_ReviewedAt,
                // which forbids a review timestamp on a paper still pending.
                ReviewedAt = status == PaperStatus.Pending ? null : uploadedAt.AddDays(2),
                RejectionReason = status == PaperStatus.Rejected ? rejectionReason : null,

                // Single-page PDFs, matching what the archive held before uploads
                // could carry several images. No bytes exist on disk for these —
                // they populate listings, and downloading one returns 404.
                Files =
                [
                    new PaperFile
                    {
                        StoredPath = $"/uploads/{year}/{slug}-{examType.ToString().ToLowerInvariant()}-{year}-{month:D2}.pdf",
                        ContentType = PaperFileTypes.Pdf.ContentType,
                        PageNumber = 1,

                        // No bytes exist for these, so the size is unrecorded
                        // rather than invented.
                        SizeBytes = 0
                    }
                ]
            };
        }

        db.Papers.AddRange(
            // Databases — the deepest archive, spanning several years.
            Doc(databases, ExamType.Final, 1, 2024, PaperStatus.Approved, new DateTime(2024, 2, 3, 9, 14, 0, DateTimeKind.Utc)),
            Doc(databases, ExamType.Midterm, 11, 2024, PaperStatus.Approved, new DateTime(2024, 11, 28, 17, 2, 0, DateTimeKind.Utc)),
            Doc(databases, ExamType.Final, 1, 2025, PaperStatus.Approved, new DateTime(2025, 1, 30, 12, 45, 0, DateTimeKind.Utc)),
            Doc(databases, ExamType.Resit, 9, 2025, PaperStatus.Pending, new DateTime(2025, 9, 19, 20, 11, 0, DateTimeKind.Utc)),

            // Data Structures and Algorithms
            Doc(dsa, ExamType.Midterm, 11, 2024, PaperStatus.Approved, new DateTime(2024, 12, 1, 8, 30, 0, DateTimeKind.Utc)),
            Doc(dsa, ExamType.Final, 6, 2025, PaperStatus.Approved, new DateTime(2025, 6, 22, 15, 5, 0, DateTimeKind.Utc)),
            Doc(dsa, ExamType.Final, 6, 2024, PaperStatus.Rejected, new DateTime(2024, 7, 2, 11, 20, 0, DateTimeKind.Utc),
                "Pages 2 and 3 are too blurred to read. Please re-photograph in better light."),

            // Mathematics I — shared by three majors, so this is the busiest subject.
            Doc(math1, ExamType.Midterm, 11, 2025, PaperStatus.Approved, new DateTime(2025, 11, 26, 10, 0, 0, DateTimeKind.Utc)),
            Doc(math1, ExamType.Final, 1, 2025, PaperStatus.Approved, new DateTime(2025, 2, 8, 13, 40, 0, DateTimeKind.Utc)),
            Doc(math1, ExamType.Resit, 9, 2024, PaperStatus.Approved, new DateTime(2024, 9, 25, 16, 55, 0, DateTimeKind.Utc)),
            Doc(math1, ExamType.Final, 1, 2026, PaperStatus.Pending, new DateTime(2026, 1, 29, 18, 12, 0, DateTimeKind.Utc)),

            // Operating Systems
            Doc(os, ExamType.Final, 6, 2025, PaperStatus.Approved, new DateTime(2025, 6, 30, 9, 5, 0, DateTimeKind.Utc)),
            Doc(os, ExamType.Midterm, 4, 2025, PaperStatus.Pending, new DateTime(2025, 4, 14, 21, 33, 0, DateTimeKind.Utc)),

            // Machine Learning
            Doc(ml, ExamType.Final, 6, 2025, PaperStatus.Approved, new DateTime(2025, 6, 18, 14, 22, 0, DateTimeKind.Utc)),
            Doc(ml, ExamType.Midterm, 4, 2026, PaperStatus.Pending, new DateTime(2026, 4, 21, 19, 47, 0, DateTimeKind.Utc)),

            // Cryptography
            Doc(crypto, ExamType.Final, 1, 2025, PaperStatus.Approved, new DateTime(2025, 1, 24, 10, 18, 0, DateTimeKind.Utc)),
            Doc(crypto, ExamType.Resit, 9, 2025, PaperStatus.Rejected, new DateTime(2025, 9, 8, 22, 4, 0, DateTimeKind.Utc),
                "This is the September 2024 paper, not 2025. Please check the date and resubmit."),

            // Computer Networks
            Doc(networks, ExamType.Final, 6, 2024, PaperStatus.Approved, new DateTime(2024, 6, 27, 8, 51, 0, DateTimeKind.Utc)),
            Doc(networks, ExamType.Midterm, 11, 2025, PaperStatus.Pending, new DateTime(2025, 11, 30, 23, 9, 0, DateTimeKind.Utc)),

            // Statistics
            Doc(stats, ExamType.Final, 6, 2025, PaperStatus.Approved, new DateTime(2025, 6, 12, 7, 38, 0, DateTimeKind.Utc)),

            // Programming Fundamentals
            Doc(progFund, ExamType.Final, 1, 2025, PaperStatus.Approved, new DateTime(2025, 1, 21, 11, 2, 0, DateTimeKind.Utc)),
            Doc(progFund, ExamType.Resit, 9, 2025, PaperStatus.Approved, new DateTime(2025, 9, 16, 13, 27, 0, DateTimeKind.Utc)),

            // Software Architecture
            Doc(arch, ExamType.Final, 6, 2025, PaperStatus.Pending, new DateTime(2025, 6, 25, 16, 43, 0, DateTimeKind.Utc)),

            // Digital Electronics
            Doc(digital, ExamType.Midterm, 11, 2024, PaperStatus.Approved, new DateTime(2024, 11, 19, 9, 56, 0, DateTimeKind.Utc)));

        // Linear Algebra is intentionally left with no papers — a subject that is
        // taught but has an empty archive, which the UI will need to handle.

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Fills in course codes on subjects that predate the Code column.
    /// </summary>
    /// <remarks>
    /// Development convenience, not a migration. Backfilling sample data in a
    /// migration would carry it into any real deployment, which is the thing this
    /// file exists to avoid — so it lives here and runs only where seeding runs.
    /// Subjects whose name is not in the map keep a null code, which is a valid
    /// state rather than something to paper over.
    /// </remarks>
    private static async Task BackfillSubjectCodesAsync(ExamArchiveDbContext db)
    {
        var uncoded = await db.Subjects
            .Where(s => s.Code == null)
            .ToListAsync();

        if (uncoded.Count == 0)
        {
            return;
        }

        var changed = false;

        foreach (var subject in uncoded)
        {
            if (SubjectCodes.TryGetValue(subject.Name, out var code))
            {
                subject.Code = code;
                changed = true;
            }
        }

        if (changed)
        {
            await db.SaveChangesAsync();
        }
    }
}
