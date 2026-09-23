using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaneHesap.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AutoExpensesAndPaymentMethods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Expenses_BusinessId",
                table: "Expenses");

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentCardId",
                table: "SupplierPayments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentMethod",
                table: "SupplierPayments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentCardId",
                table: "RecurringExpensePayments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentMethod",
                table: "RecurringExpensePayments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceReferenceId",
                table: "Expenses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceReferenceType",
                table: "Expenses",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_BusinessId_SourceReferenceType_SourceReferenceId",
                table: "Expenses",
                columns: new[] { "BusinessId", "SourceReferenceType", "SourceReferenceId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Expenses_BusinessId_SourceReferenceType_SourceReferenceId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "PaymentCardId",
                table: "SupplierPayments");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "SupplierPayments");

            migrationBuilder.DropColumn(
                name: "PaymentCardId",
                table: "RecurringExpensePayments");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "RecurringExpensePayments");

            migrationBuilder.DropColumn(
                name: "SourceReferenceId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "SourceReferenceType",
                table: "Expenses");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_BusinessId",
                table: "Expenses",
                column: "BusinessId");
        }
    }
}
