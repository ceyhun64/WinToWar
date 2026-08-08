using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations.Payments
{
    /// <inheritdoc />
    public partial class AddWalletKycAndAgeConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AgeConfirmedAt",
                table: "Wallets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KycStatus",
                table: "Wallets",
                type: "text",
                nullable: false,
                defaultValue: "NotRequired");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AgeConfirmedAt",
                table: "Wallets");

            migrationBuilder.DropColumn(
                name: "KycStatus",
                table: "Wallets");
        }
    }
}
