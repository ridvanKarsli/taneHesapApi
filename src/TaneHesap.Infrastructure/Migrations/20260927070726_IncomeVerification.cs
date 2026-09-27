using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaneHesap.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class IncomeVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyIncomeVerifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    VerificationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ActualCash = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ActualCard = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyIncomeVerifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyIncomeVerifications_Businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyIncomeVerifications_BusinessId_VerificationDate",
                table: "DailyIncomeVerifications",
                columns: new[] { "BusinessId", "VerificationDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyIncomeVerifications");
        }
    }
}
