-- Add MarkingCriteria table
CREATE TABLE [MarkingCriteria] (
    [Id] int NOT NULL IDENTITY(1,1),
    [CriteriaName] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NOT NULL,
    [MaxScore] int NOT NULL,
    [Weight] int NOT NULL,
    [Category] nvarchar(50) NULL,
    [IsActive] bit NOT NULL DEFAULT 1,
    [Notes] nvarchar(500) NULL,
    [CreatedAt] datetime2 NOT NULL DEFAULT GETDATE(),
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(100) NULL,
    [UpdatedBy] nvarchar(100) NULL,
    CONSTRAINT [PK_MarkingCriteria] PRIMARY KEY ([Id])
);

-- Insert some sample marking criteria
INSERT INTO [MarkingCriteria] ([CriteriaName], [Description], [MaxScore], [Weight], [Category], [Notes], [CreatedBy], [CreatedAt]) VALUES
('Innovation Level', 'How innovative and creative is the kaizen suggestion? Does it introduce new ideas or approaches?', 25, 25, 'Innovation', 'Evaluate the originality and creativity of the suggestion', 'Admin', GETDATE()),
('Cost Savings Impact', 'What is the financial impact of the kaizen suggestion? How much money will it save?', 30, 30, 'Cost Saving', 'Consider both direct and indirect cost savings', 'Admin', GETDATE()),
('Implementation Feasibility', 'How easy is it to implement this kaizen suggestion? What resources are required?', 20, 20, 'Implementation', 'Assess the practicality and resource requirements', 'Admin', GETDATE()),
('Quality Improvement', 'How does this kaizen suggestion improve quality, safety, or efficiency?', 15, 15, 'Quality', 'Evaluate improvements in quality, safety, or process efficiency', 'Admin', GETDATE()),
('Scalability', 'Can this kaizen suggestion be applied to other areas or departments?', 10, 10, 'Innovation', 'Consider if the solution can be replicated elsewhere', 'Admin', GETDATE());

-- Update the migration history to mark this migration as applied
INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
VALUES ('20250101000000_AddMarkingCriteriaTable', '8.0.6');
