# Employee Photo Upload Feature

## Overview
This feature allows supervisors to upload employee photos when registering new users in the Kaizen Management System.

## Features Implemented

### 1. Image Upload Field
- Added a new file input field for employee photos in the user registration form
- Supports multiple image formats: JPG, PNG, JPEG, WEBP
- File size limit: 5MB maximum
- Optional field (not required for user registration)

### 2. Image Preview
- Real-time preview of uploaded images before form submission
- Preview shows below the file input field
- Responsive design with proper styling

### 3. File Validation
- Client-side validation for file type and size
- Server-side validation using the existing FileService
- Automatic file type detection and validation

### 4. Storage
- Images are stored in the `wwwroot/uploads/employee-photos/` directory
- Unique filenames generated using GUIDs to prevent conflicts
- Relative paths stored in the database for easy access

## Technical Implementation

### Files Modified

#### 1. ViewModels/RegisterViewModel.cs
- Added `EmployeePhoto` property of type `IFormFile?`
- Includes proper display attributes and validation

#### 2. Views/Account/RegisterUser.cshtml
- Added image upload field with proper styling
- Updated form to support multipart/form-data
- Added image preview functionality
- Enhanced JavaScript validation

#### 3. Controllers/AccountController.cs
- Injected `IFileService` for handling file uploads
- Updated `RegisterUser` POST method to handle image uploads
- Added file validation and error handling
- Integrated with existing user creation logic

#### 4. wwwroot/css/account.css
- Added custom styling for the file upload field
- Responsive design with hover and focus states
- Dark mode support
- Custom styling for file input buttons

### Database Integration
- Uses existing `EmployeePhotoPath` field in the `Users` model
- No database schema changes required
- Backward compatible with existing user records

## Usage

### For Supervisors
1. Navigate to User Management → Register New User
2. Fill in all required user information
3. Optionally upload an employee photo
4. The system will automatically validate and store the image
5. User is created with the photo path stored in the database

### File Requirements
- **Supported Formats**: JPG, PNG, JPEG, WEBP
- **Maximum Size**: 5MB
- **Image Quality**: Any standard image quality

## Security Features
- File type validation to prevent malicious uploads
- File size limits to prevent abuse
- Secure file naming using GUIDs
- Proper error handling and user feedback

## Future Enhancements
- Image compression and optimization
- Thumbnail generation for faster loading
- Bulk image upload support
- Image editing capabilities
- Integration with user profile management

## Testing
The feature has been tested with:
- Various image formats (JPG, PNG, JPEG, WEBP)
- Different file sizes (small to 5MB limit)
- Form validation and error handling
- Image preview functionality
- Database storage and retrieval

## Dependencies
- `IFileService` - Handles file operations
- `IFormFile` - ASP.NET Core file upload model
- Existing `Users` model with `EmployeePhotoPath` field
- Bootstrap CSS framework for styling
- JavaScript for client-side validation and preview
