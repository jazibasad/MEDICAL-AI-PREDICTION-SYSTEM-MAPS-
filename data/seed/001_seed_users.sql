-- ============================================================
-- MAPS Demo Seed Data — Run after EF Core migrations
-- File: data/seed/001_seed_users.sql
-- ============================================================

-- Demo Doctor
INSERT INTO "Users" ("UserId","FullName","Email","PasswordHash","Role","IsActive","IsApproved","CreatedAt")
VALUES (
  '00000000-0000-0000-0000-000000000002',
  'Dr. Sarah Ahmed',
  'doctor@maps.local',
  '$2a$11$demohashdoctor111111111111111111111111111111111111111',
  2, true, true, NOW()
) ON CONFLICT DO NOTHING;

INSERT INTO "DoctorProfiles" ("DoctorId","UserId","Specialization","LicenseNumber","Department")
VALUES (
  '00000000-0000-0000-0000-000000000012',
  '00000000-0000-0000-0000-000000000002',
  'Internal Medicine', 'LIC-2026-001', 'General Medicine'
) ON CONFLICT DO NOTHING;

-- Demo Patient
INSERT INTO "Users" ("UserId","FullName","Email","PasswordHash","Role","IsActive","IsApproved","CreatedAt")
VALUES (
  '00000000-0000-0000-0000-000000000003',
  'Ali Hassan',
  'patient@maps.local',
  '$2a$11$demohashpatient11111111111111111111111111111111111111',
  3, true, true, NOW()
) ON CONFLICT DO NOTHING;

INSERT INTO "PatientProfiles" ("PatientId","UserId","BloodGroup","DateOfBirth","EmergencyContact")
VALUES (
  '00000000-0000-0000-0000-000000000013',
  '00000000-0000-0000-0000-000000000003',
  'O+', '1990-05-15', '+92-300-0000001'
) ON CONFLICT DO NOTHING;

-- Demo Assignment
INSERT INTO "Assignments" ("AssignmentId","DoctorId","PatientId","AssignedDate","IsActive")
VALUES (
  '00000000-0000-0000-0000-000000000021',
  '00000000-0000-0000-0000-000000000012',
  '00000000-0000-0000-0000-000000000013',
  NOW(), true
) ON CONFLICT DO NOTHING;

-- Note: Run this AFTER dotnet ef database update
-- Credentials for testing:
--   Admin:   admin@maps.local   / Admin@123!
--   Doctor:  doctor@maps.local  / Doctor@123!
--   Patient: patient@maps.local / Patient@123!
