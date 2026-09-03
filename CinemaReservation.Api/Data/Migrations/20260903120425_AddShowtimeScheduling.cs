using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CinemaReservation.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddShowtimeScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Showtimes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MovieId = table.Column<int>(type: "integer", nullable: false),
                    AuditoriumId = table.Column<int>(type: "integer", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TicketPrice = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Showtimes", x => x.Id);
                    table.CheckConstraint("CK_Showtimes_EndsAt_After_StartsAt", "\"EndsAt\" > \"StartsAt\"");
                    table.CheckConstraint("CK_Showtimes_TicketPrice_Positive", "\"TicketPrice\" > 0");
                    table.ForeignKey(
                        name: "FK_Showtimes_Auditoriums_AuditoriumId",
                        column: x => x.AuditoriumId,
                        principalTable: "Auditoriums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Showtimes_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Showtimes_AuditoriumId_StartsAt",
                table: "Showtimes",
                columns: new[] { "AuditoriumId", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Showtimes_MovieId_StartsAt",
                table: "Showtimes",
                columns: new[] { "MovieId", "StartsAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Showtimes");
        }
    }
}
