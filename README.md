# Kaizen Web Application

A comprehensive Continuous Improvement (Kaizen) management system built with ASP.NET Core 8.0 and C#.

## 🚀 Features

- **User Management**: Role-based authentication and authorization
- **Kaizen Form Management**: Create, edit, and track improvement suggestions
- **Approval Workflow**: Multi-level approval process (Engineer and Manager)
- **File Upload**: Image upload for before/after comparisons
- **Analytics Dashboard**: Real-time statistics and reporting
- **PDF Generation**: Export reports in PDF format
- **Department Targets**: Track improvement goals by department

## 🏗️ Architecture

This application follows modern C# and ASP.NET Core best practices:

### Core Technologies
- **.NET 8.0** - Latest LTS version
- **ASP.NET Core MVC** - Web framework
- **Entity Framework Core** - ORM for data access
- **SQL Server** - Database
- **Razor Views** - Server-side templating

### Design Patterns
- **Repository Pattern** - Data access abstraction
- **Service Layer** - Business logic separation
- **Dependency Injection** - Loose coupling
- **Middleware** - Request/response pipeline
- **Custom Exceptions** - Proper error handling

## 📁 Project Structure

```
KaizenWebApplication/
├── Controllers/          # MVC Controllers
├── Models/              # Entity models
├── ViewModels/          # View-specific models
├── Views/               # Razor views
├── Services/            # Business logic services
│   ├── IKaizenService.cs
│   ├── KaizenService.cs
│   ├── IUserService.cs
│   ├── UserService.cs
│   ├── IFileService.cs
│   └── FileService.cs
├── Data/                # Data access layer
│   └── AppDbContext.cs
├── Configuration/       # Application settings
│   └── AppSettings.cs
├── Exceptions/          # Custom exceptions
│   └── KaizenException.cs
├── Enums/              # Type-safe enumerations
│   └── KaizenStatus.cs
├── Extensions/          # Extension methods
│   └── ServiceCollectionExtensions.cs
├── Middleware/          # Custom middleware
│   └── RequestLoggingMiddleware.cs
├── wwwroot/            # Static files
└── Migrations/         # EF Core migrations
```

## 🛠️ Setup and Installation

### Prerequisites
- .NET 8.0 SDK
- SQL Server (LocalDB or full instance)
- Visual Studio 2022 or VS Code

### Installation Steps

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd KaizenWebApplication
   ```

2. **Update connection string**
   Edit `appsettings.json` and update the connection string:
   ```json
   {
     "ConnectionStrings": {
       "Default": "Server=(local);Database=UserApp;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=true"
     }
   }
   ```

3. **Run database migrations**
   ```bash
   dotnet ef database update
   ```

4. **Run the application**
   ```bash
   dotnet run
   ```

5. **Access the application**
   Navigate to `https://localhost:5001` or `http://localhost:5000`

## 👥 User Roles

- **Admin**: Full system access, user management
- **User**: Submit Kaizen suggestions
- **Manager**: Approve/reject suggestions, view analytics
- **Engineer**: Technical review of suggestions
- **Kaizen Team**: Special access for team management

## 🔧 Configuration

The application uses strongly-typed configuration in `appsettings.json`:

```json
{
  "AppSettings": {
    "FileUpload": {
      "MaxFileSizeInMB": 5,
      "AllowedImageExtensions": [".png", ".jpg", ".jpeg", ".webp"]
    },
    "Authentication": {
      "SessionTimeoutHours": 8,
      "SlidingExpiration": true
    }
  }
}
```

## 📊 Database Schema

### Key Tables
- **Users**: User accounts and roles
- **KaizenForms**: Improvement suggestions
- **DepartmentTargets**: Department goals and targets

## 🚀 Deployment

### Development
```bash
dotnet run --environment Development
```

### Production
```bash
dotnet publish -c Release
dotnet run --environment Production
```

## 🧪 Testing

The application includes comprehensive error handling and logging:

- **Request Logging**: All HTTP requests are logged with timing
- **Exception Handling**: Custom exceptions for better error management
- **Validation**: Model validation and business rule enforcement

## 🔒 Security Features

- **Authentication**: Cookie-based authentication
- **Authorization**: Role-based access control
- **Input Validation**: Model validation and sanitization
- **File Upload Security**: File type and size validation

## 📈 Performance

- **Async/Await**: All database operations are asynchronous
- **Connection Pooling**: EF Core connection management
- **Caching**: Static file caching
- **Compression**: Response compression for better performance

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable
5. Submit a pull request

## 📄 License

This project is licensed under the MIT License.

## 🆘 Support

For support and questions, please contact the development team or create an issue in the repository.

