using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations.Payments
{
    /// <inheritdoc />
    public partial class PayoutRefundWalletCredit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReconciliationLocks");

            migrationBuilder.DropColumn(
                name: "PayoutAddress",
                table: "Wallets");

            migrationBuilder.DropColumn(
                name: "PayoutAddressFormat",
                table: "Wallets");

            migrationBuilder.DropColumn(
                name: "AmountLtc",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "BtcPayTransactionId",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "NextRetryAt",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "CommissionLtc",
                table: "Payouts");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "Payouts");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Payouts");

            migrationBuilder.DropColumn(
                name: "TotalPoolLtc",
                table: "Payouts");

            migrationBuilder.DropColumn(
                name: "AmountLtc",
                table: "PayoutRecipients");

            migrationBuilder.DropColumn(
                name: "BtcPayTransactionId",
                table: "PayoutRecipients");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "PayoutRecipients");

            migrationBuilder.DropColumn(
                name: "NetworkFeeLtc",
                table: "PayoutRecipients");

            migrationBuilder.DropColumn(
                name: "NextRetryAt",
                table: "PayoutRecipients");

            migrationBuilder.DropColumn(
                name: "PayoutAddress",
                table: "PayoutRecipients");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "PayoutRecipients");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "PayoutRecipients");

            migrationBuilder.DropColumn(
                name: "PayoutAddress",
                table: "PaymentInvoices");

            migrationBuilder.DropColumn(
                name: "PayoutAddressFormat",
                table: "PaymentInvoices");

            migrationBuilder.AddColumn<decimal>(
                name: "AmountUsd",
                table: "Refunds",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CommissionUsd",
                table: "Payouts",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalPoolUsd",
                table: "Payouts",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AmountUsd",
                table: "PayoutRecipients",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "PayoutRecipients",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AmountUsd",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "CommissionUsd",
                table: "Payouts");

            migrationBuilder.DropColumn(
                name: "TotalPoolUsd",
                table: "Payouts");

            migrationBuilder.DropColumn(
                name: "AmountUsd",
                table: "PayoutRecipients");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "PayoutRecipients");

            migrationBuilder.AddColumn<string>(
                name: "PayoutAddress",
                table: "Wallets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayoutAddressFormat",
                table: "Wallets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AmountLtc",
                table: "Refunds",
                type: "numeric(18,8)",
                precision: 18,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "BtcPayTransactionId",
                table: "Refunds",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletedAt",
                table: "Refunds",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextRetryAt",
                table: "Refunds",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "Refunds",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Refunds",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "CommissionLtc",
                table: "Payouts",
                type: "numeric(18,8)",
                precision: 18,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletedAt",
                table: "Payouts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Payouts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "TotalPoolLtc",
                table: "Payouts",
                type: "numeric(18,8)",
                precision: 18,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AmountLtc",
                table: "PayoutRecipients",
                type: "numeric(18,8)",
                precision: 18,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "BtcPayTransactionId",
                table: "PayoutRecipients",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletedAt",
                table: "PayoutRecipients",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NetworkFeeLtc",
                table: "PayoutRecipients",
                type: "numeric(18,8)",
                precision: 18,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextRetryAt",
                table: "PayoutRecipients",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayoutAddress",
                table: "PayoutRecipients",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "PayoutRecipients",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "PayoutRecipients",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PayoutAddress",
                table: "PaymentInvoices",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PayoutAddressFormat",
                table: "PaymentInvoices",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ReconciliationLocks",
                columns: table => new
                {
                    LockName = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    HolderId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReconciliationLocks", x => x.LockName);
                });
        }
    }
}
