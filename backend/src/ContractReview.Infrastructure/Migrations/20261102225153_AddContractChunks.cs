using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContractReview.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddContractChunks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EmbeddingDimensions",
                table: "Contracts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmbeddingModel",
                table: "Contracts",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IndexError",
                table: "Contracts",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IndexStatus",
                table: "Contracts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "NotIndexed");

            migrationBuilder.AddColumn<DateTime>(
                name: "IndexedAtUtc",
                table: "Contracts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ContractChunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SectionOrder = table.Column<int>(type: "int", nullable: false),
                    SectionHeading = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ChunkOrder = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Embedding = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractChunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractChunks_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_IndexStatus",
                table: "Contracts",
                column: "IndexStatus");

            migrationBuilder.CreateIndex(
                name: "IX_ContractChunks_ContractId_SectionOrder_ChunkOrder",
                table: "ContractChunks",
                columns: new[] { "ContractId", "SectionOrder", "ChunkOrder" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContractChunks");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_IndexStatus",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "EmbeddingDimensions",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "EmbeddingModel",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "IndexError",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "IndexStatus",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "IndexedAtUtc",
                table: "Contracts");
        }
    }
}
