using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Energieonderbrekingen.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false),
                    PostalCode = table.Column<string>(type: "TEXT", maxLength: 6, nullable: false),
                    UnsubscribeToken = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SubscriptionId = table.Column<int>(type: "INTEGER", nullable: false),
                    DisruptionId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationLogs_Subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "Subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_SubscriptionId_DisruptionId_Kind",
                table: "NotificationLogs",
                columns: new[] { "SubscriptionId", "DisruptionId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_Email_PostalCode",
                table: "Subscriptions",
                columns: new[] { "Email", "PostalCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_UnsubscribeToken",
                table: "Subscriptions",
                column: "UnsubscribeToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationLogs");

            migrationBuilder.DropTable(
                name: "Subscriptions");
        }
    }
}
