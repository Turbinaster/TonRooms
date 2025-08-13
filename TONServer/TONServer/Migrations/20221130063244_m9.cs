using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TONServer.Migrations
{
    public partial class m9 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Friends",
                table: "RoomWebs",
                type: "nvarchar(max)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Friends",
                table: "RoomWebs");
        }
    }
}
