# Category Management Feature

## Overview
The Category Management feature allows administrators to manage kaizen categories for better organization of improvement suggestions. This feature provides a comprehensive interface for adding, editing, deleting, and managing categories.

## Features

### 1. Category Management Interface
- **Location**: Admin Panel → Category Management
- **Access**: Admin users only
- **Icon**: Tags icon (fas fa-tags)

### 2. Add New Categories
- Form to add new categories with:
  - Category Name (required, max 100 characters)
  - Active/Inactive status toggle
- Real-time validation
- Duplicate name prevention
- Success/error notifications

### 3. View Existing Categories
- Table display showing:
  - Category Name
  - Status (Active/Inactive badge)
  - Created Date
  - Last Updated Date
  - Action buttons

### 4. Edit Categories
- Modal popup for editing
- Pre-populated with current values
- Same validation as add form
- Real-time updates

### 5. Delete Categories
- Confirmation dialog before deletion
- Safety check: Prevents deletion if category is in use by kaizen forms
- Shows count of related kaizen forms if deletion is blocked

### 6. Toggle Category Status
- Quick toggle between Active/Inactive
- Visual indicators for status
- Immediate feedback

## Database Structure

### Categories Table
```sql
CREATE TABLE [Categories] (
    [Id] int NOT NULL IDENTITY(1,1),
    [Name] nvarchar(100) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_Categories] PRIMARY KEY ([Id])
);
```

### Relationship with KaizenForms
- Added `CategoryId` column to `KaizenForms` table
- Foreign key relationship to `Categories` table
- Allows kaizen forms to be categorized

## Sample Categories
The system comes pre-loaded with 8 sample categories:
1. **Safety**
2. **Quality**
3. **Efficiency**
4. **Cost Reduction**
5. **5S**
6. **Maintenance**
7. **Training**
8. **Environment**

## Technical Implementation

### Models
- `Category.cs` - Main category model
- `CategoryViewModel.cs` - View model for category forms
- `CategoryListViewModel.cs` - View model for category list page

### Controller Methods
- `CategoryManagement()` - Display category management page
- `AddCategory()` - Add new category (POST)
- `UpdateCategory()` - Update existing category (POST)
- `DeleteCategory()` - Delete category (POST)
- `ToggleCategoryStatus()` - Toggle active status (POST)

### Views
- `CategoryManagement.cshtml` - Main category management interface
- Includes modal for editing
- Toast notifications for user feedback
- Responsive design with Bootstrap

### JavaScript Features
- AJAX form submissions
- Real-time validation
- Dynamic table updates
- Toast notifications
- Confirmation dialogs

## Security
- Admin-only access
- Server-side validation
- CSRF protection
- Input sanitization

## Usage Instructions

### For Administrators:
1. Navigate to Admin Panel
2. Click on "Category Management" in the sidebar
3. Use the "Add New Category" form to create categories
4. Use action buttons to edit, toggle status, or delete categories
5. Monitor the existing categories table for management

### For Users:
- Categories will be available in kaizen form creation (future enhancement)
- Categories help organize and filter kaizen suggestions

## Future Enhancements
- Category-based filtering in kaizen lists
- Category statistics and analytics
- Bulk category operations
- Category hierarchy (parent-child relationships)
- Category-based reporting

## Files Modified/Created

### New Files:
- `Models/Category.cs`
- `ViewModels/CategoryViewModel.cs`
- `Views/Admin/CategoryManagement.cshtml`
- `add_categories_table.sql`
- `CATEGORY_MANAGEMENT_README.md`

### Modified Files:
- `Data/AppDbContext.cs` - Added Categories DbSet
- `Controllers/AdminController.cs` - Added category management methods
- `Views/Shared/_AdminLayout.cshtml` - Added Category Management navigation item

## Database Migration
The Categories table was added using a SQL script due to migration conflicts with existing tables. The script:
1. Creates the Categories table
2. Adds CategoryId column to KaizenForms
3. Creates necessary indexes and foreign keys
4. Inserts sample categories

## Testing
- Build the application: `dotnet build`
- Run the application: `dotnet run`
- Login as admin user
- Navigate to Category Management
- Test all CRUD operations
- Verify validation and error handling
