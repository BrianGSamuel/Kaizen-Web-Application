-- Insert test user with employee data
INSERT INTO Users (UserName, EmployeeName, EmployeeNumber, DepartmentName, Plant, Password, Role, EmployeePhotoPath)
VALUES ('EMP001-User', 'John Smith', 'EMP001', 'Production', 'Plant 1', 'password123', 'User', NULL);

-- Insert another test user
INSERT INTO Users (UserName, EmployeeName, EmployeeNumber, DepartmentName, Plant, Password, Role, EmployeePhotoPath)
VALUES ('EMP002-User', 'Jane Doe', 'EMP002', 'Quality', 'Plant 2', 'password123', 'User', NULL);

-- Insert a supervisor user
INSERT INTO Users (UserName, EmployeeName, EmployeeNumber, DepartmentName, Plant, Password, Role, EmployeePhotoPath)
VALUES ('SUP001-Supervisor', 'Mike Johnson', 'SUP001', 'Management', 'Plant 1', 'password123', 'Supervisor', NULL);

-- Show the inserted data
SELECT UserName, EmployeeName, EmployeeNumber, DepartmentName, Plant, Role FROM Users;


