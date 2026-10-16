using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoviePlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRatingsAndUpdatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ImdbRating",
                table: "Series",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "KpRating",
                table: "Series",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Series",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ImdbRating",
                table: "Movies",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ImdbVotes",
                table: "Movies",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "KpRating",
                table: "Movies",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KpVotes",
                table: "Movies",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Movies",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImdbRating",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "KpRating",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "ImdbRating",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "ImdbVotes",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "KpRating",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "KpVotes",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Movies");
        }
    }
}
