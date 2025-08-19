# Admin User Setup

## Overview
The Kaizen Web Application automatically creates an admin user when the database is first initialized.

## Admin User Credentials
- **Username**: `Admin`
- **Password**: `admin123`
- **Role**: `Admin`
- **Department**: `Administration`
- **Plant**: `Main Plant`
- **Email**: `admin@company.com`

## Automatic Creation
The admin user is automatically created when:
1. The database migration `AddAdminUserToDatabase` is applied
2. The application starts for the first time (via DatabaseSeedService)
3. The `DatabaseSeedService.SeedDatabaseAsync()` method is called

## Applying the Migration
To apply the migration and create the admin user:

```bash
dotnet ef database update
```

## Manual Creation
If you need to manually create the admin user, you can run the SQL script:

```sql
-- Run this script in your database
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
END
```

## Security Note
⚠️ **Important**: The current implementation stores passwords in plain text. For production use, implement proper password hashing using BCrypt or similar algorithms.

## Admin Privileges
The admin user has access to:
- User management
- System settings
- All kaizen forms and reports
- Department targets management
- Award tracking and management

## Changing Admin Password
After first login, it's recommended to change the admin password through the application's password change functionality.
