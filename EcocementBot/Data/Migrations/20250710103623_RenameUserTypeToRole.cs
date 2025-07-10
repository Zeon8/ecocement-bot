using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcocementBot.Migrations
{
    /// <inheritdoc />
    public partial class RenameUserTypeToRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UserType",
                table: "Users",
                newName: "Role");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Role",
                table: "Users",
                newName: "UserType");
        }
    }
}
