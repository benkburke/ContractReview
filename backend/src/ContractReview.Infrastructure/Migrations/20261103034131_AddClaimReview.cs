using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContractReview.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClaimReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ReviewAgrees",
                table: "Claims",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewConfidence",
                table: "Claims",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewErrorCode",
                table: "Claims",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReviewIsCovered",
                table: "Claims",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewJson",
                table: "Claims",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewModel",
                table: "Claims",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewState",
                table: "Claims",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReviewAgrees",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "ReviewConfidence",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "ReviewErrorCode",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "ReviewIsCovered",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "ReviewJson",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "ReviewModel",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "ReviewState",
                table: "Claims");
        }
    }
}
