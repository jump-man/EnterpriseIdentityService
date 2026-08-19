using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseIdentityService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSecurityAuditTrail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditEntries",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    OccurredAtUtcTicks = table.Column<long>(type: "bigint", nullable: false),
                    SortId = table.Column<string>(type: "char(32)", unicode: false, fixedLength: true, maxLength: 32, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TargetUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Permission = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEntries", x => x.Id);
                });

            migrationBuilder.InsertData(
                schema: "identity",
                table: "RolePermissions",
                columns: new[] { "Permission", "RoleId" },
                values: new object[] { "audit.read", new Guid("7d8f6e36-72a1-4f91-9b0f-8bf83ed7247c") });

            migrationBuilder.Sql(
                """
                UPDATE [identity].[Users]
                SET [AuthorizationVersion] = [AuthorizationVersion] + 1
                WHERE EXISTS (
                    SELECT 1
                    FROM [identity].[UserRoles]
                    WHERE [identity].[UserRoles].[UserId] = [identity].[Users].[Id]
                      AND [identity].[UserRoles].[RoleId] = '7d8f6e36-72a1-4f91-9b0f-8bf83ed7247c');
                """);

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_ActorUserId_OccurredAtUtcTicks_SortId",
                schema: "identity",
                table: "AuditEntries",
                columns: new[] { "ActorUserId", "OccurredAtUtcTicks", "SortId" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_CorrelationId",
                schema: "identity",
                table: "AuditEntries",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_EventType_OccurredAtUtcTicks_SortId",
                schema: "identity",
                table: "AuditEntries",
                columns: new[] { "EventType", "OccurredAtUtcTicks", "SortId" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_OccurredAtUtcTicks_SortId",
                schema: "identity",
                table: "AuditEntries",
                columns: new[] { "OccurredAtUtcTicks", "SortId" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_RoleId_OccurredAtUtcTicks_SortId",
                schema: "identity",
                table: "AuditEntries",
                columns: new[] { "RoleId", "OccurredAtUtcTicks", "SortId" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_SessionId_OccurredAtUtcTicks_SortId",
                schema: "identity",
                table: "AuditEntries",
                columns: new[] { "SessionId", "OccurredAtUtcTicks", "SortId" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_TargetUserId_OccurredAtUtcTicks_SortId",
                schema: "identity",
                table: "AuditEntries",
                columns: new[] { "TargetUserId", "OccurredAtUtcTicks", "SortId" },
                descending: new[] { false, true, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEntries",
                schema: "identity");

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RolePermissions",
                keyColumns: new[] { "Permission", "RoleId" },
                keyValues: new object[] { "audit.read", new Guid("7d8f6e36-72a1-4f91-9b0f-8bf83ed7247c") });

            migrationBuilder.Sql(
                """
                UPDATE [identity].[Users]
                SET [AuthorizationVersion] = [AuthorizationVersion] + 1
                WHERE EXISTS (
                    SELECT 1
                    FROM [identity].[UserRoles]
                    WHERE [identity].[UserRoles].[UserId] = [identity].[Users].[Id]
                      AND [identity].[UserRoles].[RoleId] = '7d8f6e36-72a1-4f91-9b0f-8bf83ed7247c');
                """);
        }
    }
}
