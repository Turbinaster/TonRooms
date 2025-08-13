using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TONServer.Migrations
{
    public partial class m10 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Incomings",
                table: "RoomWebs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Outcomings",
                table: "RoomWebs",
                type: "nvarchar(max)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Incomings",
                table: "RoomWebs");

            migrationBuilder.DropColumn(
                name: "Outcomings",
                table: "RoomWebs");
        }
    }
}
