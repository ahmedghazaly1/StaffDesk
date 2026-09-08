using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StaffDesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StoreTimesheetAttachmentsInDatabase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Attachments recorded before this migration kept their bytes on the container
            // filesystem, which does not survive a redeploy. Those rows can only produce a broken
            // download, so they are removed rather than left pointing at files that no longer exist.
            migrationBuilder.Sql(@"DELETE FROM ""TimesheetAttachments"";");

            migrationBuilder.DropColumn(
                name: "StoragePath",
                table: "TimesheetAttachments");

            migrationBuilder.CreateTable(
                name: "TimesheetAttachmentContents",
                columns: table => new
                {
                    TimesheetAttachmentId = table.Column<int>(type: "integer", nullable: false),
                    Bytes = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimesheetAttachmentContents", x => x.TimesheetAttachmentId);
                    table.ForeignKey(
                        name: "FK_TimesheetAttachmentContents_TimesheetAttachments_TimesheetA~",
                        column: x => x.TimesheetAttachmentId,
                        principalTable: "TimesheetAttachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TimesheetAttachmentContents");

            migrationBuilder.AddColumn<string>(
                name: "StoragePath",
                table: "TimesheetAttachments",
                type: "character varying(400)",
                maxLength: 400,
                nullable: false,
                defaultValue: "");
        }
    }
}
