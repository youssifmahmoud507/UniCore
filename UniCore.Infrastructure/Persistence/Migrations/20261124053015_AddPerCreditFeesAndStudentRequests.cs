using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPerCreditFeesAndStudentRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "requests");

            migrationBuilder.CreateTable(
                name: "LeaveRequests",
                schema: "requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpectedReturnDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FailureCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveRequests", x => x.Id);
                    table.CheckConstraint("CK_LeaveRequests_Dates", "[ExpectedReturnDate] > [StartDate]");
                    table.ForeignKey(
                        name: "FK_LeaveRequests_Students_StudentId",
                        column: x => x.StudentId,
                        principalSchema: "students",
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WithdrawalRequests",
                schema: "requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RegistrationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnrollmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseSectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FailureCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WithdrawalRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WithdrawalRequests_Registrations_RegistrationId",
                        column: x => x.RegistrationId,
                        principalSchema: "registration",
                        principalTable: "Registrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WithdrawalRequests_Students_StudentId",
                        column: x => x.StudentId,
                        principalSchema: "students",
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_RequestedByUserId",
                schema: "requests",
                table: "LeaveRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_WorkflowInstanceId",
                schema: "requests",
                table: "LeaveRequests",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "UX_LeaveRequests_PendingPerStudent",
                schema: "requests",
                table: "LeaveRequests",
                column: "StudentId",
                unique: true,
                filter: "[Status] = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_WithdrawalRequests_RegistrationId",
                schema: "requests",
                table: "WithdrawalRequests",
                column: "RegistrationId");

            migrationBuilder.CreateIndex(
                name: "IX_WithdrawalRequests_RequestedByUserId",
                schema: "requests",
                table: "WithdrawalRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WithdrawalRequests_StudentId",
                schema: "requests",
                table: "WithdrawalRequests",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_WithdrawalRequests_WorkflowInstanceId",
                schema: "requests",
                table: "WithdrawalRequests",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "UX_WithdrawalRequests_PendingPerEnrollment",
                schema: "requests",
                table: "WithdrawalRequests",
                column: "EnrollmentId",
                unique: true,
                filter: "[Status] = 'Pending'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeaveRequests",
                schema: "requests");

            migrationBuilder.DropTable(
                name: "WithdrawalRequests",
                schema: "requests");
        }
    }
}
