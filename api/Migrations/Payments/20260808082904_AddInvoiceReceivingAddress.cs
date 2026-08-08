using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations.Payments
{
    /// <inheritdoc />
    public partial class AddInvoiceReceivingAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Bip21Uri",
                table: "PaymentInvoices",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReceivingAddress",
                table: "PaymentInvoices",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Bip21Uri",
                table: "PaymentInvoices");

            migrationBuilder.DropColumn(
                name: "ReceivingAddress",
                table: "PaymentInvoices");
        }
    }
}
