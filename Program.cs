using System.Text.Json.Serialization;
using ExamArchive.Data;
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
