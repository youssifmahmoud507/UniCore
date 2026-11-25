using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentServices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "studentservices");

            migrationBuilder.CreateTable(
                name: "Appeals",
                schema: "studentservices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Target = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DecidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecisionNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Appeals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CertificateRequests",
                schema: "studentservices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CertificateType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ChargeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DecidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecisionNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertificateRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Complaints",
                schema: "studentservices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Details = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DecidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecisionNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Complaints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TranscriptRequests",
                schema: "studentservices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ChargeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TranscriptSnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DecidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecisionNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TranscriptRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Appeals_Status_SubmittedAt",
                schema: "studentservices",
                table: "Appeals",
                columns: new[] { "Status", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Appeals_StudentId_SubmittedAt",
                schema: "studentservices",
                table: "Appeals",
                columns: new[] { "StudentId", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Appeals_WorkflowInstanceId",
                schema: "studentservices",
                table: "Appeals",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_CertificateRequests_Status_SubmittedAt",
                schema: "studentservices",
                table: "CertificateRequests",
                columns: new[] { "Status", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CertificateRequests_StudentId_SubmittedAt",
                schema: "studentservices",
                table: "CertificateRequests",
                columns: new[] { "StudentId", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CertificateRequests_WorkflowInstanceId",
                schema: "studentservices",
                table: "CertificateRequests",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "UX_CertificateRequests_OneOpenPerStudentAndType",
                schema: "studentservices",
                table: "CertificateRequests",
                columns: new[] { "StudentId", "CertificateType" },
                unique: true,
                filter: "[Status] IN ('Pending', 'Returned')");

            migrationBuilder.CreateIndex(
                name: "IX_Complaints_Status_SubmittedAt",
                schema: "studentservices",
                table: "Complaints",
                columns: new[] { "Status", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Complaints_StudentId_SubmittedAt",
                schema: "studentservices",
                table: "Complaints",
                columns: new[] { "StudentId", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Complaints_WorkflowInstanceId",
                schema: "studentservices",
                table: "Complaints",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_TranscriptRequests_Status_SubmittedAt",
                schema: "studentservices",
                table: "TranscriptRequests",
                columns: new[] { "Status", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TranscriptRequests_StudentId_SubmittedAt",
                schema: "studentservices",
                table: "TranscriptRequests",
                columns: new[] { "StudentId", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TranscriptRequests_WorkflowInstanceId",
                schema: "studentservices",
                table: "TranscriptRequests",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "UX_TranscriptRequests_OneOpenPerStudent",
                schema: "studentservices",
                table: "TranscriptRequests",
                column: "StudentId",
                unique: true,
                filter: "[Status] IN ('Pending', 'Returned')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Appeals",
                schema: "studentservices");

            migrationBuilder.DropTable(
                name: "CertificateRequests",
                schema: "studentservices");

            migrationBuilder.DropTable(
                name: "Complaints",
                schema: "studentservices");

            migrationBuilder.DropTable(
                name: "TranscriptRequests",
                schema: "studentservices");
        }
    }
}
