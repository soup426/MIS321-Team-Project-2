-- Mulholland Real Estate - Maintenance Triage schema (MySQL 8+)
-- Copy/paste into your DB console. Safe to run on an empty schema.
-- Notes:
-- - Business key is request_number (matches API routes).
-- - Ticket lifecycle is modeled via status + closed_* fields (open/closed).
-- - actual_* are sample labels (evaluation), not resident input.

CREATE TABLE IF NOT EXISTS maintenance_requests (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  request_number INT NOT NULL,

  property_id VARCHAR(32) NOT NULL,
  unit_number VARCHAR(32) NOT NULL,
  building_type VARCHAR(64) NOT NULL,
  tenant_tenure_months INT NOT NULL,
  submission_channel VARCHAR(32) NOT NULL,
  request_timestamp DATETIME(6) NOT NULL,
  request_text VARCHAR(4000) NOT NULL,

  has_image TINYINT(1) NOT NULL DEFAULT 0,
  image_type VARCHAR(64) NOT NULL,
  image_severity_hint VARCHAR(32) NOT NULL,
  image_url_or_count VARCHAR(255) NULL,
  prior_requests_last_6mo INT NOT NULL DEFAULT 0,

  actual_category VARCHAR(64) NOT NULL,
  actual_urgency VARCHAR(32) NOT NULL,

  predicted_category VARCHAR(64) NULL,
  predicted_urgency VARCHAR(32) NULL,
  confidence_score DECIMAL(5,4) NULL,
  tags_json VARCHAR(1000) NULL,
  risk_notes VARCHAR(2000) NULL,
  needs_human_review TINYINT(1) NOT NULL DEFAULT 0,
  last_triaged_at DATETIME(6) NULL,
  triage_source VARCHAR(32) NULL,

  status ENUM('Open','Closed') NOT NULL DEFAULT 'Open',
  closed_at DATETIME(6) NULL,
  closed_by VARCHAR(128) NULL,
  resolution_notes VARCHAR(2000) NULL,

  created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),

  PRIMARY KEY (id),
  UNIQUE KEY uq_maintenance_requests_request_number (request_number),
  KEY ix_maintenance_requests_status_timestamp (status, request_timestamp),
  KEY ix_maintenance_requests_needs_human (needs_human_review, status),
  KEY ix_maintenance_requests_predicted_urgency (predicted_urgency),
  KEY ix_maintenance_requests_property_unit (property_id, unit_number)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Optional: capture state changes / triage runs for auditability.
CREATE TABLE IF NOT EXISTS maintenance_request_events (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  maintenance_request_id BIGINT UNSIGNED NOT NULL,
  event_type VARCHAR(64) NOT NULL, -- e.g. 'triaged', 'closed', 'reopened', 'note'
  event_timestamp DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  actor VARCHAR(128) NULL,
  details_json JSON NULL,

  PRIMARY KEY (id),
  KEY ix_events_request_time (maintenance_request_id, event_timestamp),
  CONSTRAINT fk_events_request
    FOREIGN KEY (maintenance_request_id) REFERENCES maintenance_requests(id)
    ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

