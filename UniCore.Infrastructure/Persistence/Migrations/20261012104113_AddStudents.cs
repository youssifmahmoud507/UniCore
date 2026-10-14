using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStudents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "students");

            migrationBuilder.CreateTable(
                name: "StudentNumberCounters",
                schema: "students",
                columns: table => new
                {
                    CounterKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LastValue = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentNumberCounters", x => x.CounterKey);
                });

            migrationBuilder.CreateTable(
                name: "Students",
                schema: "students",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StudentNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AdmissionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AcademicStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ExpectedGraduationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    GraduationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Students", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Students_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentAcademicProfiles",
                schema: "students",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdmissionAcademicYearId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentAcademicLevelId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CurrentStudyPlanVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AcademicStanding = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentAcademicProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentAcademicProfiles_Students_StudentId",
                        column: x => x.StudentId,
                        principalSchema: "students",
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentProgramHistories",
                schema: "students",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcademicProgramId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudyPlanVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcademicLevelId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentProgramHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentProgramHistories_Students_StudentId",
                        column: x => x.StudentId,
                        principalSchema: "students",
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentStatusHistories",
                schema: "students",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OldStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NewStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ChangedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentStatusHistories_Students_StudentId",
                        column: x => x.StudentId,
                        principalSchema: "students",
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudentAcademicProfiles_AdmissionAcademicYearId",
                schema: "students",
                table: "StudentAcademicProfiles",
                column: "AdmissionAcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentAcademicProfiles_CurrentStudyPlanVersionId",
                schema: "students",
                table: "StudentAcademicProfiles",
                column: "CurrentStudyPlanVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentAcademicProfiles_StudentId",
                schema: "students",
                table: "StudentAcademicProfiles",
                column: "StudentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentProgramHistories_AcademicProgramId",
                schema: "students",
                table: "StudentProgramHistories",
                column: "AcademicProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentProgramHistories_StudentId_StartDate",
                schema: "students",
                table: "StudentProgramHistories",
                columns: new[] { "StudentId", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Students_AcademicStatus",
                schema: "students",
                table: "Students",
                column: "AcademicStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Students_PersonProfileId",
                schema: "students",
                table: "Students",
                column: "PersonProfileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_StudentNumber",
                schema: "students",
                table: "Students",
                column: "StudentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_UserId",
                schema: "students",
                table: "Students",
                column: "UserId",
                unique: true,
                filter: "[UserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StudentStatusHistories_StudentId_EffectiveDate",
                schema: "students",
                table: "StudentStatusHistories",
                columns: new[] { "StudentId", "EffectiveDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudentAcademicProfiles",
                schema: "students");

            migrationBuilder.DropTable(
                name: "StudentNumberCounters",
                schema: "students");

            migrationBuilder.DropTable(
                name: "StudentProgramHistories",
                schema: "students");

            migrationBuilder.DropTable(
                name: "StudentStatusHistories",
                schema: "students");

            migrationBuilder.DropTable(
                name: "Students",
                schema: "students");
        }
    }
}
