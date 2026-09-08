using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CinemaReservation.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTmdbMovieIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TmdbId",
                table: "Movies",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Movies_TmdbId",
                table: "Movies",
                column: "TmdbId",
                unique: true,
                filter: "\"TmdbId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Movies_TmdbId",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "TmdbId",
                table: "Movies");
        }
    }
}
