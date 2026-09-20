#!/usr/bin/env bash
# taneHesap backend — yerel geliştirme ortamını tek komutla hazırlar:
#   1) PostgreSQL'e nasıl bağlanılacağını otomatik tespit eder (Homebrew/Postgres.app/Docker gibi
#      farklı kurulumlarda kullanıcı adı/şifre farklı olabiliyor — bkz. detect_postgres_connection),
#   2) veritabanını oluşturur (yoksa),
#   3) dotnet user-secrets ile bağlantı dizesi + JWT secret + ilk SUPER_ADMIN bilgilerini tanımlar,
#   4) ilk EF Core migration'ını oluşturur (yoksa) ve veritabanına uygular.
#
# Neden bir script: appsettings.json'a gerçek şifre/secret yazmak yerine (git'e commit edilme
# riski), .NET'in kendi "user-secrets" mekanizmasını kullanmak — bu script sadece o adımları
# tekrarlanabilir/idempotent hale getiriyor. NuGet paketleri (EF Core, Identity, JWT, Npgsql)
# yalnızca NuGet.org'a erişimi olan gerçek bir makinede restore edilebiliyor, bu yüzden bu
# script'in SENİN kendi makinende çalışması gerekiyor.
#
# Kullanım:
#   cd backend
#   ./scripts/setup-local.sh
#
# Ortam değişkenleriyle özelleştirme (hepsi opsiyonel):
#   PGHOST, PGPORT, PGUSER, PGPASSWORD   — verilirse otomatik tespit atlanır, doğrudan bunlar kullanılır.
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

DB_NAME="${DB_NAME:-tanehesap}"
SUPERADMIN_USERNAME="${SUPERADMIN_USERNAME:-ridvan}"
SUPERADMIN_FULLNAME="${SUPERADMIN_FULLNAME:-Rıdvan}"

info()  { printf '\n\033[1;34m==>\033[0m %s\n' "$1"; }
ok()    { printf '\033[1;32m✓\033[0m %s\n' "$1"; }
warn()  { printf '\033[1;33m!\033[0m %s\n' "$1"; }
fail()  { printf '\033[1;31m✗ %s\033[0m\n' "$1" >&2; exit 1; }

# --- 0) Ön koşullar -----------------------------------------------------

command -v dotnet >/dev/null 2>&1 || fail ".NET SDK bulunamadı. https://dotnet.microsoft.com/download adresinden .NET 10 SDK kurun."
ok ".NET SDK bulundu ($(dotnet --version))."

command -v psql >/dev/null 2>&1 || fail "psql (PostgreSQL istemcisi) bulunamadı. Kurulu değilse: brew install postgresql@16 && brew services start postgresql@16 (veya Postgres.app kullanın)."
ok "psql bulundu."

# --- 1) PostgreSQL bağlantısını bul --------------------------------------
#
# Farklı yerel kurulumlarda varsayılan kimlik farklıdır: Homebrew/Postgres.app genelde mac
# kullanıcı adını şifresiz (peer/trust) superuser yapar, Docker/klasik kurulumlar genelde
# postgres/postgres kullanır. PGHOST/PGPORT/PGUSER/PGPASSWORD'ten biri elle verilmişse onu
# olduğu gibi kullanırız (otomatik tespiti atlarız); hiçbiri verilmemişse sırayla dener.

try_connect() {
  # $1=host ("" ise -h verilmez, unix socket kullanılır) $2=port $3=user $4=password ("" ise şifresiz denenir)
  # -w: ASLA interaktif şifre sormaz — yanlış/eksikse psql hemen (sessizce) başarısız olur. Bu
  # olmadan psql, terminalde çalışırken kafa karıştırıcı ve yanıltıcı bir şifre istemi gösterir
  # (denenen aday değil, kullanıcının o an elle girdiği şifre geçerli olur).
  local host="$1" port="$2" user="$3" pass="$4"
  local -a args=(-w -U "$user" -d postgres -tAc 'select 1')
  [ -n "$host" ] && args=(-h "$host" -p "$port" "${args[@]}")
  if [ -n "$pass" ]; then
    PGPASSWORD="$pass" psql "${args[@]}" >/dev/null 2>&1
  else
    ( unset PGPASSWORD; psql "${args[@]}" >/dev/null 2>&1 )
  fi
}

if [ -n "${PGHOST:-}${PGPORT:-}${PGUSER:-}${PGPASSWORD:-}" ]; then
  PG_HOST="${PGHOST:-localhost}"
  PG_PORT="${PGPORT:-5432}"
  PG_USER="${PGUSER:-postgres}"
  PG_PASSWORD="${PGPASSWORD:-}"
  info "PGHOST/PGPORT/PGUSER/PGPASSWORD elle verilmiş, otomatik tespit atlanıyor."
  try_connect "$PG_HOST" "$PG_PORT" "$PG_USER" "$PG_PASSWORD" \
    || fail "PostgreSQL'e bağlanılamadı (host=$PG_HOST port=$PG_PORT user=$PG_USER). Bilgileri kontrol edin."
  ok "PostgreSQL'e bağlanıldı (host=$PG_HOST port=$PG_PORT user=$PG_USER)."
else
  info "PostgreSQL bağlantısı otomatik tespit ediliyor… (sessizce dener, şifre sormaz)"
  OS_USER="$(whoami)"
  FOUND=0
  # Sırasıyla: unix socket + mevcut mac kullanıcısı (Homebrew/Postgres.app varsayılanı),
  # localhost + mevcut kullanıcı, postgres/postgres (Docker/klasik), postgres kullanıcısı şifresiz.
  CANDIDATES=(
    "::$OS_USER:"
    "localhost:5432:$OS_USER:"
    "localhost:5432:postgres:postgres"
    "localhost:5432:postgres:"
  )
  for candidate in "${CANDIDATES[@]}"; do
    IFS=':' read -r c_host c_port c_user c_pass <<< "$candidate"
    c_port="${c_port:-5432}"
    if try_connect "$c_host" "$c_port" "$c_user" "$c_pass"; then
      PG_HOST="$c_host"; PG_PORT="$c_port"; PG_USER="$c_user"; PG_PASSWORD="$c_pass"
      FOUND=1
      break
    fi
  done
  if [ "$FOUND" = "0" ] && [ -t 0 ]; then
    # Bilinen hiçbir kombinasyon çalışmadı — tahmin etmeye devam etmek yerine tek seferlik,
    # açık bir şekilde soruyoruz (sessiz varsayımlarla yanlış bir bağlantı dizesi üretmektense).
    warn "Bilinen kombinasyonlarla bağlanılamadı, elle soruluyor."
    read -r -p "PostgreSQL kullanıcı adı [postgres]: " ASK_USER
    ASK_USER="${ASK_USER:-postgres}"
    read -r -s -p "PostgreSQL şifresi (boşsa Enter): " ASK_PASS
    echo
    if try_connect "localhost" "5432" "$ASK_USER" "$ASK_PASS"; then
      PG_HOST="localhost"; PG_PORT="5432"; PG_USER="$ASK_USER"; PG_PASSWORD="$ASK_PASS"
      FOUND=1
    fi
  fi
  if [ "$FOUND" = "0" ]; then
    fail "PostgreSQL'e bağlanılamadı. Sunucunun çalıştığından emin olun (brew services list | grep postgresql)
    ve PGHOST/PGPORT/PGUSER/PGPASSWORD ortam değişkenleriyle doğru bilgileri verip tekrar deneyin, örn:
      PGUSER=postgres PGPASSWORD='gercek-sifren' ./scripts/setup-local.sh"
  fi
  ok "PostgreSQL'e bağlanıldı (host=${PG_HOST:-<unix socket>} port=$PG_PORT user=$PG_USER$( [ -z "$PG_PASSWORD" ] && echo ", şifresiz" ))."
fi

# --- 2) Veritabanı (yoksa oluştur, idempotent) --------------------------

info "Veritabanı kontrol ediliyor: $DB_NAME"
PSQL_ARGS=(-w -U "$PG_USER" -d postgres)
[ -n "$PG_HOST" ] && PSQL_ARGS=(-h "$PG_HOST" -p "$PG_PORT" "${PSQL_ARGS[@]}")
if [ -n "$PG_PASSWORD" ]; then export PGPASSWORD="$PG_PASSWORD"; else unset PGPASSWORD 2>/dev/null || true; fi

DB_EXISTS=$(psql "${PSQL_ARGS[@]}" -tAc "SELECT 1 FROM pg_database WHERE datname = '$DB_NAME'")
if [ "$DB_EXISTS" = "1" ]; then
  ok "Veritabanı '$DB_NAME' zaten var, atlanıyor."
else
  psql "${PSQL_ARGS[@]}" -c "CREATE DATABASE \"$DB_NAME\"" >/dev/null
  ok "Veritabanı '$DB_NAME' oluşturuldu."
fi

CONN_HOST="${PG_HOST:-localhost}"
CONNECTION_STRING="Host=$CONN_HOST;Port=$PG_PORT;Database=$DB_NAME;Username=$PG_USER"
[ -n "$PG_PASSWORD" ] && CONNECTION_STRING="$CONNECTION_STRING;Password=$PG_PASSWORD"

# --- 3) user-secrets: bağlantı dizesi + JWT secret + ilk SUPER_ADMIN ----

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

# --- 4) Migration (yoksa oluştur) + veritabanına uygula ------------------

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
echo " Veritabanı          : $DB_NAME (host=$CONN_HOST port=$PG_PORT user=$PG_USER)"
echo " SUPER_ADMIN          : $SUPERADMIN_USERNAME"
if [ "$GENERATED_PASSWORD" = "1" ]; then
echo " SUPER_ADMIN şifresi   : $SUPERADMIN_PASSWORD   (rastgele üretildi — bir yere not et)"
else
echo " SUPER_ADMIN şifresi   : (SUPERADMIN_PASSWORD ortam değişkeninden verdiğin değer)"
fi
echo
echo " Şimdi API'yi başlatabilirsin:"
echo "   dotnet run --project src/TaneHesap.API"
echo
echo " İlk girişte TOTP (authenticator) kurulumu gerekecek — POST /api/auth/totp-setup"
echo " (Swagger UI: http://localhost:5292/swagger)."
echo "======================================================================"
