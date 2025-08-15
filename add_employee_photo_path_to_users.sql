-- Add EmployeePhotoPath column to Users table
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'EmployeePhotoPath')
BEGIN
    ALTER TABLE Users ADD EmployeePhotoPath NVARCHAR(255) NULL;
    PRINT 'EmployeePhotoPath column added to Users table successfully.';
END
ELSE
BEGIN
    PRINT 'EmployeePhotoPath column already exists in Users table.';
END






