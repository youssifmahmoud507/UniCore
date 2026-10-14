using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFeeCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "finance");

            migrationBuilder.CreateTable(
                name: "FeeDefinitions",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniversityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DefaultAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeeDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeeStructures",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcademicProgramId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AcademicSemesterId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ActivatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeeStructures", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeeStructureItems",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FeeStructureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FeeDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeeStructureItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeeStructureItems_FeeDefinitions_FeeDefinitionId",
                        column: x => x.FeeDefinitionId,
                        principalSchema: "finance",
                        principalTable: "FeeDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FeeStructureItems_FeeStructures_FeeStructureId",
                        column: x => x.FeeStructureId,
                        principalSchema: "finance",
                        principalTable: "FeeStructures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeeDefinitions_IsActive",
                schema: "finance",
                table: "FeeDefinitions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_FeeDefinitions_UniversityId_Code",
                schema: "finance",
                table: "FeeDefinitions",
                columns: new[] { "UniversityId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FeeStructureItems_FeeDefinitionId",
                schema: "finance",
                table: "FeeStructureItems",
                column: "FeeDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeStructureItems_FeeStructureId_FeeDefinitionId",
                schema: "finance",
                table: "FeeStructureItems",
                columns: new[] { "FeeStructureId", "FeeDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FeeStructures_Status",
                schema: "finance",
                table: "FeeStructures",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "UX_FeeStructures_ActiveScope",
                schema: "finance",
                table: "FeeStructures",
                columns: new[] { "AcademicProgramId", "AcademicSemesterId" },
                unique: true,
                filter: "[Status] = 'Active'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeeStructureItems",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "FeeDefinitions",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "FeeStructures",
                schema: "finance");
        }
    }
}
