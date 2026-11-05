using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Estoka.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductBarcode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "barcode",
                table: "products",
                type: "character varying(14)",
                maxLength: 14,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_products_barcode",
                table: "products",
                column: "barcode",
                unique: true,
                filter: "barcode IS NOT NULL AND is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_products_barcode",
                table: "products");

            migrationBuilder.DropColumn(
                name: "barcode",
                table: "products");
        }
    }
}
