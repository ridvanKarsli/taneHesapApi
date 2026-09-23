using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using TaneHesap.API.BackgroundJobs;
using TaneHesap.API.Extensions;
using TaneHesap.API.Hubs;
using TaneHesap.API.Middleware;
using TaneHesap.API.Services;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Infrastructure;
using TaneHesap.Infrastructure.Persistence;
using TaneHesap.Infrastructure.Persistence.Seed;

var builder = WebApplication.CreateBuilder(args);

// --- Barındırma (Railway): PORT ortam değişkeni ve DATABASE_URL desteği (bkz. README "Yayına alma") ---
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

DatabaseUrl.ApplyIfPresent(builder.Configuration);

// Üretimde varsayılan (git'teki) JWT secret ile açılmayı engelle — token'lar sahte imzalanabilirdi.
if (!builder.Environment.IsDevelopment() && (builder.Configuration["Jwt:Secret"] ?? "").Contains("CHANGE_ME"))
{
    throw new InvalidOperationException("Jwt:Secret üretim ortamında tanımlanmalı (ör. Jwt__Secret ortam değişkeni).");
}

// --- Servisler ---

builder.Services.AddControllers();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<SystemExecutionScope>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// SignalR: ADMIN'lere anlık in-app bildirim itmek için (bkz. Hubs/NotificationsHub,
// Services/SignalRRealtimeNotifier). Application katmanı IRealtimeNotifier soyutlamasını kullanır,
// somut SignalR implementasyonu composition root (API katmanı) olarak burada bağlanır.
builder.Services.AddSignalR();
builder.Services.AddScoped<IRealtimeNotifier, SignalRRealtimeNotifier>();

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructure(builder.Configuration);

// Düzenli gider hatırlatmalarını periyodik üreten arka plan görevi (bkz. BackgroundJobs/).
builder.Services.AddHostedService<RecurringExpenseReminderJob>();
builder.Services.AddHostedService<MonthlyReportJob>();

// React frontend (Vercel'de barındırılacak) için CORS — geliştirmede localhost, üretimde
// appsettings/ortam değişkeninden okunan origin. bkz. Proje Raporu bölüm 8.
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5173" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(corsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              // SignalR istemcisi negotiate isteğini kimlik bilgisiyle (withCredentials) gönderir;
              // bu olmadan tarayıcı CORS'ta reddeder ve anlık bildirimler hiç bağlanmaz.
              .AllowCredentials());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "taneHesap API", Version = "v1" });

    var jwtScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT access token'ı 'Bearer {token}' formatında girin."
    };
    options.AddSecurityDefinition("Bearer", jwtScheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() }
    });
});

var app = builder.Build();

// --- Rolleri (SuperAdmin/Admin/Employee) uygulama açılışında garantiye al ---
using (var scope = app.Services.CreateScope())
{
    // Üretimde (Railway) bekleyen EF Core migration'larını açılışta uygula — Database:MigrateOnStartup=true ile açılır.
    if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
    {
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();
    }

    // Veri düzeyinde geriye dönük düzeltmeler (idempotent) — örn. kaldırılan "Sabit gider" kategorisi.
    await LegacyDataFixups.ApplyAsync(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    foreach (var roleName in new[] { "SuperAdmin", "Admin", "Employee" })
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
        }
    }

    // İlk SUPER_ADMIN — sistemde hiç yoksa ve InitialSuperAdmin:Username/Password (appsettings/
    // user-secrets/ortam değişkeni) tanımlıysa oluşturulur; aksi halde no-op (bkz. InitialSuperAdminSeeder).
    var superAdminSeeder = scope.ServiceProvider.GetRequiredService<InitialSuperAdminSeeder>();
    await superAdminSeeder.SeedAsync();
}

// --- HTTP pipeline ---

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Railway/Vercel gibi TLS'i önde sonlandıran proxy'lerin arkasında gerçek şema/IP'yi kullan.
var forwardedHeaders = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedHeaders.KnownIPNetworks.Clear();
forwardedHeaders.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeaders);

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();

app.UseCors("Frontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<NotificationsHub>("/hubs/notifications");

app.Run();
