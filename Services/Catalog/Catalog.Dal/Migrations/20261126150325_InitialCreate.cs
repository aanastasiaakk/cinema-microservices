using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Catalog.Dal.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Genres",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Slug = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Genres", x => x.Id);
                    table.CheckConstraint("CK_Genres_Slug", "[Slug] NOT LIKE '%[^a-z0-9-]%'");
                });

            migrationBuilder.CreateTable(
                name: "Halls",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Rows = table.Column<byte>(type: "tinyint", nullable: false),
                    SeatsPerRow = table.Column<byte>(type: "tinyint", nullable: false),
                    Format = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false, defaultValue: "2D"),
                    Capacity = table.Column<int>(type: "int", nullable: false, computedColumnSql: "CAST([Rows] AS INT) * [SeatsPerRow]", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Halls", x => x.Id);
                    table.CheckConstraint("CK_Halls_Format", "[Format] IN ('2D', '3D', 'IMAX', '4DX')");
                    table.CheckConstraint("CK_Halls_Rows", "[Rows] BETWEEN 1 AND 50");
                    table.CheckConstraint("CK_Halls_SeatsPerRow", "[SeatsPerRow] BETWEEN 1 AND 60");
                });

            migrationBuilder.CreateTable(
                name: "Movies",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReleaseYear = table.Column<short>(type: "smallint", nullable: false),
                    DurationMin = table.Column<short>(type: "smallint", nullable: false),
                    AgeRating = table.Column<string>(type: "varchar(5)", unicode: false, maxLength: 5, nullable: false, defaultValue: "0+"),
                    BasePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    AverageRating = table.Column<decimal>(type: "decimal(3,2)", nullable: true),
                    ReviewsCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Movies", x => x.Id);
                    table.CheckConstraint("CK_Movies_AgeRating", "[AgeRating] IN ('0+', '6+', '12+', '16+', '18+')");
                    table.CheckConstraint("CK_Movies_AverageRating", "[AverageRating] IS NULL OR [AverageRating] BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_Movies_BasePrice", "[BasePrice] > 0");
                    table.CheckConstraint("CK_Movies_DurationMin", "[DurationMin] BETWEEN 1 AND 600");
                    table.CheckConstraint("CK_Movies_ReleaseYear", "[ReleaseYear] BETWEEN 1895 AND 2100");
                    table.CheckConstraint("CK_Movies_ReviewsCount", "[ReviewsCount] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "MovieDetails",
                columns: table => new
                {
                    MovieId = table.Column<long>(type: "bigint", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Director = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Country = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TrailerUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PosterUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovieDetails", x => x.MovieId);
                    table.CheckConstraint("CK_MovieDetails_TrailerUrl", "[TrailerUrl] IS NULL OR [TrailerUrl] LIKE 'https://%'");
                    table.ForeignKey(
                        name: "FK_MovieDetails_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MovieGenres",
                columns: table => new
                {
                    MovieId = table.Column<long>(type: "bigint", nullable: false),
                    GenreId = table.Column<int>(type: "int", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovieGenres", x => new { x.MovieId, x.GenreId });
                    table.ForeignKey(
                        name: "FK_MovieGenres_Genres_GenreId",
                        column: x => x.GenreId,
                        principalTable: "Genres",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovieGenres_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Showtimes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MovieId = table.Column<long>(type: "bigint", nullable: false),
                    HallId = table.Column<int>(type: "int", nullable: false),
                    StartsAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Language = table.Column<string>(type: "char(2)", unicode: false, fixedLength: true, maxLength: 2, nullable: false, defaultValue: "uk"),
                    IsCancelled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Showtimes", x => x.Id);
                    table.CheckConstraint("CK_Showtimes_Language", "[Language] IN ('uk', 'en')");
                    table.CheckConstraint("CK_Showtimes_Price", "[Price] > 0");
                    table.ForeignKey(
                        name: "FK_Showtimes_Halls_HallId",
                        column: x => x.HallId,
                        principalTable: "Halls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Showtimes_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Genres",
                columns: new[] { "Id", "Name", "Slug" },
                values: new object[,]
                {
                    { 1, "Драма", "drama" },
                    { 2, "Пригоди", "adventure" },
                    { 3, "Анімація", "animation" },
                    { 4, "Фантастика", "sci-fi" },
                    { 5, "Історичний", "historical" },
                    { 6, "Сімейний", "family" }
                });

            migrationBuilder.InsertData(
                table: "Halls",
                columns: new[] { "Id", "Format", "Name", "Rows", "SeatsPerRow" },
                values: new object[,]
                {
                    { 1, "IMAX", "Зал 1 (IMAX)", (byte)10, (byte)12 },
                    { 2, "2D", "Зал 2", (byte)8, (byte)10 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Genres_Name",
                table: "Genres",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Genres_Slug",
                table: "Genres",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Halls_Name",
                table: "Halls",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovieGenres_GenreId",
                table: "MovieGenres",
                column: "GenreId")
                .Annotation("SqlServer:Include", new[] { "AddedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MovieGenres_MovieId_Primary",
                table: "MovieGenres",
                column: "MovieId",
                unique: true,
                filter: "[IsPrimary] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Movies_Active_Rating",
                table: "Movies",
                column: "AverageRating",
                descending: new bool[0],
                filter: "[IsActive] = 1")
                .Annotation("SqlServer:Include", new[] { "Title", "AgeRating", "BasePrice" });

            migrationBuilder.CreateIndex(
                name: "IX_Movies_Title_ReleaseYear",
                table: "Movies",
                columns: new[] { "Title", "ReleaseYear" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Showtimes_HallId_StartsAt",
                table: "Showtimes",
                columns: new[] { "HallId", "StartsAt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Showtimes_MovieId_StartsAt",
                table: "Showtimes",
                columns: new[] { "MovieId", "StartsAt" },
                filter: "[IsCancelled] = 0")
                .Annotation("SqlServer:Include", new[] { "HallId", "Price" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MovieDetails");

            migrationBuilder.DropTable(
                name: "MovieGenres");

            migrationBuilder.DropTable(
                name: "Showtimes");

            migrationBuilder.DropTable(
                name: "Genres");

            migrationBuilder.DropTable(
                name: "Halls");

            migrationBuilder.DropTable(
                name: "Movies");
        }
    }
}
