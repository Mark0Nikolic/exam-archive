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
    public static async Task SeedAsync(ExamArchiveDbContext db)
    {
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
        var math1 = new Subject { Name = "Mathematics I" };
        var linAlg = new Subject { Name = "Linear Algebra" };
        var progFund = new Subject { Name = "Programming Fundamentals" };
        var dsa = new Subject { Name = "Data Structures and Algorithms" };
        var databases = new Subject { Name = "Databases" };
        var os = new Subject { Name = "Operating Systems" };
        var networks = new Subject { Name = "Computer Networks" };
        var ml = new Subject { Name = "Machine Learning" };
        var crypto = new Subject { Name = "Cryptography" };
        var stats = new Subject { Name = "Statistics" };
        var digital = new Subject { Name = "Digital Electronics" };
        var arch = new Subject { Name = "Software Architecture" };

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
        static Paper Doc(Subject subject, string examType, int month, int year, string status, DateTime uploadedAt)
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
                FilePath = $"/uploads/{year}/{slug}-{examType.ToLowerInvariant()}-{year}-{month:D2}.pdf"
            };
        }

        db.Papers.AddRange(
            // Databases — the deepest archive, spanning several years.
            Doc(databases, "Final", 1, 2024, "Approved", new DateTime(2024, 2, 3, 9, 14, 0, DateTimeKind.Utc)),
            Doc(databases, "Midterm", 11, 2024, "Approved", new DateTime(2024, 11, 28, 17, 2, 0, DateTimeKind.Utc)),
            Doc(databases, "Final", 1, 2025, "Approved", new DateTime(2025, 1, 30, 12, 45, 0, DateTimeKind.Utc)),
            Doc(databases, "Resit", 9, 2025, "Pending", new DateTime(2025, 9, 19, 20, 11, 0, DateTimeKind.Utc)),

            // Data Structures and Algorithms
            Doc(dsa, "Midterm", 11, 2024, "Approved", new DateTime(2024, 12, 1, 8, 30, 0, DateTimeKind.Utc)),
            Doc(dsa, "Final", 6, 2025, "Approved", new DateTime(2025, 6, 22, 15, 5, 0, DateTimeKind.Utc)),
            Doc(dsa, "Final", 6, 2024, "Rejected", new DateTime(2024, 7, 2, 11, 20, 0, DateTimeKind.Utc)),

            // Mathematics I — shared by three majors, so this is the busiest subject.
            Doc(math1, "Midterm", 11, 2025, "Approved", new DateTime(2025, 11, 26, 10, 0, 0, DateTimeKind.Utc)),
            Doc(math1, "Final", 1, 2025, "Approved", new DateTime(2025, 2, 8, 13, 40, 0, DateTimeKind.Utc)),
            Doc(math1, "Resit", 9, 2024, "Approved", new DateTime(2024, 9, 25, 16, 55, 0, DateTimeKind.Utc)),
            Doc(math1, "Final", 1, 2026, "Pending", new DateTime(2026, 1, 29, 18, 12, 0, DateTimeKind.Utc)),

            // Operating Systems
            Doc(os, "Final", 6, 2025, "Approved", new DateTime(2025, 6, 30, 9, 5, 0, DateTimeKind.Utc)),
            Doc(os, "Midterm", 4, 2025, "Pending", new DateTime(2025, 4, 14, 21, 33, 0, DateTimeKind.Utc)),

            // Machine Learning
            Doc(ml, "Final", 6, 2025, "Approved", new DateTime(2025, 6, 18, 14, 22, 0, DateTimeKind.Utc)),
            Doc(ml, "Midterm", 4, 2026, "Pending", new DateTime(2026, 4, 21, 19, 47, 0, DateTimeKind.Utc)),

            // Cryptography
            Doc(crypto, "Final", 1, 2025, "Approved", new DateTime(2025, 1, 24, 10, 18, 0, DateTimeKind.Utc)),
            Doc(crypto, "Resit", 9, 2025, "Rejected", new DateTime(2025, 9, 8, 22, 4, 0, DateTimeKind.Utc)),

            // Computer Networks
            Doc(networks, "Final", 6, 2024, "Approved", new DateTime(2024, 6, 27, 8, 51, 0, DateTimeKind.Utc)),
            Doc(networks, "Midterm", 11, 2025, "Pending", new DateTime(2025, 11, 30, 23, 9, 0, DateTimeKind.Utc)),

            // Statistics
            Doc(stats, "Final", 6, 2025, "Approved", new DateTime(2025, 6, 12, 7, 38, 0, DateTimeKind.Utc)),

            // Programming Fundamentals
            Doc(progFund, "Final", 1, 2025, "Approved", new DateTime(2025, 1, 21, 11, 2, 0, DateTimeKind.Utc)),
            Doc(progFund, "Resit", 9, 2025, "Approved", new DateTime(2025, 9, 16, 13, 27, 0, DateTimeKind.Utc)),

            // Software Architecture
            Doc(arch, "Final", 6, 2025, "Pending", new DateTime(2025, 6, 25, 16, 43, 0, DateTimeKind.Utc)),

            // Digital Electronics
            Doc(digital, "Midterm", 11, 2024, "Approved", new DateTime(2024, 11, 19, 9, 56, 0, DateTimeKind.Utc)));

        // Linear Algebra is intentionally left with no papers — a subject that is
        // taught but has an empty archive, which the UI will need to handle.

        await db.SaveChangesAsync();
    }
}
