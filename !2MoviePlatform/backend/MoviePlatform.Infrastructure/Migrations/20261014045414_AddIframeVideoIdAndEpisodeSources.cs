using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoviePlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIframeVideoIdAndEpisodeSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IframeVideoId",
                table: "Series",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ImdbVotes",
                table: "Series",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KpVotes",
                table: "Series",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Quality",
                table: "Series",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Duration",
                table: "Movies",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IframeVideoId",
                table: "Movies",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourcesJson",
                table: "Episodes",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IframeVideoId",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "ImdbVotes",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "KpVotes",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "Quality",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "Duration",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "IframeVideoId",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "SourcesJson",
                table: "Episodes");
        }
    }
}
