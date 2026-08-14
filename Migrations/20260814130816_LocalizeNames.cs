using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamArchive.Migrations
{
    /// <inheritdoc />
    public partial class LocalizeNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Name",
                table: "Subjects",
                newName: "NameSr");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "Studies",
                newName: "NameSr");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "Majors",
                newName: "NameSr");

            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                table: "Subjects",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                table: "Studies",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                table: "Majors",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            // Not scaffolded. The rename above preserved every existing name, but
            // those names are English and the column they now sit in is the Serbian
            // one — so without this the English text would be the only copy, wearing
            // the wrong label. Duplicating it into NameEn keeps it addressable while
            // NameSr is corrected: the seeder replaces both for subjects and majors
            // it recognises, and anything it does not recognise stays legible in
            // English rather than turning into an empty Serbian name.
            migrationBuilder.Sql("""UPDATE "Subjects" SET "NameEn" = "NameSr";""");
            migrationBuilder.Sql("""UPDATE "Majors" SET "NameEn" = "NameSr";""");

            migrationBuilder.UpdateData(
                table: "Studies",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "NameEn", "NameSr" },
                values: new object[] { "Bachelor's", "Основне академске студије" });

            migrationBuilder.UpdateData(
                table: "Studies",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "NameEn", "NameSr" },
                values: new object[] { "Master's", "Мастер академске студије" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NameEn",
                table: "Subjects");

            migrationBuilder.DropColumn(
                name: "NameEn",
                table: "Studies");

            migrationBuilder.DropColumn(
                name: "NameEn",
                table: "Majors");

            migrationBuilder.RenameColumn(
                name: "NameSr",
                table: "Subjects",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "NameSr",
                table: "Studies",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "NameSr",
                table: "Majors",
                newName: "Name");

            migrationBuilder.UpdateData(
                table: "Studies",
                keyColumn: "Id",
                keyValue: 1,
                column: "Name",
                value: "Bachelor's");

            migrationBuilder.UpdateData(
                table: "Studies",
                keyColumn: "Id",
                keyValue: 2,
                column: "Name",
                value: "Master's");
        }
    }
}
