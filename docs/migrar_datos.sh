#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────
# Migración de datos: BD vieja (Laravel) → BD nueva (ASP.NET Core API)
#
# Copia SOLO lectura de la vieja. No la modifica. Seguro para producción.
#
# REQUISITOS:
#   sudo apt install postgresql-client      # trae pg_dump y psql
#
# USO:
#   1. En Railway, copia la "Postgres Connection URL" PÚBLICA de cada BD:
#        - OLD_URL = la del Postgres que usa el Laravel (system) -> tiene los datos
#        - NEW_URL = la del Postgres nuevo que usa la API nueva  -> ya migrada/vacía
#   2. Pégalas abajo (o expórtalas como variables de entorno).
#   3. La API nueva ya debió arrancar al menos una vez para crear el esquema
#      (tablas + roles sembrados + admin). Verifícalo antes de correr esto.
#   4. bash docs/migrar_datos.sh
# ─────────────────────────────────────────────────────────────────────────
set -euo pipefail

OLD_URL="${OLD_URL:-postgresql://USER:PASS@HOST:PORT/DB_VIEJA}"
NEW_URL="${NEW_URL:-postgresql://USER:PASS@HOST:PORT/DB_NUEVA}"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

echo "==> 1/5 Tablas de dominio (mismos nombres y columnas, copia 1:1)"
# pg_dump ordena solo por dependencias (FKs), así que el orden es seguro.
pg_dump "$OLD_URL" --data-only --no-owner --no-privileges \
  -t cities -t companies -t entities -t zones -t products -t teachers -t campaigns \
  -t people -t phones -t work_areas -t worker_details -t payrolls -t attendances \
  -t prospects -t trial_classes -t reminders -t enrollments \
  > "$TMP/dominio.sql"
psql "$NEW_URL" -v ON_ERROR_STOP=1 -f "$TMP/dominio.sql"

echo "==> 2/5 Usuarios (password -> password_hash; bcrypt de Laravel es compatible)"
psql "$OLD_URL" -v ON_ERROR_STOP=1 \
  -c "\copy (SELECT id, name, email, password, created_at, updated_at FROM users) TO '$TMP/users.csv' WITH CSV HEADER"
# Limpia los usuarios sembrados en la nueva (incl. el admin de seed) y carga los reales.
psql "$NEW_URL" -v ON_ERROR_STOP=1 <<SQL
TRUNCATE users RESTART IDENTITY CASCADE;
\copy users (id, name, email, password_hash, created_at, updated_at) FROM '$TMP/users.csv' WITH CSV HEADER
SQL

echo "==> 3/5 Asignación de roles (Spatie model_has_roles -> user_roles)"
psql "$OLD_URL" -v ON_ERROR_STOP=1 \
  -c "\copy (SELECT mhr.model_id AS user_id, r.name AS role_name FROM model_has_roles mhr JOIN roles r ON r.id = mhr.role_id WHERE mhr.model_type LIKE '%User') TO '$TMP/user_roles.csv' WITH CSV HEADER"
psql "$NEW_URL" -v ON_ERROR_STOP=1 <<SQL
CREATE TEMP TABLE _ur (user_id int, role_name text);
\copy _ur FROM '$TMP/user_roles.csv' WITH CSV HEADER
INSERT INTO user_roles (user_id, role_id)
SELECT _ur.user_id, r.id
FROM _ur JOIN roles r ON r.name = _ur.role_name
ON CONFLICT DO NOTHING;
SQL

echo "==> 4/5 Reajustar secuencias de identidad (para que los próximos INSERT no choquen)"
psql "$NEW_URL" -v ON_ERROR_STOP=1 <<'SQL'
DO $$
DECLARE t text;
BEGIN
  FOREACH t IN ARRAY ARRAY[
    'cities','companies','entities','zones','products','teachers','campaigns',
    'people','phones','work_areas','worker_details','payrolls','attendances',
    'prospects','trial_classes','reminders','enrollments','users'
  ] LOOP
    EXECUTE format(
      'SELECT setval(pg_get_serial_sequence(%L, ''id''), GREATEST((SELECT COALESCE(MAX(id),1) FROM %I), 1))',
      t, t);
  END LOOP;
END $$;
SQL

echo "==> 5/5 Validación (conteos en la BD nueva)"
psql "$NEW_URL" -v ON_ERROR_STOP=1 <<'SQL'
SELECT 'users' t, count(*) FROM users
UNION ALL SELECT 'people', count(*) FROM people
UNION ALL SELECT 'payrolls', count(*) FROM payrolls
UNION ALL SELECT 'attendances', count(*) FROM attendances
UNION ALL SELECT 'prospects', count(*) FROM prospects
UNION ALL SELECT 'user_roles', count(*) FROM user_roles
ORDER BY 1;
SQL

echo "✅ Migración terminada. Compara estos conteos con la BD vieja y prueba un login real."
