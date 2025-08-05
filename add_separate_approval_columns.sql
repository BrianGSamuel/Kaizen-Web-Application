-- Add missing columns for separate engineer and manager approval tracking
-- Only add columns that don't already exist

-- Check if ManagerStatus column exists, if not add it
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'KaizenForms' AND COLUMN_NAME = 'ManagerStatus')
BEGIN
    ALTER TABLE [KaizenForms] ADD [ManagerStatus] nvarchar(20) NULL;
END

-- Check if ManagerApprovedBy column exists, if not add it
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'KaizenForms' AND COLUMN_NAME = 'ManagerApprovedBy')
BEGIN
    ALTER TABLE [KaizenForms] ADD [ManagerApprovedBy] nvarchar(100) NULL;
END

-- Check if EngineerStatus column exists, if not add it
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'KaizenForms' AND COLUMN_NAME = 'EngineerStatus')
BEGIN
    ALTER TABLE [KaizenForms] ADD [EngineerStatus] nvarchar(20) NULL;
END

-- Check if EngineerApprovedBy column exists, if not add it
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'KaizenForms' AND COLUMN_NAME = 'EngineerApprovedBy')
BEGIN
    ALTER TABLE [KaizenForms] ADD [EngineerApprovedBy] nvarchar(100) NULL;
END

-- Update existing records to set default values for the new status fields
UPDATE [KaizenForms] 
SET [EngineerStatus] = 'Pending', [ManagerStatus] = 'Pending'
WHERE [EngineerStatus] IS NULL OR [ManagerStatus] IS NULL; 