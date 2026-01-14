using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceDesk.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyEik : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Eik",
                table: "Companies",
                type: "nvarchar(13)",
                maxLength: 13,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Eik",
                table: "Companies");
        }
    }
}
