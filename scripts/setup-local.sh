#!/usr/bin/env bash
# taneHesap backend — yerel geliştirme ortamını tek komutla hazırlar:
#   1) PostgreSQL'de veritabanını oluşturur (yoksa),
#   2) dotnet user-secrets ile bağlantı dizesi + JWT secret + ilk SUPER_ADMIN bilgilerini tanımlar,
#   3) ilk EF Core migration'ını oluşturur (yoksa) ve veritabanına uygular.
#
# Neden bir script: appsettings.json'a gerçek şifre/secret yazmak yerine (git'e commit edilme
# riski), .NET'in kendi "user-secrets" mekanizmasını kullanmak — bu script sadece o adımları
# tekrarlanabilir/idempotent hale getiriyor. NuGet paketleri (EF Core, Identity, JWT, Npgsql)
# yalnızca gerçek bir makinede restore edilebiliyor (bkz. README — bu depoyu yazan sandbox
# ortamının NuGet.org erişimi kısıtlı), bu yüzden bu adımların SENİN kendi makinende çalışması
# gerekiyor; script bunu kolaylaştırmak için var.
#
# Kullanım:
#   cd backend
#   ./scripts/setup-local.sh
#
# Ortam değişkenleriyle özelleştirme (hepsi opsiyonel, makul varsayılanlar var):
#   PGHOST, PGPORT, PGUSER, PGPASSWORD   — PostgreSQL bağlantı bilgileri (varsayılan: localhost:5432, postgres/postgres)
#   DB_NAME                              — oluşturulacak veritabanı adı (varsayılan: tanehesap)
#   SUPERADMIN_USERNAME                  — ilk SUPER_ADMIN kullanıcı adı (varsayılan: ridvan)
#   SUPERADMIN_PASSWORD                  — ilk SUPER_ADMIN şifresi (boşsa script güvenli rastgele bir şifre üretir ve ekranda gösterir)
#   SUPERADMIN_FULLNAME                  — ilk SUPER_ADMIN ad soyad (varsayılan: Rıdvan)

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BACKEND_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
API_PROJECT="$BACKEND_ROOT/src/TaneHesap.API"
INFRA_PROJECT="$BACKEND_ROOT/src/TaneHesap.Infrastructure"
MIGRATIONS_DIR="$INFRA_PROJECT/Migrations"

PGHOST="${PGHOST:-localhost}"
PGPORT="${PGPORT:-5432}"
PGUSER="${PGUSER:-postgres}"
PGPASSWORD="${PGPASSWORD:-postgres}"
DB_NAME="${DB_NAME:-tanehesap}"

SUPERADMIN_USERNAME="${SUPERADMIN_USERNAME:-ridvan}"
SUPERADMIN_FULLNAME="${SUPERADMIN_FULLNAME:-Rıdvan}"

info()  { printf '\n\033[1;34m==>\033[0m %s\n' "$1"; }
ok()    { printf '\033[1;32m✓\033[0m %s\n' "$1"; }
fail()  { printf '\033[1;31m✗ %s\033[0m\n' "$1" >&2; exit 1; }

# --- 0) Ön koşullar -----------------------------------------------------

command -v dotnet >/dev/null 2>&1 || fail ".NET SDK bulunamadı. https://dotnet.microsoft.com/download adresinden .NET 10 SDK kurun."
ok ".NET SDK bulundu ($(dotnet --version))."

if ! command -v psql >/dev/null 2>&1; then
  fail "psql (PostgreSQL istemcisi) bulunamadı. PostgreSQL kurulu değilse: brew install postgresql@16 && brew services start postgresql@16 (veya Postgres.app kullanın)."
fi
ok "psql bulundu."

export PGPASSWORD
if ! psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d postgres -c '\q' >/dev/null 2>&1; then
  fail "PostgreSQL'e bağlanılamadı (host=$PGHOST port=$PGPORT user=$PGUSER). Sunucunun çalıştığından ve PGUSER/PGPASSWORD değerlerinin doğru olduğundan emin olun (örn. PGUSER=$USER ./scripts/setup-local.sh)."
fi
ok "PostgreSQL'e bağlanıldı."

# --- 1) Veritabanı (yoksa oluştur, idempotent) --------------------------

info "Veritabanı kontrol ediliyor: $DB_NAME"
DB_EXISTS=$(psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname = '$DB_NAME'")
if [ "$DB_EXISTS" = "1" ]; then
  ok "Veritabanı '$DB_NAME' zaten var, atlanıyor."
else
  psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d postgres -c "CREATE DATABASE \"$DB_NAME\"" >/dev/null
  ok "Veritabanı '$DB_NAME' oluşturuldu."
fi

CONNECTION_STRING="Host=$PGHOST;Port=$PGPORT;Database=$DB_NAME;Username=$PGUSER;Password=$PGPASSWORD"

# --- 2) user-secrets: bağlantı dizesi + JWT secret + ilk SUPER_ADMIN ----

info "dotnet user-secrets hazırlanıyor ($API_PROJECT)"
( cd "$API_PROJECT" && dotnet user-secrets init >/dev/null 2>&1 || true )

JWT_SECRET=$(openssl rand -base64 48)

if [ -z "${SUPERADMIN_PASSWORD:-}" ]; then
  SUPERADMIN_PASSWORD=$(openssl rand -base64 18 | tr -d '/+=' | cut -c1-16)
  GENERATED_PASSWORD=1
else
  GENERATED_PASSWORD=0
fi

( cd "$API_PROJECT" \
  && dotnet user-secrets set "ConnectionStrings:DefaultConnection" "$CONNECTION_STRING" >/dev/null \
  && dotnet user-secrets set "Jwt:Secret" "$JWT_SECRET" >/dev/null \
  && dotnet user-secrets set "InitialSuperAdmin:Username" "$SUPERADMIN_USERNAME" >/dev/null \
  && dotnet user-secrets set "InitialSuperAdmin:Password" "$SUPERADMIN_PASSWORD" >/dev/null \
  && dotnet user-secrets set "InitialSuperAdmin:FullName" "$SUPERADMIN_FULLNAME" >/dev/null )

ok "user-secrets tanımlandı (bağlantı dizesi, JWT secret, ilk SUPER_ADMIN bilgileri)."

# --- 3) Migration (yoksa oluştur) + veritabanına uygula ------------------

info "dotnet-ef aracı kontrol ediliyor"
if ! dotnet tool list --global 2>/dev/null | grep -q dotnet-ef; then
  dotnet tool install --global dotnet-ef >/dev/null
  ok "dotnet-ef global aracı kuruldu."
else
  ok "dotnet-ef zaten kurulu."
fi

info "Paketler geri yükleniyor (dotnet restore)"
( cd "$BACKEND_ROOT" && dotnet restore >/dev/null )
ok "Restore tamamlandı."

HAS_MIGRATION=$(find "$MIGRATIONS_DIR" -maxdepth 1 -iname "*.cs" 2>/dev/null | wc -l | tr -d ' ')
if [ "$HAS_MIGRATION" = "0" ]; then
  info "İlk migration oluşturuluyor (InitialCreate)"
  ( cd "$BACKEND_ROOT" && dotnet ef migrations add InitialCreate \
      --project "$INFRA_PROJECT" --startup-project "$API_PROJECT" )
  ok "Migration oluşturuldu — bunu git'e commit etmeyi unutma."
else
  ok "Migration zaten var, oluşturma adımı atlanıyor."
fi

info "Veritabanı güncelleniyor (dotnet ef database update)"
( cd "$BACKEND_ROOT" && dotnet ef database update \
    --project "$INFRA_PROJECT" --startup-project "$API_PROJECT" )
ok "Veritabanı şeması güncel."

# --- Özet -----------------------------------------------------------------

echo
echo "======================================================================"
echo " Backend yerel ortam hazır."
echo "======================================================================"
echo " Veritabanı        : $DB_NAME (host=$PGHOST port=$PGPORT user=$PGUSER)"
echo " SUPER_ADMIN        : $SUPERADMIN_USERNAME"
if [ "$GENERATED_PASSWORD" = "1" ]; then
echo " SUPER_ADMIN şifresi : $SUPERADMIN_PASSWORD   (rastgele üretildi — bir yere not et)"
else
echo " SUPER_ADMIN şifresi : (SUPERADMIN_PASSWORD ortam değişkeninden verdiğin değer)"
fi
echo
echo " Şimdi API'yi başlatabilirsin:"
echo "   dotnet run --project src/TaneHesap.API"
echo
echo " İlk girişte TOTP (authenticator) kurulumu gerekecek — POST /api/auth/totp-setup"
echo " (Swagger UI: http://localhost:5292/swagger)."
echo "======================================================================"
