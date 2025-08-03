-- Add Manager Comment columns to KaizenForms table
ALTER TABLE KaizenForms 
ADD ManagerComments NVARCHAR(1000) NULL;

ALTER TABLE KaizenForms 
ADD ManagerSignature NVARCHAR(100) NULL;

-- Update the migration history to mark this migration as applied
INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
VALUES ('20250801060617_AddManagerCommentFields', '8.0.6'); 