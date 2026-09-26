using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Infrastructure.Identity;
using TaneHesap.Infrastructure.Persistence;
using TaneHesap.Infrastructure.Persistence.Interceptors;
using TaneHesap.Infrastructure.Persistence.Seed;
using TaneHesap.Infrastructure.Services;

namespace TaneHesap.Infrastructure;

/// <summary>
/// Infrastructure katmanının DI kaydı: PostgreSQL (Npgsql) + EF Core, ASP.NET Core Identity,
/// JWT Bearer authentication, repository/unit of work ve domain servisleri.
/// bkz. Proje Raporu bölüm 8.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection tanımlı değil.");

        services.AddScoped<AuditSaveChangesInterceptor>();

        services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
            options.UseNpgsql(connectionString, npgsql =>
                    npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName))
                .AddInterceptors(serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>()));

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                // Şifre politikası — gerçek üretim öncesi işletme ihtiyacına göre sıkılaştırılabilir.
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = false;
                // Kaba kuvvet koruması: 5 hatalı denemede 15 dakika kilit (IdentityService.ValidatePasswordAsync uygular).
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        var jwtSection = configuration.GetSection("Jwt");
        var secret = jwtSection["Secret"] ?? throw new InvalidOperationException("Jwt:Secret appsettings içinde tanımlı değil.");

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSection["Issuer"] ?? "taneHesap",
                    ValidAudience = jwtSection["Audience"] ?? "taneHesap",
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                    ClockSkew = TimeSpan.FromSeconds(30)
                };

                // SignalR (WebSocket/SSE) bağlantılarında tarayıcı Authorization header'ı
                // gönderemeyebilir; bu yüzden hub bağlantılarında token query string üzerinden
                // ("?access_token=...") de kabul edilir. Sadece hub path'i için geçerlidir.
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;

                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<InitialSuperAdminSeeder>();

        return services;
    }
}
