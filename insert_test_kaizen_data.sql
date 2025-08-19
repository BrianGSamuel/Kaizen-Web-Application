-- Insert test kaizen data for the test users
-- This will help verify that the MyKaizens page is working correctly

-- Insert kaizen for EMP001-User (John Smith)
INSERT INTO KaizenForms (KaizenNo, DateSubmitted, Department, Plant, EmployeeName, EmployeeNo, SuggestionDescription, Category, EngineerStatus, ManagerStatus)
VALUES ('KAIZEN-2024-001', GETDATE(), 'Production', 'Plant 1', 'John Smith', 'EMP001', 'Improved workstation layout for better ergonomics', 'Ergonomics Improvements', 'Approved', 'Approved');

INSERT INTO KaizenForms (KaizenNo, DateSubmitted, Department, Plant, EmployeeName, EmployeeNo, SuggestionDescription, Category, EngineerStatus, ManagerStatus)
VALUES ('KAIZEN-2024-002', GETDATE(), 'Production', 'Plant 1', 'John Smith', 'EMP001', 'Reduced material waste by 15% through process optimization', 'Cost reduction', 'Pending', 'Pending');

-- Insert kaizen for EMP002-User (Jane Doe)
INSERT INTO KaizenForms (KaizenNo, DateSubmitted, Department, Plant, EmployeeName, EmployeeNo, SuggestionDescription, Category, EngineerStatus, ManagerStatus)
VALUES ('KAIZEN-2024-003', GETDATE(), 'Quality', 'Plant 2', 'Jane Doe', 'EMP002', 'Enhanced quality inspection checklist', 'Quality Improvements', 'Approved', 'Pending');

INSERT INTO KaizenForms (KaizenNo, DateSubmitted, Department, Plant, EmployeeName, EmployeeNo, SuggestionDescription, Category, EngineerStatus, ManagerStatus)
VALUES ('KAIZEN-2024-004', GETDATE(), 'Quality', 'Plant 2', 'Jane Doe', 'EMP002', 'Improved safety signage in work area', 'OHS Improvement', 'Rejected', 'Pending');

-- Insert kaizen for SUP001-Supervisor (Mike Johnson)
INSERT INTO KaizenForms (KaizenNo, DateSubmitted, Department, Plant, EmployeeName, EmployeeNo, SuggestionDescription, Category, EngineerStatus, ManagerStatus)
VALUES ('KAIZEN-2024-005', GETDATE(), 'Management', 'Plant 1', 'Mike Johnson', 'SUP001', 'Streamlined reporting process', 'Productivity Improvement', 'Approved', 'Approved');

-- Show the inserted data
SELECT KaizenNo, DateSubmitted, EmployeeName, EmployeeNo, Department, Category, EngineerStatus, ManagerStatus 
FROM KaizenForms 
WHERE EmployeeNo IN ('EMP001', 'EMP002', 'SUP001')
ORDER BY EmployeeNo, DateSubmitted;

