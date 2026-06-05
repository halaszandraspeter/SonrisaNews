using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SonrisaNews.Infrastructure.Migrations
{
    /// <summary>
    /// Wave 6 — adds the <c>Alerts.Test.Own</c> permission, which
    /// guards the read-only "Test this alert" preview endpoint
    /// (<c>POST /api/v1/alerts/{id}/test</c>). Granted to <c>User</c>
    /// and <c>Admin</c> roles; the <c>System</c> role does not get it
    /// (the worker doesn't preview — it just matches).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The deterministic id is in
    /// <c>SonrisaNews.Domain.Auth.RolesCatalogSeed.AlertsTestOwn</c>
    /// so the rbac-audit tool and the auth flow reference the same
    /// key.
    /// </para>
    /// <para>
    /// Why a new permission instead of reusing <c>Alerts.Write.Own</c>:
    /// the preview is a read-only operation. Aliasing it to Write
    /// would couple "can I create an alert" to "can I test one",
    /// which a future "I want to test alerts but not create them"
    /// admin request would trip on.
    /// </para>
    /// </remarks>
    public partial class AddAlertsTestOwnPermission : Migration
    {
        // Forward-only after merge.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var seedTimestamp = new DateTimeOffset(2026, 6, 5, 16, 10, 0, TimeSpan.Zero);

            // -- Step 1: add the Alerts.Test.Own permission row --------
            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Name", "Description", "CreatedAt" },
                values: new object[,]
                {
                    { new Guid("40000000-0000-0000-0000-000000000011"), "Alerts.Test.Own", "Run the read-only 'Test this alert' preview against recent events", seedTimestamp },
                });

            // -- Step 2: grant to User + Admin roles --------------------
            // The preview is a User-facing read operation; System
            // (the worker) doesn't preview — it just matches. So
            // User + Admin, not System.
            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "RoleId", "PermissionId", "CreatedAt" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), new Guid("40000000-0000-0000-0000-000000000011"), seedTimestamp }, // User   -> Alerts.Test.Own
                    { new Guid("22222222-2222-2222-2222-222222222222"), new Guid("40000000-0000-0000-0000-000000000011"), seedTimestamp }, // Admin  -> Alerts.Test.Own
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse order of Up: drop the grants (FK to
            // Permissions) first, then the permission row. The
            // RolePermissions table has a FK to Permissions with
            // onDelete: Restrict, so we MUST remove the grants before
            // the permission row, or the migration throws.
            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "RoleId", "PermissionId" },
                keyValues: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), new Guid("40000000-0000-0000-0000-000000000011") },
                    { new Guid("22222222-2222-2222-2222-222222222222"), new Guid("40000000-0000-0000-0000-000000000011") },
                });

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000011"));
        }
    }
}
