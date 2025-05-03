using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITCoursesBot.Migrations
{
    /// <inheritdoc />
    public partial class AddUserCourseModels2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Number",
                table: "Lessons",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Number",
                table: "Lessons");
        }
    }
}
