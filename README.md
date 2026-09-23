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

## Build durumu

**Dört proje de (`Domain`, `Application`, `Infrastructure`, `API`) `dotnet restore` + `dotnet build`
ile hatasız derleniyor** (Rıdvan'ın kendi makinesinde, .NET 10 SDK ile doğrulandı). Kod, geliştirme
sürecinde NuGet.org erişiminin engellendiği bir sandbox ortamında yazıldı; bu yüzden `Domain` ve
`Application` katmanları bilinçli olarak NuGet paketlerinden bağımsız tutuldu ve o ortamda sürekli
derlenerek doğrulandı, `Infrastructure`/`API` katmanları ise (EF Core, Identity, JWT Bearer, Otp.NET,
Swashbuckle paketlerine ihtiyaç duydukları için) ancak gerçek bir makinede doğrulanabildi — bu adım
tamamlandı.

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

Uygulama açılışında `SuperAdmin` / `Admin` / `Employee` rolleri otomatik oluşturulur. İlk
SUPER_ADMIN de otomatik oluşturulabilir — ama güvenlik gereği kimlik bilgileri koda gömülmez:

1. `dotnet user-secrets set "InitialSuperAdmin:Username" "ridvan"` ve
   `dotnet user-secrets set "InitialSuperAdmin:Password" "GucluBirSifre123!"` ile (yerelde) veya
   Railway'de ortam değişkeni olarak (`InitialSuperAdmin__Username`, `InitialSuperAdmin__Password`)
   bu değerleri tanımlayın. `appsettings.json`'daki `InitialSuperAdmin` bölümü kasıtlı olarak boş
   bırakılmıştır — gerçek değerler asla git'e commit edilmemelidir.
2. Uygulamayı başlatın: `InitialSuperAdminSeeder` (bkz. `TaneHesap.Infrastructure/Persistence/Seed`)
   açılışta sistemde hiç SUPER_ADMIN yoksa ve bu iki değer tanımlıysa otomatik olarak ilk
   SUPER_ADMIN'i oluşturur; zaten bir SUPER_ADMIN varsa veya değerler boşsa hiçbir şey yapmaz
   (idempotent — her açılışta güvenle çalışır, tekrar tekrar kullanıcı oluşturmaz).
3. İlk girişte TOTP kurulumu gerekir (`POST /api/auth/totp-setup` — bkz. AuthController); SUPER_ADMIN
   ve ADMIN için 2FA zorunludur (bkz. bölüm 2).
4. Güvenlik için ilk kurulumdan sonra `InitialSuperAdmin:Username/Password` değerlerini ortamdan
   kaldırmanız önerilir — seeder zaten bir SUPER_ADMIN varken hiçbir şey yapmaz, ama gereksiz yere
   bir şifrenin ortam değişkeninde durması iyi bir pratik değildir.

## Uygulanan modüller (Faz 1-3 — vertical slice)

- Gün sonu kapanışında gelir açığı veya fazla malzeme tüketimi varsa ADMIN'lere "fire/açık"
  bildirimi gider; düzenli giderler için periyodik hatırlatma (bkz. "Arka plan görevleri").
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
  ve ödenmemiş giderleri döner.
- Notifications: ADMIN'lere in-app bildirim (düşük stok tetikleyicisi bağlı — StockMovements ve
  DailyClosing stok düşümü sonrası eşik altına inen malzemeler için otomatik bildirim üretir),
  okundu işaretleme. `IIdentityService.GetAdminsByBusinessAsync` bildirim alıcılarını bulur.
  Kalıcı kayıt (REST ile okunabilir) yanında `IRealtimeNotifier` soyutlaması üzerinden SignalR
  hub'ına (`/hubs/notifications`) anlık push da yapılır — bkz. bölüm "Gerçek zamanlı bildirimler".
- Platforms: paket servis platformu (Yemeksepeti, Getir vb.) + komisyon yüzdesi CRUD.
- DailySales: gün sonu satış satırlarını içe aktarır (`ExcelImportLog` ile), reçeteye göre o günün
  beklenen gelirini ve malzeme tüketimini hesaplar. **Not:** Excel şablonunun kesin kolon yapısı
  henüz netleşmediği için (bkz. `proje-raporu.md` bölüm 7/9) import şu an ayrıştırılmış satırları
  (JSON) kabul ediyor; şablon netleşince API katmanına ClosedXML/EPPlus ile doğrudan `.xlsx` kabul
  eden bir uç nokta eklenip aynı DTO'ya (`ImportDailySalesRequest`) bağlanması yeterli olacak.
- DailyClosing: ADMIN'in gün sonu gerçekleşen gelir + malzeme bazında gerçek tüketim girişini alır;
  stok düşümünü bu adımda kesinleştirir (`StockMovementType.SaleConsumption`, yeniden gönderimde
  önceki düşümü geri alıp yeniden uygular) ve DailySales'in beklediği değerlerle karşılaştırıp
  `DailyLossReport`'u (malzeme bazında ve tutar bazında fark) otomatik üretir/günceller.
- Reports: `GET /api/reports/period?fromDate=...&toDate=...` — aynı tarih verilirse günlük, geniş
  aralıkla haftalık/aylık rapor olur; toplam gelir/gider, nakit-kart kırılımı, dükkan içi/platform
  kırılımı, gider kategorisi bazlı toplam ve platform bazlı brüt/komisyon/net gelir döner.
- AuditLogs: `GET /api/audit-logs` — sadece görüntüleme; kayıtlar servislerin haberi olmadan,
  `AuditSaveChangesInterceptor` (EF Core `SaveChangesInterceptor`) tarafından her `SaveChanges`
  çağrısında değişen entity'lerden otomatik üretilir (bkz. "Audit log nasıl çalışıyor").

Kapsam dışı bırakılan/ertelenen tek nokta: Excel şablonunun kesin kolon yapısı netleşene kadar
gerçek `.xlsx` dosya yükleme uç noktası (yukarıda DailySales notuna bkz.) — bunun dışında planlanan
tüm modüller tamamlandı (bkz. "Platform komisyonu → otomatik gider" bölümü, en son eklenen parça).

## Platform komisyonu → otomatik gider (SOLID notu)

`DailySalesService.ImportAsync`, gün sonu içe aktarımı tamamlandıktan sonra `Platform` kanallı
satırların komisyonunu otomatik bir `Expense` kaydına dönüştürür. Bu iş `DailySalesService`'e
gömülmek yerine ayrı bir soyutlamaya (`TaneHesap.Application/Platforms/IPlatformCommissionExpensePoster`)
devredilir — satış içe aktarımı ile komisyon/gider dönüşümü birbirinden bağımsız iki sorumluluktur
(Single Responsibility), `DailySalesService` şişmez.

`PlatformCommissionExpensePoster`:
- İlgili tarihlerdeki `Platform` kanallı satış satırlarını platform+tarih bazında gruplayıp brüt
  tutarı toplar, `Platform.CommissionPercentage` ile komisyon tutarını hesaplar.
- Her platform için otomatik bir `ExpenseType` (`"Platform Komisyonu - {platform adı}"`,
  kategori `Other`) get-or-create eder — elle tür tanımlamaya gerek kalmaz.
- Platform+tarih başına tek bir `Expense` kaydını **idempotent** şekilde oluşturur/günceller (aynı
  günün verisi tekrar içe aktarılırsa yeni satır değil, mevcut tutar güncellenir).

## Audit log nasıl çalışıyor (SOLID notu)

Denetim kaydı, her serviste tek tek `AuditLog` eklemek yerine (bu hem tekrar/"spagetti" hem de
unutulmaya açık olurdu) **tek bir yerde**, `TaneHesap.Infrastructure/Persistence/Interceptors/AuditSaveChangesInterceptor.cs`
içinde toplanır:

- `SaveChangesInterceptor`'ı miras alır, `ApplicationDbContext`'e `AddInterceptors(...)` ile bağlanır.
- Her `SaveChanges`'ten önce `ChangeTracker`'daki Added/Modified/Deleted entity'leri tarar (AuditLog,
  Notification, RefreshToken hariç) ve otomatik `AuditLog` satırı üretir.
- Modified durumda **sadece gerçekten değişen alanları** loglar (tüm satırı değil) — gereksiz büyüme olmaz.
- Servisler (ExpenseService, DishService vb.) audit logging'in var olduğunu bile bilmez — Single
  Responsibility. Yeni bir entity eklendiğinde otomatik olarak denetime dahil olur — Open/Closed.

## Gerçek zamanlı bildirimler (SignalR)

`INotificationService`, bir bildirim oluşturduğunda hem veritabanına yazar hem de `IRealtimeNotifier`
soyutlaması üzerinden anlık iletmeyi dener (Dependency Inversion — Application katmanı SignalR'ı
bilmez). Somut implementasyon (`SignalRRealtimeNotifier`, composition root olan API katmanında) bir
SignalR hub'ı (`Hubs/NotificationsHub`, `/hubs/notifications`) üzerinden `Clients.User(userId)` ile
sadece ilgili ADMIN'e "ReceiveNotification" mesajı gönderir. JWT, WebSocket/SSE bağlantılarında
`?access_token=...` query string üzerinden de kabul edilir (tarayıcı bu bağlantılarda Authorization
header'ı gönderemeyebilir). Bir alıcı o an bağlı değilse anlık iletim sessizce atlanır; bildirim
veritabanında kalır ve `GET /api/notifications` ile her zaman okunabilir.

## Yayına alma (Railway)

Repo kökündeki `Dockerfile` ile Railway servisi doğrudan oluşturulur (Railway Dockerfile'ı
otomatik algılar). Aynı projeye bir PostgreSQL eklentisi ekleyip API servisinde şu ortam
değişkenlerini tanımlayın:

| Değişken | Değer |
|---|---|
| `DATABASE_URL` | Railway PostgreSQL'in `DATABASE_URL` referansı — uygulama bunu Npgsql bağlantı dizesine çevirir (`Extensions/DatabaseUrl.cs`). |
| `Jwt__Secret` | En az 32 karakterlik rastgele değer (`openssl rand -base64 48`). Üretimde varsayılan değerle uygulama açılmaz. |
| `Cors__AllowedOrigins__0` | Frontend adresi, örn. `https://tanehesap.vercel.app` |
| `Database__MigrateOnStartup` | `true` — bekleyen EF Core migration'ları açılışta uygulanır. |
| `InitialSuperAdmin__Username` / `__Password` / `__FullName` | İlk kurulumda SUPER_ADMIN için; oluştuktan sonra silinebilir. |

`PORT` Railway tarafından verilir ve `Program.cs` tarafından okunur; TLS Railway'de sonlanır
(`UseForwardedHeaders` gerçek şemayı alır). SignalR için CORS politikası `AllowCredentials` içerir,
bu yüzden `Cors__AllowedOrigins` mutlaka tam adres olmalıdır (`*` olamaz).

## Kasa, kartlar ve çalışan cüzdanı (şema değişikliği — migration gerekir)

- **Kasa (`Application/Treasury`, `/api/treasury`):** nakit kasası, kart kasası (banka) ve kredi kartları.
  Tek doğruluk kaynağı `TreasuryTransaction` defteridir; bakiyeler toplamdan türetilir. Satış geliri
  (`SalesTreasuryPoster`: nakit → nakit kasası, kart → kart kasası brüt) ve gider ödemeleri
  (`ExpenseTreasuryPoster`: Cash → nakit, Bank → kart kasası, Card → kartın limiti) otomatik yazılır.
- **Her para çıkışı bir `Expense`'tir (`IAutoExpenseWriter`):** platform komisyonu, dükkan içi kart satışlarının
  banka komisyonu (`CardFeeExpensePoster`, `Business.CardFeePercentage`), düzenli gider dönem ödemesi ve
  tedarikçi ödemesi otomatik gider olarak yazılır (`Expense.SourceReferenceType/Id` ile kaynak başına tek kayıt,
  idempotent). Böylece raporlar, tabak başı maliyet ve kasa aynı kanaldan beslenir; otomatik giderler Giderler
  ekranından düzenlenemez/silinemez (kaynağından yönetilir). `MarkPeriodPaidRequest` ve
  `CreateSupplierPaymentRequest` artık `PaymentMethod` + `PaymentCardId` alır.
  Transfer, kart borcu ödemesi ve düzeltme ADMIN tarafından elle girilir. Kart limiti `PaymentCard.Limit`
  (elle değiştirilir), kullanılabilir limit = limit + Σ kart hareketleri.
- **Gider ödeme şekli:** `PaymentMethod` enum'una `Bank = 2` eklendi; `Card` seçilince `PaymentCardId`
  zorunlu. Gider türü kategorisi `Personnel` ise `EmployeeUserId` verilebilir (cüzdandan düşer).
  `ExpenseCategory.FixedExpense` (1) **kaldırıldı** — sabit giderler Düzenli Giderler'de; eski satırlar
  açılışta `LegacyDataFixups` ile `Other`'a taşınır (idempotent).
- **Çalışan cüzdanı (`Application/Employees/EmployeeWalletService`):** `EmployeeProfile.HourlyWage`,
  `EmployeeWorkLog` (saat × ücret = hak ediş), ödeme = Personel kategorisinde otomatik "Personel Ödemesi"
  gider türüyle `Expense`. Bakiye = Σ hak ediş − Σ ödeme. EMPLOYEE `GET /api/me/wallet` ile sadece görür.
- **Satıştan otomatik stok düşümü (`SalesStockConsumptionPoster`):** satış içe aktarımında reçeteye göre
  `SaleConsumption` hareketi (gün bazında idempotent, `StockMovement.SourceDate`). Gün sonu kapanışında
  yalnızca gerçek − beklenen **farkı** `Waste`/`ManualAdjustment` olarak işlenir.
- **Aylık rapor (`Application/Reports/MonthlyReportService`, `GET /api/reports/monthly?year&month`):**
  toplam gider ÷ satılan tabak = tabak başı genel maliyet; malzeme başına gelir (₺/birim) önceki aya göre
  %10'dan fazla düşerse uyarı. `MonthlyReportJob` günde bir çalışır, biten ayı `MonthlyReport` kaydıyla
  kapatır ve ADMIN'lere `MonthlyReport` bildirimi gönderir.
- Satış verisi değişince çalışan türetilmiş kayıtlar `IDailySalesSideEffect` ile takılıdır
  (komisyon gideri, stok düşümü, kasa geliri) — yeni kural = yeni sınıf + DI kaydı.

Migration (Rıdvan'ın makinesinde, NuGet erişimi gerekir):

```bash
dotnet ef migrations add TreasuryWalletsMonthlyReport --project src/TaneHesap.Infrastructure --startup-project src/TaneHesap.API
dotnet ef database update --project src/TaneHesap.Infrastructure --startup-project src/TaneHesap.API
```

Railway `Database__MigrateOnStartup=true` olduğu için `main`'e push sonrası üretimde otomatik uygulanır.

## Arka plan görevleri

`BackgroundJobs/RecurringExpenseReminderJob` açılıştan 1 dk sonra ve her 6 saatte bir, dönemi bitmiş
ama ödenmemiş düzenli giderler için ADMIN'lere bildirim üretir (aynı dönem için tekrar göndermez).
`BackgroundJobs/MonthlyReportJob` açılıştan 2 dk sonra ve günde bir, biten ayın raporunu kapatıp bildirir.
HTTP isteği olmadığı için kendi DI scope'unda `SystemExecutionScope.EnterSystemMode()` ile çalışırlar
— tenant sorgu filtresi bu scope'ta tüm işletmeleri kapsar.

## Sonraki adımlar

- ~~`dotnet restore` + `dotnet build` ile Infrastructure/API katmanlarını doğrulayın.~~ ✅ Tamamlandı.
- ~~Platform komisyonunu gün sonu içe aktarımında otomatik `Expense` kaydına dönüştürün.~~ ✅ Tamamlandı.
- İlk migration'ı oluşturup PostgreSQL'e uygulayın.
- `InitialSuperAdmin:Username/Password` değerlerini tanımlayıp uygulamayı başlatarak ilk SUPER_ADMIN'i
  otomatik oluşturtun (bkz. "İlk SUPER_ADMIN kullanıcısını oluşturma").
- Excel şablonu netleşince DailySales importuna gerçek `.xlsx` yükleme uç noktası ekleyin.
- Frontend (React) projesini aynı repoya, `frontend/` klasörü altına ekleyin (SignalR bağlantısı
  için `@microsoft/signalr` paketiyle `/hubs/notifications`'a bağlanılabilir).
