using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITCoursesBot.Migrations
{
    /// <inheritdoc />
    public partial class AddUserCourseModels3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Number",
                table: "Lessons",
                newName: "Title");

            migrationBuilder.AddColumn<int>(
                name: "Major",
                table: "Lessons",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Minor",
                table: "Lessons",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Major",
                table: "Lessons");

            migrationBuilder.DropColumn(
                name: "Minor",
                table: "Lessons");

            migrationBuilder.RenameColumn(
                name: "Title",
                table: "Lessons",
                newName: "Number");
        }
    }
}
