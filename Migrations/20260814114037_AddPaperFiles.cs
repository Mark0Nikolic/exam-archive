using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamArchive.Migrations
{
    /// <summary>
    /// Moves a paper's file out of the Papers.FilePath column and into a
    /// PaperFiles table, so one paper can hold several pages.
    /// </summary>
    /// <remarks>
    /// Hand-edited after scaffolding. EF generated the DropColumn first and had no
    /// reason to connect it to the new table, which would have destroyed every
    /// existing file reference. The order below — create, copy, then drop — is what
    /// makes this migration non-destructive, so keep the copy between the two.
    /// </remarks>
    public partial class AddPaperFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaperFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PaperId = table.Column<int>(type: "INTEGER", nullable: false),
                    StoredPath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PageNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaperFiles", x => x.Id);
                    table.CheckConstraint("CK_PaperFile_ContentType", "[ContentType] IN ('application/pdf', 'image/jpeg', 'image/png', 'image/webp')");
                    table.CheckConstraint("CK_PaperFile_PageNumber", "[PageNumber] >= 1");
                    table.CheckConstraint("CK_PaperFile_SizeBytes", "[SizeBytes] >= 0");
                    table.ForeignKey(
                        name: "FK_PaperFiles_Papers_PaperId",
                        column: x => x.PaperId,
                        principalTable: "Papers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaperFiles_PaperId_PageNumber",
                table: "PaperFiles",
                columns: new[] { "PaperId", "PageNumber" },
                unique: true);

            // Every existing paper becomes a one-page paper. Not scaffolded — EF
            // sees a dropped column and a new table as unrelated events, so without
            // this each row would keep its metadata and lose its file for good.
            //
            // ContentType is fixed rather than derived from the path because the
            // upload endpoint only ever accepted .pdf before this migration; images
            // arrive with the schema that follows it.
            //
            // SizeBytes is 0, meaning unrecorded. The real size is a property of a
            // file on disk, which SQL cannot read, and inventing a number would be
            // worse than admitting the value is unknown.
            migrationBuilder.Sql(
                """
                INSERT INTO "PaperFiles" ("PaperId", "StoredPath", "ContentType", "PageNumber", "SizeBytes")
                SELECT "Id", "FilePath", 'application/pdf', 1, 0
                FROM "Papers"
                WHERE "FilePath" IS NOT NULL AND "FilePath" <> '';
                """);

            // Last, and only now that every path has been copied.
            migrationBuilder.DropColumn(
                name: "FilePath",
                table: "Papers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FilePath",
                table: "Papers",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            // Mirror of the copy above, so rolling back is survivable too. Lossy by
            // nature: a paper with several pages can only keep its first one, since
            // the old schema has nowhere to put the rest.
            migrationBuilder.Sql(
                """
                UPDATE "Papers"
                SET "FilePath" = COALESCE(
                    (SELECT "StoredPath" FROM "PaperFiles"
                     WHERE "PaperFiles"."PaperId" = "Papers"."Id" AND "PaperFiles"."PageNumber" = 1),
                    '');
                """);

            migrationBuilder.DropTable(
                name: "PaperFiles");
        }
    }
}
