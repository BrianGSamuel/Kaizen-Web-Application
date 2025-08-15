-- Add Plant column to KaizenForms table
USE UserApp;
GO

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'KaizenForms' AND COLUMN_NAME = 'Plant')
BEGIN
    ALTER TABLE KaizenForms ADD Plant NVARCHAR(10) NULL;
    PRINT 'Plant column added to KaizenForms table';
END
ELSE
BEGIN
    PRINT 'Plant column already exists in KaizenForms table';
END
