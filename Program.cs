using System.Text.Json.Serialization;
using ExamArchive.Data;
using ExamArchive.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'Default' was not found.");

// SQLite resolves a relative Data Source against the process working directory,
// not the application folder. Launched from anywhere else — a published build, a
// service, an IDE with a different working directory — that silently creates and
// uses a second, empty database instead of failing. Anchoring the path to the
// content root makes the same file resolve no matter where the app starts from.
var connection = new SqliteConnectionStringBuilder(connectionString);
if (!string.IsNullOrEmpty(connection.DataSource)
    && !connection.DataSource.Equals(":memory:", StringComparison.Ordinal)
    && !Path.IsPathRooted(connection.DataSource))
{
    connection.DataSource = Path.Combine(builder.Environment.ContentRootPath, connection.DataSource);
}

builder.Services.AddDbContext<ExamArchiveDbContext>(options =>
    options.UseSqlite(connection.ConnectionString));

// Storage is stateless and resolves its root once, so a singleton. The server
// wraps a DbContext and has to follow its scope.
builder.Services.AddSingleton<PaperFileStorage>();
builder.Services.AddScoped<PaperFileServer>();

// Enums serialize as their name, not their number. Without this, PaperStatus
// would reach clients as 0/1/2 — unreadable, and it would silently change the
// shape of responses that have always carried "Pending".
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Browsing is constant here and writes are occasional, which is the access
// pattern write-ahead logging exists for. Applied before seeding so the very
// first writes already benefit.
await EnableWriteAheadLoggingAsync(connection.ConnectionString, app.Logger);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();

    // Sample data for local development only. No-op once the database has rows.
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ExamArchiveDbContext>();
    await SeedData.SeedAsync(db);
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

// Switches the database to write-ahead logging.
//
// SQLite's default rollback journal locks the whole file for the duration of a
// write, so a single upload blocks every reader until it finishes. Under WAL,
// readers carry on against the last committed state while the write appends to
// a side file, so browsing stays responsive during an upload.
//
// The mode is recorded in the database file itself rather than in the
// connection, so this only has to succeed once. It runs on every start anyway:
// that costs one statement and means a database restored from an older backup
// is repaired rather than quietly reverting to the slower default.
static async Task EnableWriteAheadLoggingAsync(string connectionString, ILogger logger)
{
    await using var sqlite = new SqliteConnection(connectionString);
    await sqlite.OpenAsync();

    await using var command = sqlite.CreateCommand();

    // PRAGMA journal_mode returns the mode actually in force, which is not
    // necessarily the one requested — WAL is unavailable on network shares and
    // read-only files, and SQLite reports that by returning the old mode rather
    // than by failing.
    command.CommandText = "PRAGMA journal_mode=WAL;";
    var mode = await command.ExecuteScalarAsync() as string;

    if (string.Equals(mode, "wal", StringComparison.OrdinalIgnoreCase))
    {
        return;
    }

    // Not fatal: the application works correctly either way, just with writes
    // blocking reads. Worth a warning because it is invisible until the archive
    // is busy enough for the difference to show up as sluggish browsing.
    logger.LogWarning(
        "Could not enable write-ahead logging; the database is running in '{JournalMode}' mode. "
        + "Reads will block while an upload is written.",
        mode ?? "unknown");
}
