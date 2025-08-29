-- Add Categories table
CREATE TABLE [Categories] (
    [Id] int NOT NULL IDENTITY(1,1),
    [Name] nvarchar(100) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_Categories] PRIMARY KEY ([Id])
);

-- Add CategoryId column to KaizenForms table
ALTER TABLE [KaizenForms] ADD [CategoryId] int NULL;

-- Create index for better performance
CREATE INDEX [IX_KaizenForms_CategoryId] ON [KaizenForms] ([CategoryId]);

-- Add foreign key constraint
ALTER TABLE [KaizenForms] ADD CONSTRAINT [FK_KaizenForms_Categories_CategoryId] 
    FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]);

-- Insert some sample categories
INSERT INTO [Categories] ([Name], [CreatedAt], [IsActive]) VALUES
('Safety', GETDATE(), 1),
('Quality', GETDATE(), 1),
('Efficiency', GETDATE(), 1),
('Cost Reduction', GETDATE(), 1),
('5S', GETDATE(), 1),
('Maintenance', GETDATE(), 1),
('Training', GETDATE(), 1),
('Environment', GETDATE(), 1);
