using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITCoursesBot.Migrations
{
    /// <inheritdoc />
    public partial class AddUserCourseModels4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Name",
                table: "Lessons");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Lessons",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
