using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCourse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "courses");

            migrationBuilder.CreateTable(
                name: "Courses",
                schema: "courses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CourseType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Courses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Courses_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalSchema: "university",
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CourseVersions",
                schema: "courses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Credits = table.Column<int>(type: "int", nullable: false),
                    LectureHours = table.Column<int>(type: "int", nullable: false),
                    PracticalHours = table.Column<int>(type: "int", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseVersions", x => x.Id);
                    table.CheckConstraint("CK_CourseVersions_Credits", "[Credits] >= 1 AND [Credits] <= 30");
                    table.CheckConstraint("CK_CourseVersions_EffectiveTo", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                    table.CheckConstraint("CK_CourseVersions_Hours", "[LectureHours] >= 0 AND [LectureHours] <= 60 AND [PracticalHours] >= 0 AND [PracticalHours] <= 60 AND [LectureHours] + [PracticalHours] > 0");
                    table.CheckConstraint("CK_CourseVersions_VersionNumber", "[VersionNumber] >= 1");
                    table.ForeignKey(
                        name: "FK_CourseVersions_Courses_CourseId",
                        column: x => x.CourseId,
                        principalSchema: "courses",
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CourseRequirements_CourseVersionId",
                schema: "curriculum",
                table: "CourseRequirements",
                column: "CourseVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Courses_DepartmentId_Code",
                schema: "courses",
                table: "Courses",
                columns: new[] { "DepartmentId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourseVersions_CourseId_VersionNumber",
                schema: "courses",
                table: "CourseVersions",
                columns: new[] { "CourseId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_CourseVersions_OneDraftPerCourse",
                schema: "courses",
                table: "CourseVersions",
                column: "CourseId",
                unique: true,
                filter: "[Status] = 'Draft'");

            migrationBuilder.AddForeignKey(
                name: "FK_CourseRequirements_CourseVersions_CourseVersionId",
                schema: "curriculum",
                table: "CourseRequirements",
                column: "CourseVersionId",
                principalSchema: "courses",
                principalTable: "CourseVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StudyPlanCourses_CourseVersions_CourseVersionId",
                schema: "curriculum",
                table: "StudyPlanCourses",
                column: "CourseVersionId",
                principalSchema: "courses",
                principalTable: "CourseVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CourseRequirements_CourseVersions_CourseVersionId",
                schema: "curriculum",
                table: "CourseRequirements");

            migrationBuilder.DropForeignKey(
                name: "FK_StudyPlanCourses_CourseVersions_CourseVersionId",
                schema: "curriculum",
                table: "StudyPlanCourses");

            migrationBuilder.DropTable(
                name: "CourseVersions",
                schema: "courses");

            migrationBuilder.DropTable(
                name: "Courses",
                schema: "courses");

            migrationBuilder.DropIndex(
                name: "IX_CourseRequirements_CourseVersionId",
                schema: "curriculum",
                table: "CourseRequirements");
        }
    }
}
