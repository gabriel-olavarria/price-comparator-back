using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PriceComparator.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeConcepts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "PriceComparator");

            migrationBuilder.CreateTable(
                name: "Category",
                schema: "PriceComparator",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Category", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExtractedSearch",
                schema: "PriceComparator",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Store = table.Column<string>(type: "text", nullable: false),
                    SearchQuery = table.Column<string>(type: "text", nullable: false),
                    Products = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    ProductCount = table.Column<int>(type: "integer", nullable: false),
                    ExtractedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExtractedSearch", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Concept",
                schema: "PriceComparator",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    CategoryId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Concept", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Concept_Category_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "PriceComparator",
                        principalTable: "Category",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Aliases",
                schema: "PriceComparator",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ConceptId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Aliases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Aliases_Concept_ConceptId",
                        column: x => x.ConceptId,
                        principalSchema: "PriceComparator",
                        principalTable: "Concept",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Subtype",
                schema: "PriceComparator",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ConceptId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subtype", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subtype_Concept_ConceptId",
                        column: x => x.ConceptId,
                        principalSchema: "PriceComparator",
                        principalTable: "Concept",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Term",
                schema: "PriceComparator",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    SubtypeId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Term", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Term_Subtype_SubtypeId",
                        column: x => x.SubtypeId,
                        principalSchema: "PriceComparator",
                        principalTable: "Subtype",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Aliases_ConceptId",
                schema: "PriceComparator",
                table: "Aliases",
                column: "ConceptId");

            migrationBuilder.CreateIndex(
                name: "IX_Concept_CategoryId",
                schema: "PriceComparator",
                table: "Concept",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ExtractedSearch_Store_SearchQuery",
                schema: "PriceComparator",
                table: "ExtractedSearch",
                columns: new[] { "Store", "SearchQuery" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subtype_ConceptId",
                schema: "PriceComparator",
                table: "Subtype",
                column: "ConceptId");

            migrationBuilder.CreateIndex(
                name: "IX_Term_SubtypeId",
                schema: "PriceComparator",
                table: "Term",
                column: "SubtypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Aliases",
                schema: "PriceComparator");

            migrationBuilder.DropTable(
                name: "ExtractedSearch",
                schema: "PriceComparator");

            migrationBuilder.DropTable(
                name: "Term",
                schema: "PriceComparator");

            migrationBuilder.DropTable(
                name: "Subtype",
                schema: "PriceComparator");

            migrationBuilder.DropTable(
                name: "Concept",
                schema: "PriceComparator");

            migrationBuilder.DropTable(
                name: "Category",
                schema: "PriceComparator");
        }
    }
}
