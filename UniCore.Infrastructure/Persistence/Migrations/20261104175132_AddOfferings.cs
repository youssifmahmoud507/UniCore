using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOfferings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "offerings");

            migrationBuilder.CreateTable(
                name: "CourseOfferings",
                schema: "offerings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcademicSemesterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MaxSections = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseOfferings", x => x.Id);
                    table.CheckConstraint("CK_CourseOfferings_MaxSections", "[MaxSections] IS NULL OR ([MaxSections] >= 1 AND [MaxSections] <= 50)");
                    table.ForeignKey(
                        name: "FK_CourseOfferings_AcademicSemesters_AcademicSemesterId",
                        column: x => x.AcademicSemesterId,
                        principalSchema: "calendar",
                        principalTable: "AcademicSemesters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CourseOfferings_CourseVersions_CourseVersionId",
                        column: x => x.CourseVersionId,
                        principalSchema: "courses",
                        principalTable: "CourseVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CourseSections",
                schema: "offerings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseOfferingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SectionCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseSections", x => x.Id);
                    table.CheckConstraint("CK_CourseSections_Capacity", "[Capacity] >= 1 AND [Capacity] <= 1000");
                    table.ForeignKey(
                        name: "FK_CourseSections_CourseOfferings_CourseOfferingId",
                        column: x => x.CourseOfferingId,
                        principalSchema: "offerings",
                        principalTable: "CourseOfferings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InstructorAssignments",
                schema: "offerings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseSectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstructorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AssignedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstructorAssignments", x => x.Id);
                    table.CheckConstraint("CK_InstructorAssignments_EndedAt", "[EndedAt] IS NULL OR [EndedAt] >= [AssignedAt]");
                    table.ForeignKey(
                        name: "FK_InstructorAssignments_CourseSections_CourseSectionId",
                        column: x => x.CourseSectionId,
                        principalSchema: "offerings",
                        principalTable: "CourseSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InstructorAssignments_Instructors_InstructorId",
                        column: x => x.InstructorId,
                        principalSchema: "people",
                        principalTable: "Instructors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CourseOfferings_AcademicSemesterId",
                schema: "offerings",
                table: "CourseOfferings",
                column: "AcademicSemesterId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseOfferings_CourseVersionId_AcademicSemesterId",
                schema: "offerings",
                table: "CourseOfferings",
                columns: new[] { "CourseVersionId", "AcademicSemesterId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourseSections_CourseOfferingId_SectionCode",
                schema: "offerings",
                table: "CourseSections",
                columns: new[] { "CourseOfferingId", "SectionCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InstructorAssignments_InstructorId",
                schema: "offerings",
                table: "InstructorAssignments",
                column: "InstructorId");

            migrationBuilder.CreateIndex(
                name: "UX_InstructorAssignments_ActivePerInstructor",
                schema: "offerings",
                table: "InstructorAssignments",
                columns: new[] { "CourseSectionId", "InstructorId" },
                unique: true,
                filter: "[EndedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_InstructorAssignments_OneActivePrimary",
                schema: "offerings",
                table: "InstructorAssignments",
                column: "CourseSectionId",
                unique: true,
                filter: "[Role] = 'Primary' AND [EndedAt] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InstructorAssignments",
                schema: "offerings");

            migrationBuilder.DropTable(
                name: "CourseSections",
                schema: "offerings");

            migrationBuilder.DropTable(
                name: "CourseOfferings",
                schema: "offerings");
        }
    }
}
