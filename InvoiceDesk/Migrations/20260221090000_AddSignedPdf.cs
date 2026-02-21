using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceDesk.Migrations
{
    /// <inheritdoc />
    public partial class AddSignedPdf : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "SignedPdf",
                table: "Invoices",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SignedPdfCreatedAtUtc",
                table: "Invoices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignedPdfFileName",
                table: "Invoices",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignedPdfSha256",
                table: "Invoices",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SignedPdf",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "SignedPdfCreatedAtUtc",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "SignedPdfFileName",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "SignedPdfSha256",
                table: "Invoices");
        }
    }
}
