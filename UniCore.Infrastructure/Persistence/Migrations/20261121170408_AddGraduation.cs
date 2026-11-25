using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGraduation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "graduation");

            migrationBuilder.CreateTable(
                name: "GraduationApplications",
                schema: "graduation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcademicProgramId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudyPlanVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CumulativeGpa = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: true),
                    EarnedCredits = table.Column<int>(type: "int", nullable: true),
                    Standing = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DecidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DecisionNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GraduationApplications", x => x.Id);
                    table.CheckConstraint("CK_GraduationApplications_Figures", "([CumulativeGpa] IS NULL OR ([CumulativeGpa] >= 0 AND [CumulativeGpa] <= 5)) AND ([EarnedCredits] IS NULL OR [EarnedCredits] >= 0)");
                });

            migrationBuilder.CreateIndex(
                name: "IX_GraduationApplications_Status_SubmittedAt",
                schema: "graduation",
                table: "GraduationApplications",
                columns: new[] { "Status", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_GraduationApplications_OneActivePerStudent",
                schema: "graduation",
                table: "GraduationApplications",
                column: "StudentId",
                unique: true,
                filter: "[Status] IN ('Submitted', 'Approved')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GraduationApplications",
                schema: "graduation");
        }
    }
}
