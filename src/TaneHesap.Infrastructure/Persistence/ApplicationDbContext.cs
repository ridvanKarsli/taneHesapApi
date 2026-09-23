using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Domain.Common;
using TaneHesap.Domain.Entities;
using TaneHesap.Domain.Enums;
using TaneHesap.Infrastructure.Identity;

namespace TaneHesap.Infrastructure.Persistence;

/// <summary>
/// Uygulamanın EF Core DbContext'i. ASP.NET Core Identity ile birleşik çalışır.
/// ITenantEntity uygulayan tüm entity'lere otomatik business_id (multi-tenant) filtresi uygular;
/// SUPER_ADMIN için bu filtre devre dışı kalır. bkz. Proje Raporu bölüm 8.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    private readonly ICurrentUserService _currentUserService;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ICurrentUserService currentUserService)
        : base(options)
    {
        _currentUserService = currentUserService;
    }

    public DbSet<Business> Businesses => Set<Business>();
    public DbSet<ExpenseType> ExpenseTypes => Set<ExpenseType>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<Dish> Dishes => Set<Dish>();
    public DbSet<DishSize> DishSizes => Set<DishSize>();
    public DbSet<DishRecipeItem> DishRecipeItems => Set<DishRecipeItem>();
    public DbSet<Platform> Platforms => Set<Platform>();
    public DbSet<DailySalesEntry> DailySalesEntries => Set<DailySalesEntry>();
    public DbSet<ExcelImportLog> ExcelImportLogs => Set<ExcelImportLog>();
    public DbSet<DailyActualEntry> DailyActualEntries => Set<DailyActualEntry>();
    public DbSet<DailyActualConsumptionItem> DailyActualConsumptionItems => Set<DailyActualConsumptionItem>();
    public DbSet<DailyLossReport> DailyLossReports => Set<DailyLossReport>();
    public DbSet<DailyLossReportItem> DailyLossReportItems => Set<DailyLossReportItem>();
    public DbSet<RecurringExpense> RecurringExpenses => Set<RecurringExpense>();
    public DbSet<RecurringExpensePayment> RecurringExpensePayments => Set<RecurringExpensePayment>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<SupplierPurchase> SupplierPurchases => Set<SupplierPurchase>();
    public DbSet<SupplierPayment> SupplierPayments => Set<SupplierPayment>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PaymentCard> PaymentCards => Set<PaymentCard>();
    public DbSet<TreasuryTransaction> TreasuryTransactions => Set<TreasuryTransaction>();
    public DbSet<EmployeeProfile> EmployeeProfiles => Set<EmployeeProfile>();
    public DbSet<EmployeeWorkLog> EmployeeWorkLogs => Set<EmployeeWorkLog>();
    public DbSet<MonthlyReport> MonthlyReports => Set<MonthlyReport>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Identity tablolarını "Identity" şemasına, kendi tablolarımızı public şemada tutuyoruz (Postgres).
        builder.Entity<ApplicationUser>(b => b.ToTable("Users", "identity"));
        builder.Entity<IdentityRole<Guid>>(b => b.ToTable("Roles", "identity"));
        builder.Entity<IdentityUserRole<Guid>>(b => b.ToTable("UserRoles", "identity"));
        builder.Entity<IdentityUserClaim<Guid>>(b => b.ToTable("UserClaims", "identity"));
        builder.Entity<IdentityUserLogin<Guid>>(b => b.ToTable("UserLogins", "identity"));
        builder.Entity<IdentityUserToken<Guid>>(b => b.ToTable("UserTokens", "identity"));
        builder.Entity<IdentityRoleClaim<Guid>>(b => b.ToTable("RoleClaims", "identity"));

        // Tüm decimal alanlar için varsayılan precision (Postgres numeric).
        foreach (var property in builder.Model.GetEntityTypes()
                     .SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            property.SetColumnType("numeric(18,4)");
        }

        ConfigureRelationships(builder);
        ApplyTenantQueryFilters(builder);
    }

    private static void ConfigureRelationships(ModelBuilder builder)
    {
        // Çoklu kademeli (multiple cascade paths) silme hatalarını önlemek için ilişkili
        // koleksiyonlarda Restrict, sahibi olunan alt satırlarda (item) Cascade kullanıyoruz.
        builder.Entity<DishRecipeItem>()
            .HasOne(x => x.DishSize).WithMany(x => x.RecipeItems)
            .HasForeignKey(x => x.DishSizeId).OnDelete(DeleteBehavior.Cascade);

        builder.Entity<DishRecipeItem>()
            .HasOne(x => x.Ingredient).WithMany(x => x.RecipeItems)
            .HasForeignKey(x => x.IngredientId).OnDelete(DeleteBehavior.Restrict);

        builder.Entity<DailyActualConsumptionItem>()
            .HasOne(x => x.DailyActualEntry).WithMany(x => x.ConsumptionItems)
            .HasForeignKey(x => x.DailyActualEntryId).OnDelete(DeleteBehavior.Cascade);

        builder.Entity<DailyLossReportItem>()
            .HasOne(x => x.DailyLossReport).WithMany(x => x.Items)
            .HasForeignKey(x => x.DailyLossReportId).OnDelete(DeleteBehavior.Cascade);

        builder.Entity<SupplierPayment>()
            .HasOne(x => x.SupplierPurchase).WithMany(x => x.Payments)
            .HasForeignKey(x => x.SupplierPurchaseId).OnDelete(DeleteBehavior.Cascade);

        builder.Entity<RecurringExpensePayment>()
            .HasOne(x => x.RecurringExpense).WithMany(x => x.Payments)
            .HasForeignKey(x => x.RecurringExpenseId).OnDelete(DeleteBehavior.Cascade);

        // Kart geçmişi olan gider/kasa hareketleri varken kart silinemez (bkz. TreasuryService.DeleteCardAsync).
        builder.Entity<Expense>()
            .HasOne(x => x.PaymentCard).WithMany()
            .HasForeignKey(x => x.PaymentCardId).OnDelete(DeleteBehavior.Restrict);

        builder.Entity<TreasuryTransaction>()
            .HasOne(x => x.PaymentCard).WithMany()
            .HasForeignKey(x => x.PaymentCardId).OnDelete(DeleteBehavior.Restrict);

        builder.Entity<TreasuryTransaction>().HasIndex(x => new { x.BusinessId, x.TransactionDate });
        builder.Entity<Expense>().HasIndex(x => new { x.BusinessId, x.SourceReferenceType, x.SourceReferenceId });
        builder.Entity<StockMovement>().HasIndex(x => new { x.BusinessId, x.SourceDate });
        builder.Entity<EmployeeProfile>().HasIndex(x => new { x.BusinessId, x.UserId }).IsUnique();
        builder.Entity<MonthlyReport>().HasIndex(x => new { x.BusinessId, x.Year, x.Month }).IsUnique();

        // Business ile ilişkili tüm entity'lerde varsayılan davranış Restrict (işletme yanlışlıkla
        // silinirse tüm veri de silinmesin diye) — her entity ayrı ayrı burada listelenmek yerine
        // EF Core'un konvansiyonel FK davranışı Restrict'e çekiliyor.
        foreach (var fk in builder.Model.GetEntityTypes()
                     .SelectMany(e => e.GetForeignKeys())
                     .Where(fk => fk.PrincipalEntityType.ClrType == typeof(Business)))
        {
            fk.DeleteBehavior = DeleteBehavior.Restrict;
        }
    }

    /// <summary>
    /// ITenantEntity uygulayan her entity için otomatik business_id filtresi ekler.
    /// SUPER_ADMIN (BusinessId == null, Role == SuperAdmin) için filtre devre dışı kalır.
    /// </summary>
    private void ApplyTenantQueryFilters(ModelBuilder builder)
    {
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var method = typeof(ApplicationDbContext)
                .GetMethod(nameof(BuildTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(entityType.ClrType);

            var filter = method.Invoke(this, null);
            entityType.SetQueryFilter((LambdaExpression)filter!);
        }
    }

    private LambdaExpression BuildTenantFilter<TEntity>() where TEntity : class, ITenantEntity
    {
        Expression<Func<TEntity, bool>> filter = e =>
            _currentUserService.Role == UserRole.SuperAdmin || e.BusinessId == _currentUserService.BusinessId;

        return filter;
    }
}
