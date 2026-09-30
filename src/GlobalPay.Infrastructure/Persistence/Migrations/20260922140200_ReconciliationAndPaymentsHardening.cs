using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlobalPay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReconciliationAndPaymentsHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReconciliationCase",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentInstructionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<int>(type: "int", nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Owner = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ResolutionNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OpenedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", nullable: false),
                    ResolvedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReconciliationCase", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationCase_Instruction",
                table: "ReconciliationCase",
                column: "PaymentInstructionId");

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationCase_Tenant_OpenedAt",
                table: "ReconciliationCase",
                columns: new[] { "TenantId", "OpenedAtUtc" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReconciliationCase");
        }
    }
}
