using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "admissions");

            migrationBuilder.CreateTable(
                name: "AdmissionRequirements",
                schema: "admissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcademicProgramId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdmissionRequirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdmissionRequirements_AcademicPrograms_AcademicProgramId",
                        column: x => x.AcademicProgramId,
                        principalSchema: "university",
                        principalTable: "AcademicPrograms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Applicants",
                schema: "admissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Applicants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdmissionApplications",
                schema: "admissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcademicProgramId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Deadline = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReviewStartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdmissionApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdmissionApplications_AcademicPrograms_AcademicProgramId",
                        column: x => x.AcademicProgramId,
                        principalSchema: "university",
                        principalTable: "AcademicPrograms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdmissionApplications_Applicants_ApplicantId",
                        column: x => x.ApplicantId,
                        principalSchema: "admissions",
                        principalTable: "Applicants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AdmissionApplicationRequirements",
                schema: "admissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdmissionApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdmissionRequirementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DocumentAttachedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReviewStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReviewNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReviewedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdmissionApplicationRequirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdmissionApplicationRequirements_AdmissionApplications_AdmissionApplicationId",
                        column: x => x.AdmissionApplicationId,
                        principalSchema: "admissions",
                        principalTable: "AdmissionApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AdmissionApplicationRequirements_AdmissionRequirements_AdmissionRequirementId",
                        column: x => x.AdmissionRequirementId,
                        principalSchema: "admissions",
                        principalTable: "AdmissionRequirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AdmissionDecisions",
                schema: "admissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdmissionApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DecidedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdmissionDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdmissionDecisions_AdmissionApplications_AdmissionApplicationId",
                        column: x => x.AdmissionApplicationId,
                        principalSchema: "admissions",
                        principalTable: "AdmissionApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionApplicationRequirements_AdmissionApplicationId_AdmissionRequirementId",
                schema: "admissions",
                table: "AdmissionApplicationRequirements",
                columns: new[] { "AdmissionApplicationId", "AdmissionRequirementId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionApplicationRequirements_AdmissionRequirementId",
                schema: "admissions",
                table: "AdmissionApplicationRequirements",
                column: "AdmissionRequirementId");

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionApplicationRequirements_DocumentId",
                schema: "admissions",
                table: "AdmissionApplicationRequirements",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionApplications_AcademicProgramId_Status",
                schema: "admissions",
                table: "AdmissionApplications",
                columns: new[] { "AcademicProgramId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionApplications_Status_Deadline",
                schema: "admissions",
                table: "AdmissionApplications",
                columns: new[] { "Status", "Deadline" });

            migrationBuilder.CreateIndex(
                name: "UX_AdmissionApplications_ActivePerApplicantProgramYear",
                schema: "admissions",
                table: "AdmissionApplications",
                columns: new[] { "ApplicantId", "AcademicProgramId", "AcademicYearId" },
                unique: true,
                filter: "[Status] <> 'Rejected' AND [Status] <> 'Withdrawn' AND [Status] <> 'Expired'");

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionDecisions_AdmissionApplicationId",
                schema: "admissions",
                table: "AdmissionDecisions",
                column: "AdmissionApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionRequirements_AcademicProgramId_Code",
                schema: "admissions",
                table: "AdmissionRequirements",
                columns: new[] { "AcademicProgramId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Applicants_Email",
                schema: "admissions",
                table: "Applicants",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Applicants_UserId",
                schema: "admissions",
                table: "Applicants",
                column: "UserId",
                unique: true,
                filter: "[UserId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdmissionApplicationRequirements",
                schema: "admissions");

            migrationBuilder.DropTable(
                name: "AdmissionDecisions",
                schema: "admissions");

            migrationBuilder.DropTable(
                name: "AdmissionRequirements",
                schema: "admissions");

            migrationBuilder.DropTable(
                name: "AdmissionApplications",
                schema: "admissions");

            migrationBuilder.DropTable(
                name: "Applicants",
                schema: "admissions");
        }
    }
}
