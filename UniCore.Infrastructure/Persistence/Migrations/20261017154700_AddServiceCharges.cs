using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceCharges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_StudentCharges_Live",
                schema: "finance",
                table: "StudentCharges");

            migrationBuilder.AlterColumn<Guid>(
                name: "FeeStructureItemId",
                schema: "finance",
                table: "StudentCharges",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "AcademicSemesterId",
                schema: "finance",
                table: "StudentCharges",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "ServiceReference",
                schema: "finance",
                table: "StudentCharges",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_StudentCharges_Live",
                schema: "finance",
                table: "StudentCharges",
                columns: new[] { "StudentId", "AcademicSemesterId", "FeeDefinitionId" },
                unique: true,
                filter: "[Status] <> 'Cancelled' AND [AcademicSemesterId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_StudentCharges_ServiceReference",
                schema: "finance",
                table: "StudentCharges",
                columns: new[] { "StudentId", "ServiceReference" },
                unique: true,
                filter: "[ServiceReference] IS NOT NULL AND [Status] <> 'Cancelled'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_StudentCharges_Live",
                schema: "finance",
                table: "StudentCharges");

            migrationBuilder.DropIndex(
                name: "UX_StudentCharges_ServiceReference",
                schema: "finance",
                table: "StudentCharges");

            migrationBuilder.DropColumn(
                name: "ServiceReference",
                schema: "finance",
                table: "StudentCharges");

            migrationBuilder.AlterColumn<Guid>(
                name: "FeeStructureItemId",
                schema: "finance",
                table: "StudentCharges",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "AcademicSemesterId",
                schema: "finance",
                table: "StudentCharges",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_StudentCharges_Live",
                schema: "finance",
                table: "StudentCharges",
                columns: new[] { "StudentId", "AcademicSemesterId", "FeeDefinitionId" },
                unique: true,
                filter: "[Status] <> 'Cancelled'");
        }
    }
}
