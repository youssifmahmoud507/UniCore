using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPerCreditFees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_StudentCharges_Live",
                schema: "finance",
                table: "StudentCharges");

            migrationBuilder.AddColumn<int>(
                name: "Credits",
                schema: "finance",
                table: "StudentCharges",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EnrollmentId",
                schema: "finance",
                table: "StudentCharges",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Basis",
                schema: "finance",
                table: "FeeDefinitions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Fixed");

            migrationBuilder.CreateIndex(
                name: "UX_StudentCharges_Enrollment",
                schema: "finance",
                table: "StudentCharges",
                columns: new[] { "EnrollmentId", "FeeDefinitionId" },
                unique: true,
                filter: "[EnrollmentId] IS NOT NULL AND [Status] <> 'Cancelled'");

            migrationBuilder.CreateIndex(
                name: "UX_StudentCharges_Live",
                schema: "finance",
                table: "StudentCharges",
                columns: new[] { "StudentId", "AcademicSemesterId", "FeeDefinitionId" },
                unique: true,
                filter: "[Status] <> 'Cancelled' AND [AcademicSemesterId] IS NOT NULL AND [EnrollmentId] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_StudentCharges_Enrollment",
                schema: "finance",
                table: "StudentCharges");

            migrationBuilder.DropIndex(
                name: "UX_StudentCharges_Live",
                schema: "finance",
                table: "StudentCharges");

            migrationBuilder.DropColumn(
                name: "Credits",
                schema: "finance",
                table: "StudentCharges");

            migrationBuilder.DropColumn(
                name: "EnrollmentId",
                schema: "finance",
                table: "StudentCharges");

            migrationBuilder.DropColumn(
                name: "Basis",
                schema: "finance",
                table: "FeeDefinitions");

            migrationBuilder.CreateIndex(
                name: "UX_StudentCharges_Live",
                schema: "finance",
                table: "StudentCharges",
                columns: new[] { "StudentId", "AcademicSemesterId", "FeeDefinitionId" },
                unique: true,
                filter: "[Status] <> 'Cancelled' AND [AcademicSemesterId] IS NOT NULL");
        }
    }
}
