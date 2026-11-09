using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceAndExamination : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "examination");

            migrationBuilder.CreateTable(
                name: "ExamSessions",
                schema: "examination",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcademicSemesterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExamType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamSessions", x => x.Id);
                    table.CheckConstraint("CK_ExamSessions_Dates", "[EndDate] >= [StartDate]");
                    table.ForeignKey(
                        name: "FK_ExamSessions_AcademicSemesters_AcademicSemesterId",
                        column: x => x.AcademicSemesterId,
                        principalSchema: "calendar",
                        principalTable: "AcademicSemesters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Exams",
                schema: "examination",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseSectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MaxScore = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: false),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Exams", x => x.Id);
                    table.CheckConstraint("CK_Exams_Duration", "[DurationMinutes] >= 15 AND [DurationMinutes] <= 480");
                    table.CheckConstraint("CK_Exams_MaxScore", "[MaxScore] > 0 AND [MaxScore] <= 1000");
                    table.ForeignKey(
                        name: "FK_Exams_CourseSections_CourseSectionId",
                        column: x => x.CourseSectionId,
                        principalSchema: "offerings",
                        principalTable: "CourseSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Exams_ExamSessions_ExamSessionId",
                        column: x => x.ExamSessionId,
                        principalSchema: "examination",
                        principalTable: "ExamSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExamRooms",
                schema: "examination",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamRooms", x => x.Id);
                    table.CheckConstraint("CK_ExamRooms_Capacity", "[Capacity] > 0");
                    table.ForeignKey(
                        name: "FK_ExamRooms_Exams_ExamId",
                        column: x => x.ExamId,
                        principalSchema: "examination",
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExamRooms_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalSchema: "university",
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExamSchedules",
                schema: "examination",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamDate = table.Column<DateOnly>(type: "date", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamSchedules", x => x.Id);
                    table.CheckConstraint("CK_ExamSchedules_Times", "[EndTime] > [StartTime]");
                    table.ForeignKey(
                        name: "FK_ExamSchedules_ExamSessions_ExamSessionId",
                        column: x => x.ExamSessionId,
                        principalSchema: "examination",
                        principalTable: "ExamSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExamSchedules_Exams_ExamId",
                        column: x => x.ExamId,
                        principalSchema: "examination",
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExamInvigilators",
                schema: "examination",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamRoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstructorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamInvigilators", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExamInvigilators_ExamRooms_ExamRoomId",
                        column: x => x.ExamRoomId,
                        principalSchema: "examination",
                        principalTable: "ExamRooms",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ExamInvigilators_Exams_ExamId",
                        column: x => x.ExamId,
                        principalSchema: "examination",
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExamInvigilators_Instructors_InstructorId",
                        column: x => x.InstructorId,
                        principalSchema: "people",
                        principalTable: "Instructors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExamInvigilators_ExamId_InstructorId",
                schema: "examination",
                table: "ExamInvigilators",
                columns: new[] { "ExamId", "InstructorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExamInvigilators_InstructorId",
                schema: "examination",
                table: "ExamInvigilators",
                column: "InstructorId");

            migrationBuilder.CreateIndex(
                name: "UX_ExamInvigilators_ChiefPerRoom",
                schema: "examination",
                table: "ExamInvigilators",
                columns: new[] { "ExamRoomId", "Role" },
                unique: true,
                filter: "[Role] = 'Chief'");

            migrationBuilder.CreateIndex(
                name: "IX_ExamRooms_ExamId_RoomId",
                schema: "examination",
                table: "ExamRooms",
                columns: new[] { "ExamId", "RoomId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExamRooms_RoomId",
                schema: "examination",
                table: "ExamRooms",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Exams_CourseSectionId",
                schema: "examination",
                table: "Exams",
                column: "CourseSectionId");

            migrationBuilder.CreateIndex(
                name: "UX_Exams_ActivePerSectionAndType",
                schema: "examination",
                table: "Exams",
                columns: new[] { "ExamSessionId", "CourseSectionId", "ExamType" },
                unique: true,
                filter: "[Status] <> 'Cancelled'");

            migrationBuilder.CreateIndex(
                name: "IX_ExamSchedules_ExamDate",
                schema: "examination",
                table: "ExamSchedules",
                column: "ExamDate");

            migrationBuilder.CreateIndex(
                name: "IX_ExamSchedules_ExamId",
                schema: "examination",
                table: "ExamSchedules",
                column: "ExamId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExamSchedules_ExamSessionId",
                schema: "examination",
                table: "ExamSchedules",
                column: "ExamSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ExamSessions_AcademicSemesterId_Name",
                schema: "examination",
                table: "ExamSessions",
                columns: new[] { "AcademicSemesterId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExamInvigilators",
                schema: "examination");

            migrationBuilder.DropTable(
                name: "ExamSchedules",
                schema: "examination");

            migrationBuilder.DropTable(
                name: "ExamRooms",
                schema: "examination");

            migrationBuilder.DropTable(
                name: "Exams",
                schema: "examination");

            migrationBuilder.DropTable(
                name: "ExamSessions",
                schema: "examination");
        }
    }
}
