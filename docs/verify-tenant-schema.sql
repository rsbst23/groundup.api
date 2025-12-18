-- ================================================
-- Verification Script for Tenant Schema Changes
-- Migration: AddTenantTypeAndRealmUrl
-- ================================================

-- Check the Tenants table structure
DESCRIBE Tenants;

-- Check all existing tenants and their new fields
SELECT 
    Id, 
    Name, 
    TenantType, 
    RealmUrl,
    IsActive,
    CreatedAt
FROM Tenants
ORDER BY Id;

-- Verify the TenantType index exists
SHOW INDEX FROM Tenants WHERE Key_name = 'IX_Tenants_TenantType';

-- Verify the RealmUrl index exists
SHOW INDEX FROM Tenants WHERE Key_name = 'IX_Tenants_RealmUrl';

-- Count tenants by type
SELECT 
    TenantType,
    COUNT(*) as Count
FROM Tenants
GROUP BY TenantType;

-- Check for any NULL TenantType values (should be 0)
SELECT COUNT(*) as NullTenantTypeCount
FROM Tenants
WHERE TenantType IS NULL;

-- ================================================
-- Expected Results:
-- ================================================
-- 1. DESCRIBE Tenants should show:
--    - TenantType: varchar(50), NOT NULL, default 'standard'
--    - RealmUrl: varchar(255), NULL
--
-- 2. All existing tenants should have TenantType = 'standard'
--
-- 3. Both indexes should exist
--
-- 4. NullTenantTypeCount should be 0
-- ================================================
