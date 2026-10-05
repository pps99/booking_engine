using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rezerv.BookingEngine.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "businesses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_businesses", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "customers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Email = table.Column<string>(type: "varchar(254)", maxLength: 254, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customers", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "package_definitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    BusinessId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TotalCredits = table.Column<int>(type: "int", nullable: false),
                    ValidityDays = table.Column<int>(type: "int", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_package_definitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_package_definitions_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "timetable_schedules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    BusinessId = table.Column<int>(type: "int", nullable: false),
                    ClassName = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    InstructorName = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StartTimeUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    EndTimeUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<uint>(type: "int unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_timetable_schedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_timetable_schedules_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "customer_packages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    PackageDefinitionId = table.Column<int>(type: "int", nullable: false),
                    BusinessId = table.Column<int>(type: "int", nullable: false),
                    TotalCredits = table.Column<int>(type: "int", nullable: false),
                    RemainingCredits = table.Column<int>(type: "int", nullable: false),
                    PurchasedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    Version = table.Column<uint>(type: "int unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_packages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_customer_packages_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_customer_packages_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_customer_packages_package_definitions_PackageDefinitionId",
                        column: x => x.PackageDefinitionId,
                        principalTable: "package_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "bookings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    ScheduleId = table.Column<long>(type: "bigint", nullable: false),
                    CustomerPackageId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    BookedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    CancelledAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    CreditRefunded = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    PromotedFromWaitlistEntryId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_bookings_customer_packages_CustomerPackageId",
                        column: x => x.CustomerPackageId,
                        principalTable: "customer_packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_bookings_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_bookings_timetable_schedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "timetable_schedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "waitlist_entries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    ScheduleId = table.Column<long>(type: "bigint", nullable: false),
                    PreferredCustomerPackageId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    JoinedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    ResolvedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    PromotedBookingId = table.Column<long>(type: "bigint", nullable: true),
                    ResolutionReason = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_waitlist_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_waitlist_entries_customer_packages_PreferredCustomerPackageId",
                        column: x => x.PreferredCustomerPackageId,
                        principalTable: "customer_packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_waitlist_entries_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_waitlist_entries_timetable_schedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "timetable_schedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_CustomerId_Status",
                table: "bookings",
                columns: new[] { "CustomerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_bookings_CustomerPackageId",
                table: "bookings",
                column: "CustomerPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_PromotedFromWaitlistEntryId",
                table: "bookings",
                column: "PromotedFromWaitlistEntryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bookings_ScheduleId_Status",
                table: "bookings",
                columns: new[] { "ScheduleId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_customer_packages_BusinessId",
                table: "customer_packages",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_customer_packages_CustomerId_BusinessId_ExpiresAtUtc",
                table: "customer_packages",
                columns: new[] { "CustomerId", "BusinessId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_customer_packages_PackageDefinitionId",
                table: "customer_packages",
                column: "PackageDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_customers_Email",
                table: "customers",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_package_definitions_BusinessId_IsActive",
                table: "package_definitions",
                columns: new[] { "BusinessId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_timetable_schedules_BusinessId_StartTimeUtc",
                table: "timetable_schedules",
                columns: new[] { "BusinessId", "StartTimeUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_waitlist_entries_CustomerId_ScheduleId_Status",
                table: "waitlist_entries",
                columns: new[] { "CustomerId", "ScheduleId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_waitlist_entries_PreferredCustomerPackageId",
                table: "waitlist_entries",
                column: "PreferredCustomerPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_waitlist_entries_ScheduleId_Status_JoinedAtUtc",
                table: "waitlist_entries",
                columns: new[] { "ScheduleId", "Status", "JoinedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bookings");

            migrationBuilder.DropTable(
                name: "waitlist_entries");

            migrationBuilder.DropTable(
                name: "customer_packages");

            migrationBuilder.DropTable(
                name: "timetable_schedules");

            migrationBuilder.DropTable(
                name: "customers");

            migrationBuilder.DropTable(
                name: "package_definitions");

            migrationBuilder.DropTable(
                name: "businesses");
        }
    }
}
