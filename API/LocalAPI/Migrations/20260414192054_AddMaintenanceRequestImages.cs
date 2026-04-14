using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MulhollandRealEstate.API.Migrations
{
    /// <inheritdoc />
    public partial class AddMaintenanceRequestImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_MaintenanceRequests",
                table: "MaintenanceRequests");

            migrationBuilder.RenameTable(
                name: "MaintenanceRequests",
                newName: "maintenance_requests");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "maintenance_requests",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UnitNumber",
                table: "maintenance_requests",
                newName: "unit_number");

            migrationBuilder.RenameColumn(
                name: "TriageSource",
                table: "maintenance_requests",
                newName: "triage_source");

            migrationBuilder.RenameColumn(
                name: "TenantTenureMonths",
                table: "maintenance_requests",
                newName: "tenant_tenure_months");

            migrationBuilder.RenameColumn(
                name: "TagsJson",
                table: "maintenance_requests",
                newName: "tags_json");

            migrationBuilder.RenameColumn(
                name: "SubmissionChannel",
                table: "maintenance_requests",
                newName: "submission_channel");

            migrationBuilder.RenameColumn(
                name: "RiskNotes",
                table: "maintenance_requests",
                newName: "risk_notes");

            migrationBuilder.RenameColumn(
                name: "RequestTimestamp",
                table: "maintenance_requests",
                newName: "request_timestamp");

            migrationBuilder.RenameColumn(
                name: "RequestText",
                table: "maintenance_requests",
                newName: "request_text");

            migrationBuilder.RenameColumn(
                name: "RequestNumber",
                table: "maintenance_requests",
                newName: "request_number");

            migrationBuilder.RenameColumn(
                name: "PropertyId",
                table: "maintenance_requests",
                newName: "property_id");

            migrationBuilder.RenameColumn(
                name: "PriorRequestsLast6Mo",
                table: "maintenance_requests",
                newName: "prior_requests_last_6mo");

            migrationBuilder.RenameColumn(
                name: "PredictedUrgency",
                table: "maintenance_requests",
                newName: "predicted_urgency");

            migrationBuilder.RenameColumn(
                name: "PredictedCategory",
                table: "maintenance_requests",
                newName: "predicted_category");

            migrationBuilder.RenameColumn(
                name: "NeedsHumanReview",
                table: "maintenance_requests",
                newName: "needs_human_review");

            migrationBuilder.RenameColumn(
                name: "LastTriagedAt",
                table: "maintenance_requests",
                newName: "last_triaged_at");

            migrationBuilder.RenameColumn(
                name: "ImageUrlOrCount",
                table: "maintenance_requests",
                newName: "image_url_or_count");

            migrationBuilder.RenameColumn(
                name: "ImageType",
                table: "maintenance_requests",
                newName: "image_type");

            migrationBuilder.RenameColumn(
                name: "ImageSeverityHint",
                table: "maintenance_requests",
                newName: "image_severity_hint");

            migrationBuilder.RenameColumn(
                name: "HasImage",
                table: "maintenance_requests",
                newName: "has_image");

            migrationBuilder.RenameColumn(
                name: "ConfidenceScore",
                table: "maintenance_requests",
                newName: "confidence_score");

            migrationBuilder.RenameColumn(
                name: "BuildingType",
                table: "maintenance_requests",
                newName: "building_type");

            migrationBuilder.RenameColumn(
                name: "ActualUrgency",
                table: "maintenance_requests",
                newName: "actual_urgency");

            migrationBuilder.RenameColumn(
                name: "ActualCategory",
                table: "maintenance_requests",
                newName: "actual_category");

            migrationBuilder.RenameIndex(
                name: "IX_MaintenanceRequests_RequestNumber",
                table: "maintenance_requests",
                newName: "uq_maintenance_requests_request_number");

            migrationBuilder.AddColumn<DateTime>(
                name: "assigned_at",
                table: "maintenance_requests",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "assigned_employee_id",
                table: "maintenance_requests",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "assignment_source",
                table: "maintenance_requests",
                type: "varchar(32)",
                maxLength: 32,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "closed_at",
                table: "maintenance_requests",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "closed_by",
                table: "maintenance_requests",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "resolution_notes",
                table: "maintenance_requests",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "maintenance_requests",
                type: "varchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddPrimaryKey(
                name: "PK_maintenance_requests",
                table: "maintenance_requests",
                column: "id");

            migrationBuilder.CreateTable(
                name: "employees",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    full_name = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    role = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    phone = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    email = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    home_property_id = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    max_open_tickets = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employees", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "maintenance_request_events",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    maintenance_request_id = table.Column<long>(type: "bigint", nullable: false),
                    event_type = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    event_timestamp = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    actor = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    details_json = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_maintenance_request_events", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "maintenance_request_images",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    maintenance_request_id = table.Column<long>(type: "bigint", nullable: false),
                    storage_key = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    file_name = table.Column<string>(type: "varchar(260)", maxLength: 260, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    content_type = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_maintenance_request_images", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_mri_request",
                table: "maintenance_request_images",
                column: "maintenance_request_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "employees");

            migrationBuilder.DropTable(
                name: "maintenance_request_events");

            migrationBuilder.DropTable(
                name: "maintenance_request_images");

            migrationBuilder.DropPrimaryKey(
                name: "PK_maintenance_requests",
                table: "maintenance_requests");

            migrationBuilder.DropColumn(
                name: "assigned_at",
                table: "maintenance_requests");

            migrationBuilder.DropColumn(
                name: "assigned_employee_id",
                table: "maintenance_requests");

            migrationBuilder.DropColumn(
                name: "assignment_source",
                table: "maintenance_requests");

            migrationBuilder.DropColumn(
                name: "closed_at",
                table: "maintenance_requests");

            migrationBuilder.DropColumn(
                name: "closed_by",
                table: "maintenance_requests");

            migrationBuilder.DropColumn(
                name: "resolution_notes",
                table: "maintenance_requests");

            migrationBuilder.DropColumn(
                name: "status",
                table: "maintenance_requests");

            migrationBuilder.RenameTable(
                name: "maintenance_requests",
                newName: "MaintenanceRequests");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "MaintenanceRequests",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "unit_number",
                table: "MaintenanceRequests",
                newName: "UnitNumber");

            migrationBuilder.RenameColumn(
                name: "triage_source",
                table: "MaintenanceRequests",
                newName: "TriageSource");

            migrationBuilder.RenameColumn(
                name: "tenant_tenure_months",
                table: "MaintenanceRequests",
                newName: "TenantTenureMonths");

            migrationBuilder.RenameColumn(
                name: "tags_json",
                table: "MaintenanceRequests",
                newName: "TagsJson");

            migrationBuilder.RenameColumn(
                name: "submission_channel",
                table: "MaintenanceRequests",
                newName: "SubmissionChannel");

            migrationBuilder.RenameColumn(
                name: "risk_notes",
                table: "MaintenanceRequests",
                newName: "RiskNotes");

            migrationBuilder.RenameColumn(
                name: "request_timestamp",
                table: "MaintenanceRequests",
                newName: "RequestTimestamp");

            migrationBuilder.RenameColumn(
                name: "request_text",
                table: "MaintenanceRequests",
                newName: "RequestText");

            migrationBuilder.RenameColumn(
                name: "request_number",
                table: "MaintenanceRequests",
                newName: "RequestNumber");

            migrationBuilder.RenameColumn(
                name: "property_id",
                table: "MaintenanceRequests",
                newName: "PropertyId");

            migrationBuilder.RenameColumn(
                name: "prior_requests_last_6mo",
                table: "MaintenanceRequests",
                newName: "PriorRequestsLast6Mo");

            migrationBuilder.RenameColumn(
                name: "predicted_urgency",
                table: "MaintenanceRequests",
                newName: "PredictedUrgency");

            migrationBuilder.RenameColumn(
                name: "predicted_category",
                table: "MaintenanceRequests",
                newName: "PredictedCategory");

            migrationBuilder.RenameColumn(
                name: "needs_human_review",
                table: "MaintenanceRequests",
                newName: "NeedsHumanReview");

            migrationBuilder.RenameColumn(
                name: "last_triaged_at",
                table: "MaintenanceRequests",
                newName: "LastTriagedAt");

            migrationBuilder.RenameColumn(
                name: "image_url_or_count",
                table: "MaintenanceRequests",
                newName: "ImageUrlOrCount");

            migrationBuilder.RenameColumn(
                name: "image_type",
                table: "MaintenanceRequests",
                newName: "ImageType");

            migrationBuilder.RenameColumn(
                name: "image_severity_hint",
                table: "MaintenanceRequests",
                newName: "ImageSeverityHint");

            migrationBuilder.RenameColumn(
                name: "has_image",
                table: "MaintenanceRequests",
                newName: "HasImage");

            migrationBuilder.RenameColumn(
                name: "confidence_score",
                table: "MaintenanceRequests",
                newName: "ConfidenceScore");

            migrationBuilder.RenameColumn(
                name: "building_type",
                table: "MaintenanceRequests",
                newName: "BuildingType");

            migrationBuilder.RenameColumn(
                name: "actual_urgency",
                table: "MaintenanceRequests",
                newName: "ActualUrgency");

            migrationBuilder.RenameColumn(
                name: "actual_category",
                table: "MaintenanceRequests",
                newName: "ActualCategory");

            migrationBuilder.RenameIndex(
                name: "uq_maintenance_requests_request_number",
                table: "MaintenanceRequests",
                newName: "IX_MaintenanceRequests_RequestNumber");

            migrationBuilder.AddPrimaryKey(
                name: "PK_MaintenanceRequests",
                table: "MaintenanceRequests",
                column: "Id");
        }
    }
}
