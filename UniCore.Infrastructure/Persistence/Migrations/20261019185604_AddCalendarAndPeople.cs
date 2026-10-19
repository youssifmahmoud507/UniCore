using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarAndPeople : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "calendar");

            migrationBuilder.EnsureSchema(
                name: "people");

            migrationBuilder.CreateTable(
                name: "AcademicLevels",
                schema: "calendar",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcademicProgramId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    RequiredCredits = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcademicLevels", x => x.Id);
                    table.CheckConstraint("CK_AcademicLevels_Order", "[Order] >= 1 AND [Order] <= 20");
                    table.CheckConstraint("CK_AcademicLevels_RequiredCredits", "[RequiredCredits] >= 0 AND [RequiredCredits] <= 400");
                    table.ForeignKey(
                        name: "FK_AcademicLevels_AcademicPrograms_AcademicProgramId",
                        column: x => x.AcademicProgramId,
                        principalSchema: "university",
                        principalTable: "AcademicPrograms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AcademicTerms",
                schema: "calendar",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcademicTerms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AcademicYears",
                schema: "calendar",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcademicYears", x => x.Id);
                    table.CheckConstraint("CK_AcademicYears_Dates", "[StartDate] < [EndDate]");
                });

            migrationBuilder.CreateTable(
                name: "PersonProfiles",
                schema: "people",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    Gender = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    NationalId = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AcademicSemesters",
                schema: "calendar",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcademicTermId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RegistrationStart = table.Column<DateOnly>(type: "date", nullable: true),
                    RegistrationEnd = table.Column<DateOnly>(type: "date", nullable: true),
                    AddDropStart = table.Column<DateOnly>(type: "date", nullable: true),
                    AddDropEnd = table.Column<DateOnly>(type: "date", nullable: true),
                    WithdrawalDeadline = table.Column<DateOnly>(type: "date", nullable: true),
                    MidtermStart = table.Column<DateOnly>(type: "date", nullable: true),
                    MidtermEnd = table.Column<DateOnly>(type: "date", nullable: true),
                    FinalExamStart = table.Column<DateOnly>(type: "date", nullable: true),
                    FinalExamEnd = table.Column<DateOnly>(type: "date", nullable: true),
                    GradeSubmissionDeadline = table.Column<DateOnly>(type: "date", nullable: true),
                    GradePublicationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcademicSemesters", x => x.Id);
                    table.CheckConstraint("CK_AcademicSemesters_Dates", "[StartDate] < [EndDate]");
                    table.ForeignKey(
                        name: "FK_AcademicSemesters_AcademicTerms_AcademicTermId",
                        column: x => x.AcademicTermId,
                        principalSchema: "calendar",
                        principalTable: "AcademicTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AcademicSemesters_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalSchema: "calendar",
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Employees",
                schema: "people",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    JobTitle = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    HireDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TerminationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Employees_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalSchema: "university",
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Employees_PersonProfiles_PersonProfileId",
                        column: x => x.PersonProfileId,
                        principalSchema: "people",
                        principalTable: "PersonProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Instructors",
                schema: "people",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Rank = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Specialization = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Instructors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Instructors_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "people",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TeachingAssistants",
                schema: "people",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeachingAssistants", x => x.Id);
                    table.CheckConstraint("CK_TeachingAssistants_EmployeeOrStudent", "[EmployeeId] IS NOT NULL OR [StudentId] IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_TeachingAssistants_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "people",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeachingAssistants_Students_StudentId",
                        column: x => x.StudentId,
                        principalSchema: "students",
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AcademicLevels_AcademicProgramId_Name",
                schema: "calendar",
                table: "AcademicLevels",
                columns: new[] { "AcademicProgramId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AcademicLevels_AcademicProgramId_Order",
                schema: "calendar",
                table: "AcademicLevels",
                columns: new[] { "AcademicProgramId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AcademicSemesters_AcademicTermId",
                schema: "calendar",
                table: "AcademicSemesters",
                column: "AcademicTermId");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicSemesters_AcademicYearId_AcademicTermId",
                schema: "calendar",
                table: "AcademicSemesters",
                columns: new[] { "AcademicYearId", "AcademicTermId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AcademicSemesters_Status",
                schema: "calendar",
                table: "AcademicSemesters",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicTerms_Code",
                schema: "calendar",
                table: "AcademicTerms",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AcademicYears_IsCurrent",
                schema: "calendar",
                table: "AcademicYears",
                column: "IsCurrent",
                unique: true,
                filter: "[IsCurrent] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicYears_Name",
                schema: "calendar",
                table: "AcademicYears",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_DepartmentId",
                schema: "people",
                table: "Employees",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_EmployeeNumber",
                schema: "people",
                table: "Employees",
                column: "EmployeeNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_PersonProfileId",
                schema: "people",
                table: "Employees",
                column: "PersonProfileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Instructors_EmployeeId",
                schema: "people",
                table: "Instructors",
                column: "EmployeeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PersonProfiles_LastName_FirstName",
                schema: "people",
                table: "PersonProfiles",
                columns: new[] { "LastName", "FirstName" });

            migrationBuilder.CreateIndex(
                name: "IX_PersonProfiles_NationalId",
                schema: "people",
                table: "PersonProfiles",
                column: "NationalId",
                unique: true,
                filter: "[NationalId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PersonProfiles_UserId",
                schema: "people",
                table: "PersonProfiles",
                column: "UserId",
                unique: true,
                filter: "[UserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TeachingAssistants_EmployeeId",
                schema: "people",
                table: "TeachingAssistants",
                column: "EmployeeId",
                unique: true,
                filter: "[EmployeeId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TeachingAssistants_StudentId",
                schema: "people",
                table: "TeachingAssistants",
                column: "StudentId",
                unique: true,
                filter: "[StudentId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AcademicLevels",
                schema: "calendar");

            migrationBuilder.DropTable(
                name: "AcademicSemesters",
                schema: "calendar");

            migrationBuilder.DropTable(
                name: "Instructors",
                schema: "people");

            migrationBuilder.DropTable(
                name: "TeachingAssistants",
                schema: "people");

            migrationBuilder.DropTable(
                name: "AcademicTerms",
                schema: "calendar");

            migrationBuilder.DropTable(
                name: "AcademicYears",
                schema: "calendar");

            migrationBuilder.DropTable(
                name: "Employees",
                schema: "people");

            migrationBuilder.DropTable(
                name: "PersonProfiles",
                schema: "people");
        }
    }
}
