using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStudyPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "curriculum");

            migrationBuilder.EnsureSchema(
                name: "notifications");

            migrationBuilder.CreateTable(
                name: "NotificationPreferences",
                schema: "notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NotificationType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    InAppEnabled = table.Column<bool>(type: "bit", nullable: false),
                    EmailEnabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationPreferences_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                schema: "notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ReferenceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ReferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudyPlans",
                schema: "curriculum",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcademicProgramId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudyPlans_AcademicPrograms_AcademicProgramId",
                        column: x => x.AcademicProgramId,
                        principalSchema: "university",
                        principalTable: "AcademicPrograms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NotificationDeliveries",
                schema: "notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NotificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FailedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationDeliveries_Notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalSchema: "notifications",
                        principalTable: "Notifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudyPlanVersions",
                schema: "curriculum",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudyPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyPlanVersions", x => x.Id);
                    table.CheckConstraint("CK_StudyPlanVersions_EffectiveTo", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                    table.CheckConstraint("CK_StudyPlanVersions_VersionNumber", "[VersionNumber] >= 1");
                    table.ForeignKey(
                        name: "FK_StudyPlanVersions_StudyPlans_StudyPlanId",
                        column: x => x.StudyPlanId,
                        principalSchema: "curriculum",
                        principalTable: "StudyPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CourseRequirements",
                schema: "curriculum",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudyPlanVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequirementType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseRequirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseRequirements_StudyPlanVersions_StudyPlanVersionId",
                        column: x => x.StudyPlanVersionId,
                        principalSchema: "curriculum",
                        principalTable: "StudyPlanVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudyPlanCourses",
                schema: "curriculum",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudyPlanVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcademicLevelId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecommendedTermId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequirementType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RequiredCredits = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyPlanCourses", x => x.Id);
                    table.CheckConstraint("CK_StudyPlanCourses_RequiredCredits", "[RequiredCredits] IS NULL OR ([RequiredCredits] >= 1 AND [RequiredCredits] <= 30)");
                    table.ForeignKey(
                        name: "FK_StudyPlanCourses_AcademicLevels_AcademicLevelId",
                        column: x => x.AcademicLevelId,
                        principalSchema: "calendar",
                        principalTable: "AcademicLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyPlanCourses_AcademicTerms_RecommendedTermId",
                        column: x => x.RecommendedTermId,
                        principalSchema: "calendar",
                        principalTable: "AcademicTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyPlanCourses_StudyPlanVersions_StudyPlanVersionId",
                        column: x => x.StudyPlanVersionId,
                        principalSchema: "curriculum",
                        principalTable: "StudyPlanVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CourseRequirements_StudyPlanVersionId_CourseVersionId",
                schema: "curriculum",
                table: "CourseRequirements",
                columns: new[] { "StudyPlanVersionId", "CourseVersionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveries_NotificationId_Channel",
                schema: "notifications",
                table: "NotificationDeliveries",
                columns: new[] { "NotificationId", "Channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationPreferences_UserId_NotificationType",
                schema: "notifications",
                table: "NotificationPreferences",
                columns: new[] { "UserId", "NotificationType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_ReadAt_CreatedAt",
                schema: "notifications",
                table: "Notifications",
                columns: new[] { "UserId", "ReadAt", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_Notifications_Source",
                schema: "notifications",
                table: "Notifications",
                columns: new[] { "UserId", "SourceEventId", "Type" },
                unique: true,
                filter: "[SourceEventId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StudyPlanCourses_AcademicLevelId",
                schema: "curriculum",
                table: "StudyPlanCourses",
                column: "AcademicLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyPlanCourses_CourseVersionId",
                schema: "curriculum",
                table: "StudyPlanCourses",
                column: "CourseVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyPlanCourses_RecommendedTermId",
                schema: "curriculum",
                table: "StudyPlanCourses",
                column: "RecommendedTermId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyPlanCourses_StudyPlanVersionId_CourseVersionId",
                schema: "curriculum",
                table: "StudyPlanCourses",
                columns: new[] { "StudyPlanVersionId", "CourseVersionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudyPlans_AcademicProgramId_Code",
                schema: "curriculum",
                table: "StudyPlans",
                columns: new[] { "AcademicProgramId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudyPlanVersions_StudyPlanId_VersionNumber",
                schema: "curriculum",
                table: "StudyPlanVersions",
                columns: new[] { "StudyPlanId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_StudyPlanVersions_OneDraftPerPlan",
                schema: "curriculum",
                table: "StudyPlanVersions",
                column: "StudyPlanId",
                unique: true,
                filter: "[Status] = 'Draft'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CourseRequirements",
                schema: "curriculum");

            migrationBuilder.DropTable(
                name: "NotificationDeliveries",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "NotificationPreferences",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "StudyPlanCourses",
                schema: "curriculum");

            migrationBuilder.DropTable(
                name: "Notifications",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "StudyPlanVersions",
                schema: "curriculum");

            migrationBuilder.DropTable(
                name: "StudyPlans",
                schema: "curriculum");
        }
    }
}
