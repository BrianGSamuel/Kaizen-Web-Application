-- Add Role column to Users table
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'Role')
BEGIN
    ALTER TABLE Users ADD Role NVARCHAR(50) NOT NULL DEFAULT 'User';
END

-- Update existing users with appropriate roles based on their usernames
UPDATE Users SET Role = 'Admin' WHERE UserName = 'admin';
UPDATE Users SET Role = 'Manager' WHERE UserName LIKE '%manager%' AND UserName != 'admin';
UPDATE Users SET Role = 'KaizenTeam' WHERE UserName LIKE '%kaizenteam%';
UPDATE Users SET Role = 'User' WHERE UserName LIKE '%user%' AND UserName NOT LIKE '%manager%' AND UserName NOT LIKE '%kaizenteam%' AND UserName != 'admin';
UPDATE Users SET Role = 'Engineer' WHERE UserName NOT LIKE '%user%' AND UserName NOT LIKE '%manager%' AND UserName NOT LIKE '%kaizenteam%' AND UserName != 'admin';








