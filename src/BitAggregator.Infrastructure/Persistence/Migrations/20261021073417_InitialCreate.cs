using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BitAggregator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AggregatedPrices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Instrument = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Price = table.Column<double>(type: "REAL", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AggregatedPrices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SourcePrices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AggregatedPriceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Price = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourcePrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SourcePrices_AggregatedPrices_AggregatedPriceId",
                        column: x => x.AggregatedPriceId,
                        principalTable: "AggregatedPrices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AggregatedPrices_Instrument_Timestamp",
                table: "AggregatedPrices",
                columns: new[] { "Instrument", "Timestamp" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SourcePrices_AggregatedPriceId_SourceName",
                table: "SourcePrices",
                columns: new[] { "AggregatedPriceId", "SourceName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SourcePrices");

            migrationBuilder.DropTable(
                name: "AggregatedPrices");
        }
    }
}
