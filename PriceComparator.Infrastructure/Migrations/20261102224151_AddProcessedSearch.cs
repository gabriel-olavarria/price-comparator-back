using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PriceComparator.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessedSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcessedSearch",
                schema: "PriceComparator",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Store = table.Column<string>(type: "text", nullable: false),
                    SearchQuery = table.Column<string>(type: "text", nullable: false),
                    Products = table.Column<string>(type: "jsonb", nullable: false),
                    ProductCount = table.Column<int>(type: "integer", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedSearch", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedSearch_Store_SearchQuery",
                schema: "PriceComparator",
                table: "ProcessedSearch",
                columns: new[] { "Store", "SearchQuery" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcessedSearch",
                schema: "PriceComparator");
        }
    }
}
