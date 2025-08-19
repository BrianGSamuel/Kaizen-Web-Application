-- Add Admin User to the database
-- This script should be run after the database is created

-- Check if admin user already exists to avoid duplicates
IF NOT EXISTS (SELECT 1 FROM Users WHERE UserName = 'Admin')
BEGIN
    INSERT INTO Users (
        DepartmentName,
        Plant,
        UserName,
        Password,
        Role,
        EmployeeName,
        EmployeeNumber,
        Email
    ) VALUES (
        'Administration',
        'Main Plant',
        'Admin',
        'admin123',
        'Admin',
        'System Administrator',
        'ADMIN001',
        'admin@company.com'
    );
    
    PRINT 'Admin user created successfully with username: Admin and password: admin123';
END
ELSE
BEGIN
    PRINT 'Admin user already exists in the database.';
END

