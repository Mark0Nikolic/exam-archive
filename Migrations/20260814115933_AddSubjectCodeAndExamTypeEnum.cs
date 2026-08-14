using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamArchive.Migrations
{
    /// <inheritdoc />
    public partial class AddSubjectCodeAndExamTypeEnum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Subjects",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_Code",
                table: "Subjects",
                column: "Code",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_MajorSubject_YearOfStudy",
                table: "MajorSubjects",
                sql: "[YearOfStudy] >= 1 AND [YearOfStudy] <= 6");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Subjects_Code",
                table: "Subjects");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MajorSubject_YearOfStudy",
                table: "MajorSubjects");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Subjects");
        }
    }
}
