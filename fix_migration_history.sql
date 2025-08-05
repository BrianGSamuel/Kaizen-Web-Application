-- Fix migration history by removing the reference to the deleted migration
-- This script should be run in your database to clean up the migration history

-- Remove the reference to the deleted migration
DELETE FROM __EFMigrationsHistory 
WHERE MigrationId = '20250801060617_AddManagerCommentFields';

-- Verify the current migration history
SELECT MigrationId, ProductVersion 
FROM __EFMigrationsHistory 
ORDER BY MigrationId; 