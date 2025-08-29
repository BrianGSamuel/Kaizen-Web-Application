using KaizenWebApp.Extensions;
using KaizenWebApp.Middleware;
using KaizenWebApp.Data;
using KaizenWebApp.Models;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();

// Add localization with explicit resource path
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// Configure localization options
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[]
    {
        new CultureInfo("en"),
        new CultureInfo("si"),
        new CultureInfo("ta")
    };

    options.DefaultRequestCulture = new RequestCulture("en");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    
    // Add culture providers
    options.RequestCultureProviders.Clear();
    options.RequestCultureProviders.Add(new CookieRequestCultureProvider());
});

// Add application services using extension methods
builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddCustomAuthentication(builder.Configuration);
builder.Services.AddCustomCors();

var app = builder.Build();

// Initialize database and seed admin user
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var seedService = scope.ServiceProvider.GetRequiredService<DatabaseSeedService>();
    
    // Ensure database is created
    context.Database.EnsureCreated();
    
    // Seed the database with initial data
    await seedService.SeedDatabaseAsync();
}

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Add custom middleware
app.UseRequestLogging();
app.UseMiddleware<SystemMaintenanceMiddleware>();

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Add localization middleware
app.UseRequestLocalization();

// Add CORS
app.UseCors("AllowSpecificOrigin");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=LandingPage}/{id?}");

app.Run();
