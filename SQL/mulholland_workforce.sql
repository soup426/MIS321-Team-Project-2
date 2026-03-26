-- Mulholland Real Estate - Workforce / Assignment / Notes (MySQL 8+)
-- Run after mulholland_schema.sql

CREATE TABLE IF NOT EXISTS employees (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  full_name VARCHAR(128) NOT NULL,
  active TINYINT(1) NOT NULL DEFAULT 1,
  role ENUM('Maintenance','Vendor','Manager') NOT NULL DEFAULT 'Maintenance',
  phone VARCHAR(32) NULL,
  email VARCHAR(128) NULL,
  home_property_id VARCHAR(32) NULL,
  max_open_tickets INT NOT NULL DEFAULT 10,
  created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
  PRIMARY KEY (id),
  KEY ix_employees_active (active),
  KEY ix_employees_property (home_property_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS skills (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  skill_code VARCHAR(64) NOT NULL,
  display_name VARCHAR(128) NOT NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_skills_code (skill_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS employee_skills (
  employee_id BIGINT UNSIGNED NOT NULL,
  skill_id BIGINT UNSIGNED NOT NULL,
  proficiency TINYINT UNSIGNED NOT NULL DEFAULT 3,
  PRIMARY KEY (employee_id, skill_id),
  KEY ix_emp_skills_skill (skill_id),
  CONSTRAINT fk_emp_skills_emp FOREIGN KEY (employee_id) REFERENCES employees(id) ON DELETE CASCADE,
  CONSTRAINT fk_emp_skills_skill FOREIGN KEY (skill_id) REFERENCES skills(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS ticket_assignments (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  maintenance_request_id BIGINT UNSIGNED NOT NULL,
  employee_id BIGINT UNSIGNED NOT NULL,
  assigned_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  unassigned_at DATETIME(6) NULL,
  assigned_by VARCHAR(64) NOT NULL DEFAULT 'auto',
  reason VARCHAR(255) NULL,
  PRIMARY KEY (id),
  KEY ix_assignments_ticket (maintenance_request_id, assigned_at),
  KEY ix_assignments_employee (employee_id, assigned_at),
  CONSTRAINT fk_assignments_ticket FOREIGN KEY (maintenance_request_id) REFERENCES maintenance_requests(id) ON DELETE CASCADE,
  CONSTRAINT fk_assignments_employee FOREIGN KEY (employee_id) REFERENCES employees(id) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Optional direct columns on ticket for fast read:
-- MySQL does NOT support "ADD COLUMN IF NOT EXISTS", so do conditional adds via information_schema.
SET @db := DATABASE();

SET @sql := IF(
  EXISTS(SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='maintenance_requests' AND COLUMN_NAME='assigned_employee_id'),
  'SELECT ''assigned_employee_id exists''',
  'ALTER TABLE maintenance_requests ADD COLUMN assigned_employee_id BIGINT UNSIGNED NULL'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := IF(
  EXISTS(SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='maintenance_requests' AND COLUMN_NAME='assigned_at'),
  'SELECT ''assigned_at exists''',
  'ALTER TABLE maintenance_requests ADD COLUMN assigned_at DATETIME(6) NULL'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := IF(
  EXISTS(SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='maintenance_requests' AND COLUMN_NAME='assignment_source'),
  'SELECT ''assignment_source exists''',
  'ALTER TABLE maintenance_requests ADD COLUMN assignment_source VARCHAR(32) NULL'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := IF(
  EXISTS(SELECT 1 FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='maintenance_requests' AND INDEX_NAME='ix_requests_assigned'),
  'SELECT ''ix_requests_assigned exists''',
  'ALTER TABLE maintenance_requests ADD INDEX ix_requests_assigned (assigned_employee_id)'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- MySQL doesn't support IF NOT EXISTS for FK constraints; add manually if you want:
-- ALTER TABLE maintenance_requests
--   ADD CONSTRAINT fk_requests_assigned_emp
--     FOREIGN KEY (assigned_employee_id) REFERENCES employees(id);

