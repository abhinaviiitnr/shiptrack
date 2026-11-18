using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orders.Api.Migrations;

/// <inheritdoc />
public partial class AddOrderCompositeIndexes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_Orders_CustomerId_CreatedAt",
            table: "Orders",
            columns: new[] { "CustomerId", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_Orders_Status_CreatedAt",
            table: "Orders",
            columns: new[] { "Status", "CreatedAt" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Orders_CustomerId_CreatedAt",
            table: "Orders");

        migrationBuilder.DropIndex(
            name: "IX_Orders_Status_CreatedAt",
            table: "Orders");
    }
}
