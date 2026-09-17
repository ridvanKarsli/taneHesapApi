# taneHesap — Backend

.NET 10 / ASP.NET Core Web API, katmanlı (layered) mimari ile. Kapsam ve karar geçmişi için
`proje-raporu.md` dosyasına bakın.

## Katmanlar

```
src/
  TaneHesap.Domain          Entity'ler, enum'lar — dış bağımlılık yok (saf C#).
  TaneHesap.Application     DTO'lar, servis arayüzleri/implementasyonları, repository arayüzleri.
                             Sadece Domain'e bağımlı; EF Core/Identity/JWT gibi altyapı paketlerine
                             bağımlı DEĞİL (composition root API katmanındadır).
  TaneHesap.Infrastructure  EF Core (PostgreSQL/Npgsql), ASP.NET Core Identity, JWT üretimi,
                             TOTP (Otp.NET), repository/unit of work implementasyonları.
  TaneHesap.API              Controller'lar, Program.cs (DI kaydı, middleware), appsettings.
```

Bağımlılık yönü: `API → Infrastructure → Application → Domain` (Domain hiçbir şeye bağımlı değil).

## Bu ortamda neyin doğrulandığı

Bu proje, NuGet.org erişiminin organizasyon politikasıyla engellendiği bir sandbox ortamında
hazırlandı. **TaneHesap.Domain ve TaneHesap.Application projeleri bu ortamda `dotnet build` ile
derlenip doğrulandı** (hiçbir NuGet paketine ihtiyaç duymazlar). **TaneHesap.Infrastructure ve
TaneHesap.API projeleri EF Core, ASP.NET Core Identity, JWT Bearer, Otp.NET ve Swashbuckle NuGet
paketlerine ihtiyaç duyduğundan burada restore/build edilemedi** — kodları dikkatle, aynı
pattern'leri kullanarak yazıldı ancak derleyici ile doğrulanmadı. İlk adım olarak localde
`dotnet restore` + `dotnet build` çalıştırıp çıkan hataları (çoğunlukla küçük paket versiyon
uyuşmazlıkları olması beklenir) gidermeniz gerekebilir.

## Kurulum (yerelde)

1. **PostgreSQL** kurun/çalıştırın, bir veritabanı oluşturun (örn. `tanehesap`).
2. `src/TaneHesap.API/appsettings.json` içindeki `ConnectionStrings:DefaultConnection` ve
   `Jwt:Secret` değerlerini güncelleyin (Secret için en az 32 karakterlik rastgele bir değer kullanın;
   gerçek değerleri git'e commit etmeyin — bkz. `dotnet user-secrets` kullanımı).
3. Paketleri geri yükleyin ve derleyin:
   ```bash
   dotnet restore
   dotnet build
   ```
4. İlk migration'ı oluşturun ve veritabanını güncelleyin:
   ```bash
   dotnet tool install --global dotnet-ef   # ilk seferde
   dotnet ef migrations add InitialCreate --project src/TaneHesap.Infrastructure --startup-project src/TaneHesap.API
   dotnet ef database update --project src/TaneHesap.Infrastructure --startup-project src/TaneHesap.API
   ```
5. API'yi çalıştırın:
   ```bash
   dotnet run --project src/TaneHesap.API
   ```
   Geliştirme ortamında Swagger UI `/swagger` altında açılır.

## İlk SUPER_ADMIN kullanıcısını oluşturma

Uygulama açılışında `SuperAdmin` / `Admin` / `Employee` rolleri otomatik oluşturulur, ancak
güvenlik gereği ilk SUPER_ADMIN kullanıcısı koda gömülmez. `IIdentityService.CreateAdminOrSuperAdminAsync`
metodunu bir kerelik bir seed script/endpoint ile çağırarak veya `dotnet ef` / küçük bir konsol
aracıyla ilk kullanıcıyı oluşturmanız gerekir — bu, projenin bir sonraki adımlarından biridir.

## Uygulanan modüller (Faz 1 — vertical slice)

- Auth: login (SUPER_ADMIN/ADMIN için TOTP zorunlu, EMPLOYEE için yok), refresh token (rotation),
  revoke, TOTP kurulumu.
- Businesses: SUPER_ADMIN için CRUD.
- Employees: ADMIN'in kendi işletmesine çalışan ekleme/güncelleme.
- ExpenseTypes: ADMIN yönetir, EMPLOYEE listeler.
- Expenses: ADMIN + EMPLOYEE girer/listeler.
- Ingredients: ADMIN yönetir (ad, birim, güncel birim fiyat, minimum stok eşiği), EMPLOYEE listeler;
  `GET /api/ingredients/below-threshold` düşük stoktaki malzemeleri döner.
- Dishes: ADMIN ürün (Dish), tabak boyu (DishSize) ve reçete (DishRecipeItem) tanımlar; tabak
  maliyeti (Cost) ve kâr marjı (ProfitMargin) reçete × güncel malzeme fiyatına göre otomatik hesaplanır.
- Stock (StockMovements): ADMIN manuel stok hareketi (sayım düzeltmesi, fire) girer, ADMIN+EMPLOYEE
  görüntüler; her hareket ilgili Ingredient.CurrentStockQuantity alanını otomatik günceller.
- Suppliers: ADMIN tedarikçi kartı, alış (SupplierPurchase) ve (kısmi olabilen) ödeme kaydeder.
  Alış kaydedilince otomatik olarak (1) malzeme stoğuna Purchase hareketi eklenir, (2) malzemenin
  güncel birim fiyatı bu alışla güncellenir; ödemeler toplamı tutara ulaşınca alış IsFullyPaid=true
  olur. `GET /api/suppliers/debt-summary` işletme genelinde ödenmemiş toplam borcu döner.
- RecurringExpenses: ADMIN kira/elektrik gibi periyodik giderleri (haftalık/aylık/yıllık) tanımlar;
  sistem bugüne göre güncel dönemi ve ödenip ödenmediğini otomatik hesaplar, `mark-period-paid` ile
  bir dönem ödendi işaretlenir. `GET /api/recurring-expenses/due-for-reminder` dönem sonuna gelmiş
  ve ödenmemiş giderleri döner (Notifications modülü bunu tüketecek).

Diğer modüller (gün sonu Excel içe aktarımı ve fire analizi, paket servis platform komisyonları,
raporlama, audit log middleware'i, SignalR bildirimleri) için Domain katmanındaki entity'ler ve
veritabanı şeması hazır; Application/Infrastructure/API katmanlarında aynı pattern (Repository +
Service + Controller) izlenerek eklenmesi gerekir — bkz. `proje-raporu.md` bölüm 3 ve 6 (fazlandırma).

## Sonraki adımlar

- `dotnet restore` + `dotnet build` ile Infrastructure/API katmanlarını doğrulayın.
- İlk migration'ı oluşturup PostgreSQL'e uygulayın.
- İlk SUPER_ADMIN kullanıcısını oluşturun.
- Faz 2/3 modüllerini (stok, gün sonu, tedarikçi, raporlama, SignalR bildirimleri, Excel içe
  aktarımı) aynı katmanlı pattern ile ekleyin.
- Frontend (React) projesini aynı repoya, `frontend/` klasörü altına ekleyin.
