-- Test data for Leaderboard functionality
-- This script adds multiple kaizens for the same employee numbers to test grouping

-- Clear existing test data (optional - uncomment if needed)
-- DELETE FROM KaizenForms WHERE EmployeeNo IN ('EMP001', 'EMP002', 'SUP001');

-- Add multiple kaizens for EMP001-User (John Smith) - should be grouped together
INSERT INTO KaizenForms (KaizenNo, DateSubmitted, Department, Plant, EmployeeName, EmployeeNo, SuggestionDescription, Category, EngineerStatus, ManagerStatus, CostSaving)
VALUES 
('KAIZEN-2024-006', GETDATE(), 'Production', 'Plant 1', 'John Smith', 'EMP001', 'Improved workstation layout for better ergonomics', 'Ergonomics Improvements', 'Approved', 'Approved', 1500.00),
('KAIZEN-2024-007', GETDATE(), 'Production', 'Plant 1', 'John Smith', 'EMP001', 'Reduced material waste by 15% through process optimization', 'Cost reduction', 'Approved', 'Approved', 2500.00),
('KAIZEN-2024-008', GETDATE(), 'Production', 'Plant 1', 'John Smith', 'EMP001', 'Enhanced safety protocols in assembly line', 'OHS Improvement', 'Pending', 'Pending', 0.00),
('KAIZEN-2024-009', GETDATE(), 'Production', 'Plant 1', 'John Smith', 'EMP001', 'Streamlined quality inspection process', 'Quality Improvements', 'Approved', 'Pending', 800.00);

-- Add multiple kaizens for EMP002-User (Jane Doe) - should be grouped together
INSERT INTO KaizenForms (KaizenNo, DateSubmitted, Department, Plant, EmployeeName, EmployeeNo, SuggestionDescription, Category, EngineerStatus, ManagerStatus, CostSaving)
VALUES 
('KAIZEN-2024-010', GETDATE(), 'Quality', 'Plant 2', 'Jane Doe', 'EMP002', 'Enhanced quality inspection checklist', 'Quality Improvements', 'Approved', 'Approved', 1200.00),
('KAIZEN-2024-011', GETDATE(), 'Quality', 'Plant 2', 'Jane Doe', 'EMP002', 'Improved safety signage in work area', 'OHS Improvement', 'Rejected', 'Pending', 0.00),
('KAIZEN-2024-012', GETDATE(), 'Quality', 'Plant 2', 'Jane Doe', 'EMP002', 'Optimized testing procedures', 'Quality Improvements', 'Approved', 'Approved', 1800.00),
('KAIZEN-2024-013', GETDATE(), 'Quality', 'Plant 2', 'Jane Doe', 'EMP002', 'Reduced energy consumption in lab', 'Energy Saving Improvement', 'Pending', 'Pending', 0.00),
('KAIZEN-2024-014', GETDATE(), 'Quality', 'Plant 2', 'Jane Doe', 'EMP002', '5S implementation in storage area', '5S Improvement', 'Approved', 'Approved', 600.00);

-- Add multiple kaizens for SUP001-Supervisor (Mike Johnson) - should be grouped together
INSERT INTO KaizenForms (KaizenNo, DateSubmitted, Department, Plant, EmployeeName, EmployeeNo, SuggestionDescription, Category, EngineerStatus, ManagerStatus, CostSaving)
VALUES 
('KAIZEN-2024-015', GETDATE(), 'Management', 'Plant 1', 'Mike Johnson', 'SUP001', 'Streamlined reporting process', 'Productivity Improvement', 'Approved', 'Approved', 3000.00),
('KAIZEN-2024-016', GETDATE(), 'Management', 'Plant 1', 'Mike Johnson', 'SUP001', 'Improved team communication system', 'Productivity Improvement', 'Approved', 'Approved', 2200.00),
('KAIZEN-2024-017', GETDATE(), 'Management', 'Plant 1', 'Mike Johnson', 'SUP001', 'Enhanced training program', 'Productivity Improvement', 'Pending', 'Pending', 0.00);

-- Show the test data grouped by employee
SELECT 
    EmployeeNo,
    EmployeeName,
    Department,
    COUNT(*) as TotalKaizens,
    COUNT(CASE WHEN EngineerStatus = 'Approved' AND ManagerStatus = 'Approved' THEN 1 END) as ApprovedKaizens,
    COUNT(CASE WHEN (EngineerStatus = 'Pending' OR EngineerStatus IS NULL) OR 
                  (EngineerStatus = 'Approved' AND (ManagerStatus = 'Pending' OR ManagerStatus IS NULL)) THEN 1 END) as PendingKaizens,
    COUNT(CASE WHEN EngineerStatus = 'Rejected' OR ManagerStatus = 'Rejected' THEN 1 END) as RejectedKaizens,
    SUM(ISNULL(CostSaving, 0)) as TotalCostSaving,
    MAX(DateSubmitted) as LastSubmission
FROM KaizenForms 
WHERE EmployeeNo IN ('EMP001', 'EMP002', 'SUP001')
GROUP BY EmployeeNo, EmployeeName, Department
ORDER BY TotalKaizens DESC, ApprovedKaizens DESC, TotalCostSaving DESC;

