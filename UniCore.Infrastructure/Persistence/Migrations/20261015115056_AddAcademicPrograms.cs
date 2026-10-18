using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAcademicPrograms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AcademicPrograms",
                schema: "university",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DegreeType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DurationYears = table.Column<int>(type: "int", nullable: false),
                    TotalRequiredCredits = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcademicPrograms", x => x.Id);
                    table.CheckConstraint("CK_AcademicPrograms_DurationYears", "[DurationYears] >= 1 AND [DurationYears] <= 10");
                    table.CheckConstraint("CK_AcademicPrograms_TotalRequiredCredits", "[TotalRequiredCredits] >= 1 AND [TotalRequiredCredits] <= 400");
                    table.ForeignKey(
                        name: "FK_AcademicPrograms_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalSchema: "university",
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AcademicPrograms_DepartmentId_Code",
                schema: "university",
                table: "AcademicPrograms",
                columns: new[] { "DepartmentId", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AcademicPrograms",
                schema: "university");
        }
    }
}
