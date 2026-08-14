using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamArchive.Migrations
{
    /// <inheritdoc />
    public partial class AddModerationDecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "Papers",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "Papers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Paper_RejectionReason",
                table: "Papers",
                sql: "[Status] = 'Rejected' OR [RejectionReason] IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Paper_ReviewedAt",
                table: "Papers",
                sql: "[Status] <> 'Pending' OR [ReviewedAt] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Paper_RejectionReason",
                table: "Papers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Paper_ReviewedAt",
                table: "Papers");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "Papers");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "Papers");
        }
    }
}
