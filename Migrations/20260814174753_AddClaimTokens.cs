using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamArchive.Migrations
{
    /// <inheritdoc />
    public partial class AddClaimTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_User_Role",
                table: "Users");

            migrationBuilder.AddColumn<string>(
                name: "ClaimTokenHash",
                table: "Papers",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            // Not scaffolded, and the migration fails without it. The Student role
            // is gone, but any row still holding it would be copied into the
            // rebuilt table below and rejected by the new constraint — SQLite
            // implements a changed constraint by recreating the table and copying
            // every row through it.
            //
            // The references are cleared explicitly rather than left to the
            // foreign key's ON DELETE SET NULL, because migrations run with
            // PRAGMA foreign_keys = 0 and the cascade would simply not fire,
            // leaving Papers pointing at users that no longer exist.
            //
            // Papers keep their files and their place in the archive; they just
            // become anonymous, which is what every public submission is now.
            migrationBuilder.Sql(
                """
                UPDATE "Papers" SET "SubmittedByUserId" = NULL
                WHERE "SubmittedByUserId" IN (SELECT "Id" FROM "Users" WHERE "Role" = 'Student');
                """);

            migrationBuilder.Sql("""DELETE FROM "Users" WHERE "Role" = 'Student';""");

            migrationBuilder.AddCheckConstraint(
                name: "CK_User_Role",
                table: "Users",
                sql: "[Role] IN ('Moderator', 'Admin')");

            migrationBuilder.CreateIndex(
                name: "IX_Papers_ClaimTokenHash",
                table: "Papers",
                column: "ClaimTokenHash",
                unique: true);
        }

        /// <remarks>
        /// Restores the schema but not the data: the Student accounts deleted above
        /// cannot be recreated, and the papers unlinked from them stay anonymous.
        /// That is acceptable only because those accounts existed on development
        /// databases alone — nothing in a deployment ever held that role.
        /// </remarks>
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_User_Role",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Papers_ClaimTokenHash",
                table: "Papers");

            migrationBuilder.DropColumn(
                name: "ClaimTokenHash",
                table: "Papers");

            migrationBuilder.AddCheckConstraint(
                name: "CK_User_Role",
                table: "Users",
                sql: "[Role] IN ('Student', 'Moderator', 'Admin')");
        }
    }
}
