using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CinemaReservation.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMovieFeaturedFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsFeatured",
                table: "Movies",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsFeatured",
                table: "Movies");
        }
    }
}
