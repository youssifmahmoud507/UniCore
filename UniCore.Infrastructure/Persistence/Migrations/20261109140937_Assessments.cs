using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Assessments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "assessment");

            migrationBuilder.CreateTable(
                name: "Assessments",
                schema: "assessment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseSectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assessments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Assessments_CourseSections_CourseSectionId",
                        column: x => x.CourseSectionId,
                        principalSchema: "offerings",
                        principalTable: "CourseSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssessmentComponents",
                schema: "assessment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssessmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Weight = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    MaxScore = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssessmentComponents", x => x.Id);
                    table.CheckConstraint("CK_AssessmentComponents_MaxScore", "[MaxScore] > 0 AND [MaxScore] <= 1000");
                    table.CheckConstraint("CK_AssessmentComponents_Weight", "[Weight] > 0 AND [Weight] <= 100");
                    table.ForeignKey(
                        name: "FK_AssessmentComponents_Assessments_AssessmentId",
                        column: x => x.AssessmentId,
                        principalSchema: "assessment",
                        principalTable: "Assessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentComponents_AssessmentId_Name",
                schema: "assessment",
                table: "AssessmentComponents",
                columns: new[] { "AssessmentId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_AssessmentComponents_OneExamPerType",
                schema: "assessment",
                table: "AssessmentComponents",
                columns: new[] { "AssessmentId", "Type" },
                unique: true,
                filter: "[Type] IN ('Midterm', 'Final')");

            migrationBuilder.CreateIndex(
                name: "UX_Assessments_LivePerSection",
                schema: "assessment",
                table: "Assessments",
                column: "CourseSectionId",
                unique: true,
                filter: "[Status] <> 'Cancelled'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssessmentComponents",
                schema: "assessment");

            migrationBuilder.DropTable(
                name: "Assessments",
                schema: "assessment");
        }
    }
}
