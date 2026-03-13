using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elevator.Infrastructure.Migration;

public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder mb)
    {
        mb.CreateTable(
            name: "DownloadJobs",
            columns: t => new
            {
                Id              = t.Column<Guid>(nullable: false),
                FileName        = t.Column<string>(maxLength: 500,  nullable: false),
                FileUrl         = t.Column<string>(maxLength: 2000, nullable: false),
                ContentType     = t.Column<string>(maxLength: 200,  nullable: false, defaultValue: "application/octet-stream"),
                FileSizeBytes   = t.Column<long>(nullable: true),
                DownloadType    = t.Column<string>(maxLength: 30,   nullable: false),
                FileFormat      = t.Column<string>(maxLength: 30,   nullable: false),
                StorageProvider = t.Column<string>(maxLength: 30,   nullable: false),
                Status          = t.Column<string>(maxLength: 30,   nullable: false),
                ProgressPercent = t.Column<int>(nullable: false, defaultValue: 0),
                BytesDownloaded = t.Column<long>(nullable: false, defaultValue: 0L),
                ErrorMessage    = t.Column<string>(maxLength: 2000, nullable: true),
                LocalPath       = t.Column<string>(maxLength: 1000, nullable: true),
                RequestedBy     = t.Column<string>(maxLength: 200,  nullable: true),
                Metadata        = t.Column<string>(maxLength: 4000, nullable: false, defaultValue: "{}"),
                CreatedAt       = t.Column<DateTime>(nullable: false),
                StartedAt       = t.Column<DateTime>(nullable: true),
                CompletedAt     = t.Column<DateTime>(nullable: true),
                UpdatedAt       = t.Column<DateTime>(nullable: true)
            },
            constraints: t => t.PrimaryKey("PK_DownloadJobs", x => x.Id));

        mb.CreateIndex("IX_DownloadJobs_Status",      "DownloadJobs", "Status");
        mb.CreateIndex("IX_DownloadJobs_CreatedAt",   "DownloadJobs", "CreatedAt");
        mb.CreateIndex("IX_DownloadJobs_RequestedBy", "DownloadJobs", "RequestedBy");

        mb.CreateTable(
            name: "DownloadAuditLogs",
            columns: t => new
            {
                Id        = t.Column<int>(nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                JobId     = t.Column<Guid>(nullable: false),
                OldStatus = t.Column<string>(maxLength: 30, nullable: false),
                NewStatus = t.Column<string>(maxLength: 30, nullable: false),
                Message   = t.Column<string>(maxLength: 1000, nullable: true),
                CreatedAt = t.Column<DateTime>(nullable: false)
            },
            constraints: t =>
            {
                t.PrimaryKey("PK_DownloadAuditLogs", x => x.Id);
                t.ForeignKey("FK_DownloadAuditLogs_DownloadJobs_JobId",
                    x => x.JobId, "DownloadJobs", "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        mb.CreateIndex("IX_DownloadAuditLogs_JobId", "DownloadAuditLogs", "JobId");
    }

    protected override void Down(MigrationBuilder mb)
    {
        mb.DropTable("DownloadAuditLogs");
        mb.DropTable("DownloadJobs");
    }
}
