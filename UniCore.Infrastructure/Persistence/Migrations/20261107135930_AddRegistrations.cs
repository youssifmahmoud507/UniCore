using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRegistrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "scheduling");

            migrationBuilder.EnsureSchema(
                name: "registration");

            migrationBuilder.AddColumn<int>(
                name: "EnrolledCount",
                schema: "offerings",
                table: "CourseSections",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "InstructorAvailabilities",
                schema: "scheduling",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstructorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DayOfWeek = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstructorAvailabilities", x => x.Id);
                    table.CheckConstraint("CK_InstructorAvailabilities_Time", "[StartTime] < [EndTime]");
                    table.ForeignKey(
                        name: "FK_InstructorAvailabilities_Instructors_InstructorId",
                        column: x => x.InstructorId,
                        principalSchema: "people",
                        principalTable: "Instructors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Registrations",
                schema: "registration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcademicSemesterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Registrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Registrations_AcademicSemesters_AcademicSemesterId",
                        column: x => x.AcademicSemesterId,
                        principalSchema: "calendar",
                        principalTable: "AcademicSemesters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Registrations_Students_StudentId",
                        column: x => x.StudentId,
                        principalSchema: "students",
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RoomAvailabilities",
                schema: "scheduling",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DayOfWeek = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomAvailabilities", x => x.Id);
                    table.CheckConstraint("CK_RoomAvailabilities_Time", "[StartTime] < [EndTime]");
                    table.ForeignKey(
                        name: "FK_RoomAvailabilities_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalSchema: "university",
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Schedules",
                schema: "scheduling",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcademicSemesterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Schedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Schedules_AcademicSemesters_AcademicSemesterId",
                        column: x => x.AcademicSemesterId,
                        principalSchema: "calendar",
                        principalTable: "AcademicSemesters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Enrollments",
                schema: "registration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RegistrationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseSectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Credits = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RegisteredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DroppedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    WithdrawnAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Enrollments", x => x.Id);
                    table.CheckConstraint("CK_Enrollments_Credits", "[Credits] >= 1 AND [Credits] <= 30");
                    table.ForeignKey(
                        name: "FK_Enrollments_CourseSections_CourseSectionId",
                        column: x => x.CourseSectionId,
                        principalSchema: "offerings",
                        principalTable: "CourseSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Enrollments_CourseVersions_CourseVersionId",
                        column: x => x.CourseVersionId,
                        principalSchema: "courses",
                        principalTable: "CourseVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Enrollments_Registrations_RegistrationId",
                        column: x => x.RegistrationId,
                        principalSchema: "registration",
                        principalTable: "Registrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleSlots",
                schema: "scheduling",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScheduleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DayOfWeek = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleSlots", x => x.Id);
                    table.CheckConstraint("CK_ScheduleSlots_Time", "[StartTime] < [EndTime]");
                    table.ForeignKey(
                        name: "FK_ScheduleSlots_Schedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalSchema: "scheduling",
                        principalTable: "Schedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CourseSectionSchedules",
                schema: "scheduling",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseSectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScheduleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScheduleSlotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcademicSemesterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DayOfWeek = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseSectionSchedules", x => x.Id);
                    table.CheckConstraint("CK_CourseSectionSchedules_Time", "[StartTime] < [EndTime]");
                    table.ForeignKey(
                        name: "FK_CourseSectionSchedules_AcademicSemesters_AcademicSemesterId",
                        column: x => x.AcademicSemesterId,
                        principalSchema: "calendar",
                        principalTable: "AcademicSemesters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CourseSectionSchedules_CourseSections_CourseSectionId",
                        column: x => x.CourseSectionId,
                        principalSchema: "offerings",
                        principalTable: "CourseSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CourseSectionSchedules_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalSchema: "university",
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CourseSectionSchedules_ScheduleSlots_ScheduleSlotId",
                        column: x => x.ScheduleSlotId,
                        principalSchema: "scheduling",
                        principalTable: "ScheduleSlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CourseSectionSchedules_Schedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalSchema: "scheduling",
                        principalTable: "Schedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_CourseSections_EnrolledCount",
                schema: "offerings",
                table: "CourseSections",
                sql: "[EnrolledCount] >= 0 AND [EnrolledCount] <= [Capacity]");

            migrationBuilder.CreateIndex(
                name: "IX_CourseSectionSchedules_AcademicSemesterId_DayOfWeek",
                schema: "scheduling",
                table: "CourseSectionSchedules",
                columns: new[] { "AcademicSemesterId", "DayOfWeek" });

            migrationBuilder.CreateIndex(
                name: "IX_CourseSectionSchedules_CourseSectionId_ScheduleSlotId",
                schema: "scheduling",
                table: "CourseSectionSchedules",
                columns: new[] { "CourseSectionId", "ScheduleSlotId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourseSectionSchedules_RoomId_AcademicSemesterId_DayOfWeek_StartTime",
                schema: "scheduling",
                table: "CourseSectionSchedules",
                columns: new[] { "RoomId", "AcademicSemesterId", "DayOfWeek", "StartTime" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourseSectionSchedules_ScheduleId",
                schema: "scheduling",
                table: "CourseSectionSchedules",
                column: "ScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseSectionSchedules_ScheduleSlotId",
                schema: "scheduling",
                table: "CourseSectionSchedules",
                column: "ScheduleSlotId");

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_CourseId",
                schema: "registration",
                table: "Enrollments",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_CourseSectionId_Status",
                schema: "registration",
                table: "Enrollments",
                columns: new[] { "CourseSectionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_CourseVersionId",
                schema: "registration",
                table: "Enrollments",
                column: "CourseVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_RegistrationId_CourseSectionId",
                schema: "registration",
                table: "Enrollments",
                columns: new[] { "RegistrationId", "CourseSectionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_RegistrationId_IdempotencyKey",
                schema: "registration",
                table: "Enrollments",
                columns: new[] { "RegistrationId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InstructorAvailabilities_InstructorId_DayOfWeek_StartTime",
                schema: "scheduling",
                table: "InstructorAvailabilities",
                columns: new[] { "InstructorId", "DayOfWeek", "StartTime" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Registrations_AcademicSemesterId_Status",
                schema: "registration",
                table: "Registrations",
                columns: new[] { "AcademicSemesterId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Registrations_StudentId_AcademicSemesterId",
                schema: "registration",
                table: "Registrations",
                columns: new[] { "StudentId", "AcademicSemesterId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoomAvailabilities_RoomId_DayOfWeek_StartTime",
                schema: "scheduling",
                table: "RoomAvailabilities",
                columns: new[] { "RoomId", "DayOfWeek", "StartTime" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_AcademicSemesterId_Name",
                schema: "scheduling",
                table: "Schedules",
                columns: new[] { "AcademicSemesterId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleSlots_ScheduleId_DayOfWeek_StartTime_EndTime",
                schema: "scheduling",
                table: "ScheduleSlots",
                columns: new[] { "ScheduleId", "DayOfWeek", "StartTime", "EndTime" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CourseSectionSchedules",
                schema: "scheduling");

            migrationBuilder.DropTable(
                name: "Enrollments",
                schema: "registration");

            migrationBuilder.DropTable(
                name: "InstructorAvailabilities",
                schema: "scheduling");

            migrationBuilder.DropTable(
                name: "RoomAvailabilities",
                schema: "scheduling");

            migrationBuilder.DropTable(
                name: "ScheduleSlots",
                schema: "scheduling");

            migrationBuilder.DropTable(
                name: "Registrations",
                schema: "registration");

            migrationBuilder.DropTable(
                name: "Schedules",
                schema: "scheduling");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CourseSections_EnrolledCount",
                schema: "offerings",
                table: "CourseSections");

            migrationBuilder.DropColumn(
                name: "EnrolledCount",
                schema: "offerings",
                table: "CourseSections");
        }
    }
}
