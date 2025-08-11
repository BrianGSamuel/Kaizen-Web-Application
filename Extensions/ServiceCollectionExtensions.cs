using KaizenWebApp.Configuration;
using KaizenWebApp.Data;
using KaizenWebApp.Services;
using Microsoft.EntityFrameworkCore;

namespace KaizenWebApp.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Configure strongly typed settings
            services.Configure<AppSettings>(configuration.GetSection("AppSettings"));

            // Add DbContext
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlServer(
                    configuration.GetConnectionString("Default"),
                    sqlOptions => sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null));
            });

            // Register application services
            services.AddScoped<IKaizenService, KaizenService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IFileService, FileService>();

            // Add HTTP context accessor for accessing HttpContext in services
            services.AddHttpContextAccessor();

            return services;
        }

        public static IServiceCollection AddCustomAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            var authSettings = configuration.GetSection("AppSettings:Authentication").Get<AuthenticationSettings>();

            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.LoginPath = authSettings?.LoginPath ?? "/Account/Login";
                    options.AccessDeniedPath = authSettings?.AccessDeniedPath ?? "/Home/AccessDenied";
                    options.ExpireTimeSpan = TimeSpan.FromHours(authSettings?.SessionTimeoutHours ?? 8);
                    options.SlidingExpiration = authSettings?.SlidingExpiration ?? true;
                    options.Cookie.HttpOnly = true;
                    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                });

            return services;
        }

        public static IServiceCollection AddCustomCors(this IServiceCollection services)
        {
            services.AddCors(options =>
            {
                options.AddPolicy("AllowSpecificOrigin",
                    builder =>
                    {
                        builder.WithOrigins("http://localhost:5000", "https://localhost:5001")
                               .AllowAnyMethod()
                               .AllowAnyHeader()
                               .AllowCredentials();
                    });
            });

            return services;
        }
    }
}
