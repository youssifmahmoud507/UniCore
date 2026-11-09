using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExamination : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "grading");

            migrationBuilder.CreateTable(
                name: "GradeScales",
                schema: "grading",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GradeScales", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Grades",
                schema: "grading",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnrollmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseSectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssessmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcademicSemesterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Credits = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    GradeScaleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Percentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    Letter = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    GradePoints = table.Column<decimal>(type: "decimal(3,2)", precision: 3, scale: 2, nullable: true),
                    IsPassing = table.Column<bool>(type: "bit", nullable: true),
                    EnteredByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReturnReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Grades", x => x.Id);
                    table.CheckConstraint("CK_Grades_Percentage", "[Percentage] IS NULL OR ([Percentage] >= 0 AND [Percentage] <= 100)");
                    table.ForeignKey(
                        name: "FK_Grades_Assessments_AssessmentId",
                        column: x => x.AssessmentId,
                        principalSchema: "assessment",
                        principalTable: "Assessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Grades_CourseSections_CourseSectionId",
                        column: x => x.CourseSectionId,
                        principalSchema: "offerings",
                        principalTable: "CourseSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Grades_GradeScales_GradeScaleId",
                        column: x => x.GradeScaleId,
                        principalSchema: "grading",
                        principalTable: "GradeScales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GradeScaleBands",
                schema: "grading",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GradeScaleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Letter = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    MinPercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    GradePoints = table.Column<decimal>(type: "decimal(3,2)", precision: 3, scale: 2, nullable: false),
                    IsPassing = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GradeScaleBands", x => x.Id);
                    table.CheckConstraint("CK_GradeScaleBands_Min", "[MinPercentage] >= 0 AND [MinPercentage] <= 100");
                    table.CheckConstraint("CK_GradeScaleBands_Points", "[GradePoints] >= 0 AND [GradePoints] <= 5");
                    table.ForeignKey(
                        name: "FK_GradeScaleBands_GradeScales_GradeScaleId",
                        column: x => x.GradeScaleId,
                        principalSchema: "grading",
                        principalTable: "GradeScales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GradeComponentScores",
                schema: "grading",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GradeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssessmentComponentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Score = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GradeComponentScores", x => x.Id);
                    table.CheckConstraint("CK_GradeComponentScores_Score", "[Score] >= 0 AND [Score] <= 1000");
                    table.ForeignKey(
                        name: "FK_GradeComponentScores_Grades_GradeId",
                        column: x => x.GradeId,
                        principalSchema: "grading",
                        principalTable: "Grades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GradeComponentScores_GradeId_AssessmentComponentId",
                schema: "grading",
                table: "GradeComponentScores",
                columns: new[] { "GradeId", "AssessmentComponentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Grades_AssessmentId",
                schema: "grading",
                table: "Grades",
                column: "AssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Grades_CourseSectionId_Status",
                schema: "grading",
                table: "Grades",
                columns: new[] { "CourseSectionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Grades_EnrollmentId",
                schema: "grading",
                table: "Grades",
                column: "EnrollmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Grades_GradeScaleId",
                schema: "grading",
                table: "Grades",
                column: "GradeScaleId");

            migrationBuilder.CreateIndex(
                name: "IX_Grades_StudentId_AcademicSemesterId",
                schema: "grading",
                table: "Grades",
                columns: new[] { "StudentId", "AcademicSemesterId" });

            migrationBuilder.CreateIndex(
                name: "IX_GradeScaleBands_GradeScaleId_Letter",
                schema: "grading",
                table: "GradeScaleBands",
                columns: new[] { "GradeScaleId", "Letter" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GradeScaleBands_GradeScaleId_MinPercentage",
                schema: "grading",
                table: "GradeScaleBands",
                columns: new[] { "GradeScaleId", "MinPercentage" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GradeScales_Name",
                schema: "grading",
                table: "GradeScales",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_GradeScales_OneDefault",
                schema: "grading",
                table: "GradeScales",
                column: "IsDefault",
                unique: true,
                filter: "[IsDefault] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GradeComponentScores",
                schema: "grading");

            migrationBuilder.DropTable(
                name: "GradeScaleBands",
                schema: "grading");

            migrationBuilder.DropTable(
                name: "Grades",
                schema: "grading");

            migrationBuilder.DropTable(
                name: "GradeScales",
                schema: "grading");
        }
    }
}
