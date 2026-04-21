using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PROG7313_TechMove.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSeedData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Clients",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "ContactDetails", "Name" },
                values: new object[] { "contact@transafrica.co.za | +27 31 765 1122", "TransAfrica Logistics" });

            migrationBuilder.UpdateData(
                table: "Clients",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "ContactDetails", "Name" },
                values: new object[] { "info@nordicfreight.se | +46 8 442 7788", "Nordic Freight Solutions" });

            migrationBuilder.UpdateData(
                table: "Clients",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "ContactDetails", "Name" },
                values: new object[] { "support@atlanticcargo.com | +1 212 555 9812", "Atlantic Cargo Lines" });

            migrationBuilder.UpdateData(
                table: "Contracts",
                keyColumn: "Id",
                keyValue: 1,
                column: "ServiceLevel",
                value: "Enterprise Supply Chain");

            migrationBuilder.UpdateData(
                table: "Contracts",
                keyColumn: "Id",
                keyValue: 2,
                column: "ServiceLevel",
                value: "Standard Freight");

            migrationBuilder.UpdateData(
                table: "Contracts",
                keyColumn: "Id",
                keyValue: 3,
                column: "ServiceLevel",
                value: "Express Delivery");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Clients",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "ContactDetails", "Name" },
                values: new object[] { "info@globalfreight.com | +27 31 000 0001", "Global Freight Ltd" });

            migrationBuilder.UpdateData(
                table: "Clients",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "ContactDetails", "Name" },
                values: new object[] { "contact@eurocargo.de | +49 30 000 0002", "Euro Cargo GmbH" });

            migrationBuilder.UpdateData(
                table: "Clients",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "ContactDetails", "Name" },
                values: new object[] { "ops@pacshipping.com | +1 310 000 0003", "Pacific Shipping Co" });

            migrationBuilder.UpdateData(
                table: "Contracts",
                keyColumn: "Id",
                keyValue: 1,
                column: "ServiceLevel",
                value: "Gold");

            migrationBuilder.UpdateData(
                table: "Contracts",
                keyColumn: "Id",
                keyValue: 2,
                column: "ServiceLevel",
                value: "Silver");

            migrationBuilder.UpdateData(
                table: "Contracts",
                keyColumn: "Id",
                keyValue: 3,
                column: "ServiceLevel",
                value: "Bronze");
        }
    }
}
