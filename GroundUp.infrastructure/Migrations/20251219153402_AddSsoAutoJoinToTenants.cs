using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroundUp.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSsoAutoJoinToTenants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SsoAutoJoinDomains",
                table: "Tenants",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "SsoAutoJoinRoleId",
                table: "Tenants",
                type: "int",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "InventoryCategories",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2025, 12, 19, 15, 34, 0, 253, DateTimeKind.Utc).AddTicks(4098));

            migrationBuilder.UpdateData(
                table: "InventoryCategories",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2025, 12, 19, 15, 34, 0, 253, DateTimeKind.Utc).AddTicks(4101));

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 1,
                column: "PurchaseDate",
                value: new DateTime(2025, 12, 19, 15, 34, 0, 253, DateTimeKind.Utc).AddTicks(4415));

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 2,
                column: "PurchaseDate",
                value: new DateTime(2025, 12, 19, 15, 34, 0, 253, DateTimeKind.Utc).AddTicks(4417));

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_SsoAutoJoinRoleId",
                table: "Tenants",
                column: "SsoAutoJoinRoleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tenants_Roles_SsoAutoJoinRoleId",
                table: "Tenants",
                column: "SsoAutoJoinRoleId",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tenants_Roles_SsoAutoJoinRoleId",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_SsoAutoJoinRoleId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "SsoAutoJoinDomains",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "SsoAutoJoinRoleId",
                table: "Tenants");

            migrationBuilder.UpdateData(
                table: "InventoryCategories",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2025, 12, 16, 3, 48, 51, 555, DateTimeKind.Utc).AddTicks(351));

            migrationBuilder.UpdateData(
                table: "InventoryCategories",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2025, 12, 16, 3, 48, 51, 555, DateTimeKind.Utc).AddTicks(353));

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 1,
                column: "PurchaseDate",
                value: new DateTime(2025, 12, 16, 3, 48, 51, 555, DateTimeKind.Utc).AddTicks(477));

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 2,
                column: "PurchaseDate",
                value: new DateTime(2025, 12, 16, 3, 48, 51, 555, DateTimeKind.Utc).AddTicks(480));
        }
    }
}
