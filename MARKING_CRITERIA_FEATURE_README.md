# Marking Criteria Feature

## Overview

The Marking Criteria feature allows administrators to define and manage evaluation criteria for kaizen suggestions. This system provides a structured approach to assess and score kaizen submissions based on predefined criteria with weighted scoring.

## Features

### 1. Criteria Management
- **Add New Criteria**: Create new evaluation criteria with custom names, descriptions, and scoring parameters
- **Edit Criteria**: Modify existing criteria including scores, weights, and descriptions
- **Delete Criteria**: Remove criteria that are no longer needed
- **Active/Inactive Status**: Toggle criteria availability for evaluations

### 2. Scoring System
- **Maximum Score**: Each criteria can have a maximum score (1-100 points)
- **Weight Percentage**: Assign importance weights (1-100%) to each criteria
- **Category Classification**: Organize criteria by categories (Innovation, Cost Saving, Implementation, etc.)

### 3. Predefined Sample Criteria
The system comes with 5 sample criteria:
- **Innovation Level** (25 points, 25% weight) - Evaluates creativity and originality
- **Cost Savings Impact** (30 points, 30% weight) - Assesses financial benefits
- **Implementation Feasibility** (20 points, 20% weight) - Evaluates practicality
- **Quality Improvement** (15 points, 15% weight) - Measures quality/safety improvements
- **Scalability** (10 points, 10% weight) - Assesses replication potential

## Database Structure

### MarkingCriteria Table
```sql
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
```

## Implementation Details

### Models
- **MarkingCriteria**: Database entity for storing criteria data
- **MarkingCriteriaViewModel**: View model for form binding and validation

### Controllers
- **AdminController**: Contains all marking criteria management methods
  - `MarkingCriteria()` - List all criteria
  - `AddMarkingCriteria()` - Add new criteria (GET/POST)
  - `EditMarkingCriteria()` - Edit existing criteria (GET/POST)
  - `DeleteMarkingCriteria()` - Delete criteria
  - `ToggleMarkingCriteriaStatus()` - Activate/deactivate criteria

### Views
- **MarkingCriteria.cshtml**: Main listing page with table view
- **AddMarkingCriteria.cshtml**: Form for adding new criteria
- **EditMarkingCriteria.cshtml**: Form for editing existing criteria

## Usage

### For Administrators
1. Navigate to **Admin > Award Tracking**
2. Click **"Add Marking Criteria"** button
3. Fill in the criteria details:
   - **Criteria Name**: Short, descriptive name
   - **Category**: Select from predefined categories
   - **Description**: Detailed explanation of how to evaluate
   - **Maximum Score**: Points available (1-100)
   - **Weight**: Importance percentage (1-100%)
   - **Notes**: Additional instructions (optional)
   - **Active**: Check to make available for use
4. Save the criteria

### Access Points
- **Primary Access**: Admin/AwardTracking page → "Add Marking Criteria" button
- **Direct Access**: Admin/MarkingCriteria page for full management
- **Navigation**: Admin menu can be extended to include direct link

## Benefits

### For Evaluation Process
- **Consistent Scoring**: Standardized criteria ensure fair evaluation
- **Weighted Assessment**: Important factors get appropriate emphasis
- **Transparency**: Clear criteria help evaluators understand expectations
- **Flexibility**: Customizable criteria adapt to organizational needs

### For Administrators
- **Centralized Management**: All criteria in one place
- **Easy Maintenance**: Simple add/edit/delete operations
- **Version Control**: Track changes with timestamps and user attribution
- **Active/Inactive Control**: Manage criteria availability without deletion

## Future Enhancements

### Potential Improvements
1. **Criteria Templates**: Pre-built templates for common evaluation scenarios
2. **Scoring History**: Track how criteria have been used in evaluations
3. **Criteria Analytics**: Analyze which criteria are most/least effective
4. **Multi-language Support**: Support for criteria in different languages
5. **Criteria Import/Export**: Bulk import/export functionality
6. **Criteria Validation**: Automatic validation of weight totals
7. **Integration with Award Assignment**: Use criteria in award assignment process

### Integration Opportunities
- **Award Assignment**: Integrate criteria scoring into award decisions
- **Reporting**: Include criteria scores in kaizen reports
- **Dashboard**: Show criteria usage statistics
- **Notifications**: Alert when criteria weights don't total 100%

## Technical Notes

### Security
- All operations require admin privileges
- CSRF protection enabled on all forms
- Input validation and sanitization implemented

### Performance
- Efficient database queries with proper indexing
- Minimal JavaScript for enhanced user experience
- Responsive design for mobile compatibility

### Maintenance
- Database migration script provided (`add_marking_criteria_table.sql`)
- Sample data included for immediate use
- Clear documentation for future developers

## Support

For technical support or questions about this feature:
1. Check the application logs for error details
2. Verify database connection and table existence
3. Ensure admin privileges are properly configured
4. Contact the development team for additional assistance
