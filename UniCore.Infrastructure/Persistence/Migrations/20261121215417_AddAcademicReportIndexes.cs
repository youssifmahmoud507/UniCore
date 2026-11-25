using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAcademicReportIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Grades_Status_PublishedAt",
                schema: "grading",
                table: "Grades",
                columns: new[] { "Status", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_RegisteredAt",
                schema: "registration",
                table: "Enrollments",
                column: "RegisteredAt");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSessions_Status_SessionDate",
                schema: "attendance",
                table: "AttendanceSessions",
                columns: new[] { "Status", "SessionDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Grades_Status_PublishedAt",
                schema: "grading",
                table: "Grades");

            migrationBuilder.DropIndex(
                name: "IX_Enrollments_RegisteredAt",
                schema: "registration",
                table: "Enrollments");

            migrationBuilder.DropIndex(
                name: "IX_AttendanceSessions_Status_SessionDate",
                schema: "attendance",
                table: "AttendanceSessions");
        }
    }
}
