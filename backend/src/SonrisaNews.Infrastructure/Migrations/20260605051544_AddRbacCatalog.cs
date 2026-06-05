using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SonrisaNews.Infrastructure.Migrations
{
    /// <summary>
    /// Wave 3 — replaces the <c>User.Role</c> column with the full RBAC
    /// catalog (5 tables: <c>Users</c>, <c>Roles</c>, <c>Permissions</c>,
    /// <c>UserRoles</c>, <c>RolePermissions</c>). Validation is now by
    /// permission, not by role (user rule 2026-06-05). The catalog is
    /// seeded with the 3 baseline roles and the 16 MVP permissions;
    /// the role↔permission grants are the matrix from
    /// <c>docs/roadmap/1-features.md</c>.
    /// </summary>
    /// <remarks>
    /// The deterministic Guids for the seeded rows live in
    /// <c>SonrisaNews.Domain.Auth.RolesCatalogSeed</c> so the auth flow
    /// and the migration reference the same row keys.
    /// </remarks>
    public partial class AddRbacCatalog : Migration
    {
        // Forward-only after merge.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // -- Step 1: drop the old User.Role column -------------------------
            // The UserRole enum is gone; the role is now a row in Roles
            // joined via UserRoles. Any data in the column is lost — there
            // are no production users yet (wave 3 is the first wave with
            // real users).
            migrationBuilder.DropColumn(
                name: "Role",
                table: "Users");

            // The User entity gains a MustChangePassword flag in the same
            // wave. The scaffold already added it; this is here for
            // documentation so the reader sees the full surface change.
            migrationBuilder.AddColumn<bool>(
                name: "MustChangePassword",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            // -- Step 2: create the 4 new RBAC tables -------------------------
            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PermissionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => new { x.RoleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_RolePermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_Permissions_Name",
                table: "Permissions",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionId",
                table: "RolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "UX_Roles_Name",
                table: "Roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                table: "UserRoles",
                column: "RoleId");

            // -- Step 3: seed the catalog -------------------------------------
            // 3 roles + 16 permissions + ~30 grants. The Ids are
            // deterministic (see RolesCatalogSeed) so the auth flow and
            // the audit tool reference the same keys.
            var seedTimestamp = new DateTimeOffset(2026, 6, 5, 0, 0, 0, TimeSpan.Zero);

            // Roles
            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "Name", "DisplayName", "Description", "CreatedAt" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), "User",   "User",   "End-user",                                                seedTimestamp },
                    { new Guid("22222222-2222-2222-2222-222222222222"), "Admin",  "Admin",  "Operator (administrates the platform)",                  seedTimestamp },
                    { new Guid("33333333-3333-3333-3333-333333333333"), "System", "System", "Background worker (matcher, dispatcher, poller, etc.)", seedTimestamp },
                });

            // Permissions (id, name, description)
            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Name", "Description", "CreatedAt" },
                values: new object[,]
                {
                    { new Guid("40000000-0000-0000-0000-000000000001"), "Alerts.Read.Own",        "Read own alerts",                       seedTimestamp },
                    { new Guid("40000000-0000-0000-0000-000000000002"), "Alerts.Write.Own",       "Create/update/delete own alerts",       seedTimestamp },
                    { new Guid("40000000-0000-0000-0000-000000000003"), "Alerts.Read.Any",        "Read any user's alerts (admin/audit)",  seedTimestamp },
                    { new Guid("40000000-0000-0000-0000-000000000004"), "Alerts.Write.Any",       "Moderate any user's alerts",            seedTimestamp },
                    { new Guid("40000000-0000-0000-0000-000000000005"), "Channels.Read.Own",      "Read own channels (email/Slack)",       seedTimestamp },
                    { new Guid("40000000-0000-0000-0000-000000000006"), "Channels.Write.Own",     "Add/remove/verify own channels",        seedTimestamp },
                    { new Guid("40000000-0000-0000-0000-000000000007"), "Sources.Read.Any",       "List data sources and their health",    seedTimestamp },
                    { new Guid("40000000-0000-0000-0000-000000000008"), "Sources.Write.Any",      "Add/remove/pause data sources",         seedTimestamp },
                    { new Guid("40000000-0000-0000-0000-000000000009"), "Users.Read.Any",         "List users (admin/audit)",              seedTimestamp },
                    { new Guid("40000000-0000-0000-0000-00000000000a"), "Users.Write.Any",        "Edit any user (admin/audit)",           seedTimestamp },
                    { new Guid("40000000-0000-0000-0000-00000000000b"), "Users.Suspend",          "Suspend a user (admin/audit)",          seedTimestamp },
                    { new Guid("40000000-0000-0000-0000-00000000000c"), "AuditLog.Read",          "Read the audit log (admin/audit)",      seedTimestamp },
                    { new Guid("40000000-0000-0000-0000-00000000000d"), "Announcements.Write",    "Post platform-wide announcements",      seedTimestamp },
                    { new Guid("40000000-0000-0000-0000-00000000000e"), "Health.Read",           "Read the platform health status",       seedTimestamp },
                    { new Guid("40000000-0000-0000-0000-00000000000f"), "Matcher.Run",            "Run the alert matcher (worker)",        seedTimestamp },
                    { new Guid("40000000-0000-0000-0000-000000000010"), "Profile.Read",           "Read the current user's profile",      seedTimestamp },
                });

            // Role → Permission grants per docs/roadmap/1-features.md.
            // User   : Alerts.Read.Own, Alerts.Write.Own, Channels.Read.Own,
            //          Channels.Write.Own, Profile.Read
            // Admin  : User.* + Alerts.Read.Any, Alerts.Write.Any, Sources.*,
            //          Users.*, AuditLog.Read, Announcements.Write, Health.Read
            // System : Matcher.Run, Alerts.Read.Any, Alerts.Write.Any,
            //          Sources.Read.Any, Profile.Read
            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "RoleId", "PermissionId", "CreatedAt" },
                values: new object[,]
                {
                    // User
                    { new Guid("11111111-1111-1111-1111-111111111111"), new Guid("40000000-0000-0000-0000-000000000001"), seedTimestamp },
                    { new Guid("11111111-1111-1111-1111-111111111111"), new Guid("40000000-0000-0000-0000-000000000002"), seedTimestamp },
                    { new Guid("11111111-1111-1111-1111-111111111111"), new Guid("40000000-0000-0000-0000-000000000005"), seedTimestamp },
                    { new Guid("11111111-1111-1111-1111-111111111111"), new Guid("40000000-0000-0000-0000-000000000006"), seedTimestamp },
                    { new Guid("11111111-1111-1111-1111-111111111111"), new Guid("40000000-0000-0000-0000-000000000010"), seedTimestamp },

                    // Admin (operator)
                    { new Guid("22222222-2222-2222-2222-222222222222"), new Guid("40000000-0000-0000-0000-000000000001"), seedTimestamp },
                    { new Guid("22222222-2222-2222-2222-222222222222"), new Guid("40000000-0000-0000-0000-000000000002"), seedTimestamp },
                    { new Guid("22222222-2222-2222-2222-222222222222"), new Guid("40000000-0000-0000-0000-000000000003"), seedTimestamp },
                    { new Guid("22222222-2222-2222-2222-222222222222"), new Guid("40000000-0000-0000-0000-000000000004"), seedTimestamp },
                    { new Guid("22222222-2222-2222-2222-222222222222"), new Guid("40000000-0000-0000-0000-000000000005"), seedTimestamp },
                    { new Guid("22222222-2222-2222-2222-222222222222"), new Guid("40000000-0000-0000-0000-000000000006"), seedTimestamp },
                    { new Guid("22222222-2222-2222-2222-222222222222"), new Guid("40000000-0000-0000-0000-000000000007"), seedTimestamp },
                    { new Guid("22222222-2222-2222-2222-222222222222"), new Guid("40000000-0000-0000-0000-000000000008"), seedTimestamp },
                    { new Guid("22222222-2222-2222-2222-222222222222"), new Guid("40000000-0000-0000-0000-000000000009"), seedTimestamp },
                    { new Guid("22222222-2222-2222-2222-222222222222"), new Guid("40000000-0000-0000-0000-00000000000a"), seedTimestamp },
                    { new Guid("22222222-2222-2222-2222-222222222222"), new Guid("40000000-0000-0000-0000-00000000000b"), seedTimestamp },
                    { new Guid("22222222-2222-2222-2222-222222222222"), new Guid("40000000-0000-0000-0000-00000000000c"), seedTimestamp },
                    { new Guid("22222222-2222-2222-2222-222222222222"), new Guid("40000000-0000-0000-0000-00000000000d"), seedTimestamp },
                    { new Guid("22222222-2222-2222-2222-222222222222"), new Guid("40000000-0000-0000-0000-00000000000e"), seedTimestamp },
                    { new Guid("22222222-2222-2222-2222-222222222222"), new Guid("40000000-0000-0000-0000-000000000010"), seedTimestamp },

                    // System (background worker)
                    { new Guid("33333333-3333-3333-3333-333333333333"), new Guid("40000000-0000-0000-0000-00000000000f"), seedTimestamp },
                    { new Guid("33333333-3333-3333-3333-333333333333"), new Guid("40000000-0000-0000-0000-000000000003"), seedTimestamp },
                    { new Guid("33333333-3333-3333-3333-333333333333"), new Guid("40000000-0000-0000-0000-000000000004"), seedTimestamp },
                    { new Guid("33333333-3333-3333-3333-333333333333"), new Guid("40000000-0000-0000-0000-000000000007"), seedTimestamp },
                    { new Guid("33333333-3333-3333-3333-333333333333"), new Guid("40000000-0000-0000-0000-000000000010"), seedTimestamp },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The Down restores the schema to its pre-wave-3 shape. The
            // data in the catalog is lost; any role assignments on real
            // users are lost. Acceptable because wave 3 is the first
            // wave with real users — there is nothing to preserve. The
            // 26 seeded RolePermissions rows (5 User + 16 Admin +
            // 5 System) are purged implicitly when the table is dropped
            // below.
            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropColumn(
                name: "MustChangePassword",
                table: "Users");

            migrationBuilder.AddColumn<int>(
                name: "Role",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }
    }
}
