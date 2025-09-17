using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DvSqlGenWeb.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Chunks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    CardType = table.Column<string>(type: "TEXT", nullable: true),
                    SectionAlias = table.Column<string>(type: "TEXT", nullable: true),
                    SectionId = table.Column<string>(type: "TEXT", nullable: true),
                    Field = table.Column<string>(type: "TEXT", nullable: true),
                    Synonyms = table.Column<string>(type: "TEXT", nullable: true),
                    Embedding = table.Column<byte[]>(type: "BLOB", nullable: false),
                    Dim = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Chunks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Chunks_CardType_SectionAlias",
                table: "Chunks",
                columns: new[] { "CardType", "SectionAlias" });

            migrationBuilder.CreateIndex(
                name: "IX_Chunks_Field",
                table: "Chunks",
                column: "Field");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Chunks");
        }
    }
}
