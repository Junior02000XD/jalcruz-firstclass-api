# Migración de datos: Laravel (antiguo) → ASP.NET Core API (nuevo)

> ## ⚠️ OBSOLETO — no se sigue este procedimiento
>
> Se decidió con los interesados que **no se migra ningún dato**, ni del CRM ni
> de RRHH. El recambio es un proyecto nuevo de Railway con base vacía, poblada
> sólo por migraciones + seeder: ver **[RECAMBIO-RAILWAY.md](RECAMBIO-RAILWAY.md)**.
>
> Se conserva sólo como referencia del mapeo entre el esquema de Laravel y el de
> EF Core, por si alguna vez hace falta leer datos viejos.

La BD Postgres actual (creada por las migraciones de Laravel) ya tiene información.
El esquema nuevo de EF Core usa **los mismos nombres de tabla y columna en
`snake_case`** y **los mismos valores de enum** (`asistio`, `inscrito`, `bueno`…),
por lo que la mayoría de tablas se copian casi 1:1.

> **Regla de oro:** nunca migrar contra la BD de producción directamente.
> Trabaja sobre una copia/restore y valida antes de cualquier corte.

## Estrategia general

1. **Respaldar** la BD actual de Railway:
   ```bash
   pg_dump "$DATABASE_URL_VIEJA" -Fc -f backup_laravel.dump
   ```
2. **Levantar** una BD nueva (local o un Postgres nuevo en Railway) y dejar que la
   API aplique sus migraciones al arrancar (crea el esquema nuevo vacío + roles + admin).
3. **Restaurar** el dump viejo en una BD aparte (ej. `jalcruz_old`) para leer de ahí.
4. **Copiar tabla por tabla** del esquema viejo al nuevo (ver mapa abajo), respetando
   el orden de dependencias (FKs).
5. **Validar** con conteos y muestreos.
6. **Cortar**: apuntar `VITE_API_URL` del frontend a la nueva API.

## Mapa de tablas

### Se copian directo (mismos nombres/columnas)

`cities`, `people`, `phones`, `entities`, `companies`, `work_areas`,
`worker_details`, `payrolls`, `attendances`, `products`, `zones`, `teachers`,
`campaigns`, `prospects`, `trial_classes`, `reminders`, `enrollments`.

Detalles:

- **Columnas de enum** (`worker_details.reliability`, `attendances.status`,
  `entities.type`, `prospects.status`, `trial_classes.status`): los valores string
  son idénticos a los de Laravel, se copian tal cual.
- **`created_at` / `updated_at`**: existen en ambos. En el esquema nuevo son
  `timestamp with time zone`; si en el viejo eran `timestamp`, Postgres los castea
  al copiar. No se pierden datos.
- **`attendances.extra_amount`**: ya existía (migración `add_extra_amount`), se copia.

Ejemplo de copia (con las dos BDs accesibles vía `dblink`/FDW, o exportando a CSV):

```sql
-- Orden respetando FKs: primero independientes, luego dependientes.
INSERT INTO cities      (id, name, created_at, updated_at)            SELECT id, name, created_at, updated_at FROM jalcruz_old.cities;
INSERT INTO people      (id, city_id, first_name, last_name, ci, ci_complement, email, birth_date, created_at, updated_at)
                        SELECT id, city_id, first_name, last_name, ci, ci_complement, email, birth_date, created_at, updated_at FROM jalcruz_old.people;
-- … companies, work_areas, worker_details, payrolls, attendances,
--    entities, zones, products, teachers, campaigns, prospects,
--    trial_classes, reminders, enrollments (mismo patrón) …

-- Reajustar las secuencias de identidad tras insertar IDs explícitos:
SELECT setval(pg_get_serial_sequence('people','id'), (SELECT MAX(id) FROM people));
-- … repetir por cada tabla con id …
```

> La forma más simple sin FDW: `pg_dump --data-only --table=<tabla>` de la vieja y
> `psql` a la nueva, tabla por tabla en orden de dependencias. Como los nombres
> coinciden, normalmente funciona sin transformación.

### Cambian (requieren atención)

| Vieja (Laravel) | Nueva (API) | Acción |
|-----------------|-------------|--------|
| `users` (con `password` bcrypt de Laravel) | `users` (`password_hash`) | Copiar `password` → `password_hash`. **Los hashes bcrypt de Laravel son compatibles con BCrypt.Net**, los logins siguen funcionando. |
| `roles`, `model_has_roles` (Spatie) | `roles` (sembrada), `user_roles` | Los roles "Super Admin/HR Admin/CRM Admin" ya se siembran. Reconstruir asignaciones: por cada fila de `model_has_roles` (model_type=User), insertar en `user_roles` el `user_id` + `role_id` correspondiente por nombre. |
| `permissions`, `model_has_permissions`, `role_has_permissions` | — | No se migran. El nuevo RBAC es por rol, sin permisos granulares. |
| `personal_access_tokens` (Sanctum) | — | No se migra. Con JWT los tokens se re-emiten al hacer login. |
| `cache`, `jobs`, `sessions` | — | Infraestructura de Laravel, no aplica. |

Asignación de roles de ejemplo:

```sql
INSERT INTO user_roles (user_id, role_id)
SELECT mhr.model_id, r.id
FROM jalcruz_old.model_has_roles mhr
JOIN jalcruz_old.roles old_r ON old_r.id = mhr.role_id
JOIN roles r ON r.name = old_r.name
WHERE mhr.model_type LIKE '%User';
```

## Validación post-migración

```sql
-- Conteos deben coincidir entre vieja y nueva.
SELECT 'people' t, count(*) FROM people
UNION ALL SELECT 'payrolls', count(*) FROM payrolls
UNION ALL SELECT 'attendances', count(*) FROM attendances
UNION ALL SELECT 'prospects', count(*) FROM prospects;
```

- Verificar que un usuario real pueda hacer login (hash bcrypt compatible).
- Abrir una planilla con asistencias y exportar a Excel.
- Revisar el embudo (`/api/reports/funnel`) contra los estados reales.

Una vez validado en la BD/entorno nuevo, recién entonces apuntar el frontend.
