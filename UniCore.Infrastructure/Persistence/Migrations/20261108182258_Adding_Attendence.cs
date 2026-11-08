using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Adding_Attendence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "attendance");

            migrationBuilder.CreateTable(
                name: "AttendancePolicies",
                schema: "attendance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcademicProgramId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MinimumPercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    WarningPercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    LateThresholdMinutes = table.Column<int>(type: "int", nullable: false),
                    IsExamBlocking = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendancePolicies", x => x.Id);
                    table.CheckConstraint("CK_AttendancePolicies_OneTarget", "([AcademicProgramId] IS NULL AND [CourseId] IS NOT NULL) OR ([AcademicProgramId] IS NOT NULL AND [CourseId] IS NULL)");
                    table.CheckConstraint("CK_AttendancePolicies_Percentages", "[MinimumPercentage] >= 0 AND [MinimumPercentage] <= [WarningPercentage] AND [WarningPercentage] <= 100");
                    table.ForeignKey(
                        name: "FK_AttendancePolicies_AcademicPrograms_AcademicProgramId",
                        column: x => x.AcademicProgramId,
                        principalSchema: "university",
                        principalTable: "AcademicPrograms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendancePolicies_Courses_CourseId",
                        column: x => x.CourseId,
                        principalSchema: "courses",
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceSessions",
                schema: "attendance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseSectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OpenedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OpenedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceSessions_CourseSections_CourseSectionId",
                        column: x => x.CourseSectionId,
                        principalSchema: "offerings",
                        principalTable: "CourseSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceWarnings",
                schema: "attendance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseSectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Percentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    Level = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsExamBlocking = table.Column<bool>(type: "bit", nullable: false),
                    IssuedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceWarnings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceWarnings_CourseSections_CourseSectionId",
                        column: x => x.CourseSectionId,
                        principalSchema: "offerings",
                        principalTable: "CourseSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceWarnings_Students_StudentId",
                        column: x => x.StudentId,
                        principalSchema: "students",
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceRecords",
                schema: "attendance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttendanceSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnrollmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MinutesLate = table.Column<int>(type: "int", nullable: true),
                    MarkedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MarkedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceRecords", x => x.Id);
                    table.CheckConstraint("CK_AttendanceRecords_MinutesLate", "[MinutesLate] IS NULL OR [MinutesLate] > 0");
                    table.ForeignKey(
                        name: "FK_AttendanceRecords_AttendanceSessions_AttendanceSessionId",
                        column: x => x.AttendanceSessionId,
                        principalSchema: "attendance",
                        principalTable: "AttendanceSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AttendanceRecords_Students_StudentId",
                        column: x => x.StudentId,
                        principalSchema: "students",
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceExcuses",
                schema: "attendance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttendanceSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttendanceRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseSectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReviewNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceExcuses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceExcuses_AttendanceRecords_AttendanceRecordId",
                        column: x => x.AttendanceRecordId,
                        principalSchema: "attendance",
                        principalTable: "AttendanceRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceExcuses_AttendanceSessions_AttendanceSessionId",
                        column: x => x.AttendanceSessionId,
                        principalSchema: "attendance",
                        principalTable: "AttendanceSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceExcuses_Students_StudentId",
                        column: x => x.StudentId,
                        principalSchema: "students",
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceExcuses_AttendanceSessionId",
                schema: "attendance",
                table: "AttendanceExcuses",
                column: "AttendanceSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceExcuses_CourseSectionId_Status",
                schema: "attendance",
                table: "AttendanceExcuses",
                columns: new[] { "CourseSectionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceExcuses_StudentId",
                schema: "attendance",
                table: "AttendanceExcuses",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "UX_AttendanceExcuses_ActivePerRecord",
                schema: "attendance",
                table: "AttendanceExcuses",
                column: "AttendanceRecordId",
                unique: true,
                filter: "[Status] <> 'Rejected'");

            migrationBuilder.CreateIndex(
                name: "UX_AttendancePolicies_ActivePerCourse",
                schema: "attendance",
                table: "AttendancePolicies",
                column: "CourseId",
                unique: true,
                filter: "[IsActive] = 1 AND [CourseId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_AttendancePolicies_ActivePerProgram",
                schema: "attendance",
                table: "AttendancePolicies",
                column: "AcademicProgramId",
                unique: true,
                filter: "[IsActive] = 1 AND [AcademicProgramId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_AttendanceSessionId_StudentId",
                schema: "attendance",
                table: "AttendanceRecords",
                columns: new[] { "AttendanceSessionId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_StudentId",
                schema: "attendance",
                table: "AttendanceRecords",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSessions_CourseSectionId_SessionDate_StartTime",
                schema: "attendance",
                table: "AttendanceSessions",
                columns: new[] { "CourseSectionId", "SessionDate", "StartTime" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceWarnings_CourseSectionId",
                schema: "attendance",
                table: "AttendanceWarnings",
                column: "CourseSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceWarnings_StudentId_CourseSectionId_IssuedAt",
                schema: "attendance",
                table: "AttendanceWarnings",
                columns: new[] { "StudentId", "CourseSectionId", "IssuedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceExcuses",
                schema: "attendance");

            migrationBuilder.DropTable(
                name: "AttendancePolicies",
                schema: "attendance");

            migrationBuilder.DropTable(
                name: "AttendanceWarnings",
                schema: "attendance");

            migrationBuilder.DropTable(
                name: "AttendanceRecords",
                schema: "attendance");

            migrationBuilder.DropTable(
                name: "AttendanceSessions",
                schema: "attendance");
        }
    }
}
