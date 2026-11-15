using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTranscripts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "transcript");

            migrationBuilder.CreateTable(
                name: "TranscriptSnapshots",
                schema: "transcript",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    VerificationCode = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    IssuedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IssuedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RepeatPolicy = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CumulativeGpa = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: false),
                    AttemptedCredits = table.Column<int>(type: "int", nullable: false),
                    EarnedCredits = table.Column<int>(type: "int", nullable: false),
                    Standing = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TranscriptSnapshots", x => x.Id);
                    table.CheckConstraint("CK_TranscriptSnapshots_Credits", "[AttemptedCredits] >= 0 AND [EarnedCredits] >= 0 AND [EarnedCredits] <= [AttemptedCredits]");
                    table.CheckConstraint("CK_TranscriptSnapshots_Gpa", "[CumulativeGpa] >= 0 AND [CumulativeGpa] <= 5");
                });

            migrationBuilder.CreateTable(
                name: "TranscriptEntries",
                schema: "transcript",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TranscriptSnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SequenceNo = table.Column<int>(type: "int", nullable: false),
                    GradeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SemesterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SemesterLabel = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SemesterStart = table.Column<DateOnly>(type: "date", nullable: false),
                    CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CourseTitle = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Credits = table.Column<int>(type: "int", nullable: false),
                    Percentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    Letter = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    GradePoints = table.Column<decimal>(type: "decimal(3,2)", precision: 3, scale: 2, nullable: false),
                    IsPassing = table.Column<bool>(type: "bit", nullable: false),
                    IsCounted = table.Column<bool>(type: "bit", nullable: false),
                    SemesterGpa = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: false),
                    CumulativeGpa = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TranscriptEntries", x => x.Id);
                    table.CheckConstraint("CK_TranscriptEntries_Credits", "[Credits] >= 0");
                    table.CheckConstraint("CK_TranscriptEntries_Percentage", "[Percentage] >= 0 AND [Percentage] <= 100");
                    table.ForeignKey(
                        name: "FK_TranscriptEntries_TranscriptSnapshots_TranscriptSnapshotId",
                        column: x => x.TranscriptSnapshotId,
                        principalSchema: "transcript",
                        principalTable: "TranscriptSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TranscriptEntries_TranscriptSnapshotId_SequenceNo",
                schema: "transcript",
                table: "TranscriptEntries",
                columns: new[] { "TranscriptSnapshotId", "SequenceNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TranscriptSnapshots_StudentId_IssuedAt",
                schema: "transcript",
                table: "TranscriptSnapshots",
                columns: new[] { "StudentId", "IssuedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TranscriptSnapshots_VerificationCode",
                schema: "transcript",
                table: "TranscriptSnapshots",
                column: "VerificationCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TranscriptEntries",
                schema: "transcript");

            migrationBuilder.DropTable(
                name: "TranscriptSnapshots",
                schema: "transcript");
        }
    }
}
