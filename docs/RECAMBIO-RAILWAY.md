# Día del recambio: proyecto nuevo en Railway, base vacía

Procedimiento para reemplazar el proyecto viejo de Railway por uno nuevo con la
API .NET y una base **vacía**, poblada sólo por migraciones + seeder.

**No se migra ningún dato** — ni del CRM ni de RRHH. Decisión tomada con los
interesados. `docs/MIGRACION_DATOS.md` quedó obsoleto por eso.

Todo lo de acá se ejecuta a mano, en orden. Cada paso dice cómo verificar antes
de seguir al siguiente.

---

## 0. Antes de tocar nada

- [ ] **Desconectar el auto-deploy del proyecto viejo.** Si sigue conectado a
      `main`, el primer push despliega la API nueva contra la base vieja. Como
      el historial de migraciones se consolidó en una sola `InitialCreate`, esa
      migración intenta crear tablas que ya existen: el arranque falla y la API
      vieja queda caída.
- [ ] Avisar a quien esté usando el panel: hay corte y **los datos no vuelven**.
- [ ] Si querés quedarte con una copia de lo viejo por las dudas (no se
      restaura a ningún lado, es sólo por si acaso):
      `pg_dump "$DATABASE_URL_VIEJA" -Fc -f respaldo_final.dump`

## 1. Crear el proyecto nuevo

En Railway: **New Project** → agregarle dos servicios.

1. **PostgreSQL** (plantilla de Railway).
2. **La API**: *Deploy from GitHub repo* → `jalcruz-firstclass-api`, rama `main`.
   Railway detecta el `Dockerfile` y lo usa. El `ENTRYPOINT` ya escucha en el
   `PORT` que inyecta Railway.

Enlazar el Postgres al servicio de la API para que aparezca `DATABASE_URL`.

## 2. Variables de entorno del servicio API

| Variable | Valor | Notas |
|---|---|---|
| `DATABASE_URL` | *(la pone Railway)* | Al enlazar el Postgres. La API traduce el formato `postgres://` a Npgsql sola |
| `Jwt__Key` | **clave nueva, ≥32 caracteres** | No reutilizar la del proyecto viejo |
| `Jwt__Issuer` | `jalcruz-firstclass-api` | |
| `Jwt__Audience` | `jalcruz-firstclass-web` | |
| `Jwt__ExpiryHours` | `12` | |
| `Cors__AllowedOrigins` | dominio del frontend | Separados por coma. Sin esto el panel no puede llamar a la API |
| `Seed__AdminEmail` | correo del Super Admin | |
| `Seed__AdminPassword` | **contraseña fuerte** | Cambiarla después del primer login |
| `Seed__CrmUserName` | `Susanne` | |
| `Seed__CrmUserEmail` | correo de mamá | |
| `Seed__CrmUserPassword` | **contraseña fuerte** | |

Las tres `Seed__CrmUser*` son las que crean la cuenta que recibe las
conversaciones derivadas por la IA (`prospects.assigned_to_user_id`). **Si falta
el correo o la contraseña, la cuenta no se crea** — es a propósito, para que no
exista una cuenta con contraseña por defecto. Se puede agregar después: al
definirlas y reiniciar, el seeder la crea.

Ojo con la sintaxis: **doble guión bajo** (`Jwt__Key`), que es como .NET lee
secciones anidadas desde el entorno.

## 3. Primer despliegue = migraciones + seeder

No hay paso manual de migraciones: la API corre `Database.MigrateAsync()` y el
seeder al arrancar. Sobre una base vacía crea las 21 tablas, los 3 roles y las
2 cuentas.

Verificar en **Deploy Logs**:

```
Applying migration '20260802012802_InitialCreate'.
Super Admin inicial creado: <correo>
Usuario de CRM creado: <correo>
```

Y contra la base (pestaña *Data* del Postgres, o `psql`):

```sql
SELECT migration_id FROM "__EFMigrationsHistory";   -- 1 fila: InitialCreate
SELECT u.email, r.name FROM users u
  JOIN user_roles ur ON ur.user_id = u.id
  JOIN roles r ON r.id = ur.role_id;                -- Super Admin + CRM Admin
```

Reiniciar el servicio no duplica nada: el seeder es idempotente (verificado).

## 4. Verificar la API

```bash
API=https://<tu-servicio>.up.railway.app

curl -s $API/health                       # {"status":"healthy"}

TOKEN=$(curl -s -X POST $API/api/login -H 'Content-Type: application/json' \
  -d '{"email":"<admin>","password":"<clave>"}' | jq -r .access_token)

curl -s -H "Authorization: Bearer $TOKEN" $API/api/prospects   # [] en base vacía
```

Si `/health` responde pero el login da 500, mirar los logs: casi siempre es
`Jwt__Key` ausente o de menos de 32 caracteres.

## 5. Usuario de servicio para n8n

El agente necesita su **propia cuenta**, distinta de la de Susanne: si n8n
entrara con la de ella, no se podría distinguir lo que hace el bot de lo que
hace la persona, y el `assigned_to_user_id` del hand-off perdería sentido.

No hay endpoint de alta directa; son dos llamadas:

```bash
# 1. Registrar (endpoint público, queda SIN roles y por lo tanto sin acceso)
curl -s -X POST $API/api/register -H 'Content-Type: application/json' \
  -d '{"name":"Agente n8n","email":"n8n@firstclass...","password":"<clave larga>"}'

# 2. Asignarle el rol, como Super Admin
curl -s -X POST $API/api/users/<id>/roles -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/json' -d '{"roles":["CRM Admin"]}'
```

Después, cargar ese correo y contraseña en las credenciales de n8n.

> **Nota de seguridad:** `POST /api/register` es público. Un desconocido puede
> crearse una cuenta, aunque queda sin roles y sin acceso a nada hasta que un
> Super Admin se los asigne. Si molesta, es un `[Authorize]` de una línea —
> pero entonces las altas pasan a depender de un admin logueado.

## 6. Frontend

En Cloudflare Pages, apuntar la build a la API nueva:

- `VITE_API_URL` = `https://<tu-servicio>.up.railway.app/api`
  (la usa `src/api/axios.js`; **incluye el `/api`**).

Redesplegar y verificar el login desde el panel. Si el login falla con error de
CORS, falta el dominio del panel en `Cors__AllowedOrigins` de la API.

## 7. Dominios

Recién cuando lo anterior funcione:

1. Apuntar el dominio de la API al servicio nuevo (Railway → *Settings* →
   *Networking* → *Custom Domain*), y actualizar el DNS.
2. Ajustar `Cors__AllowedOrigins` y `VITE_API_URL` si cambian los dominios.
3. Actualizar la URL base de la API en los flujos de **n8n**.

## 8. Eliminar el proyecto viejo

Último paso, y **sólo** cuando el nuevo esté sirviendo de verdad:

- [ ] El panel entra y opera contra la API nueva.
- [ ] Los flujos de n8n apuntan al dominio nuevo y responden.
- [ ] Pasaron unos días de uso normal.

Recién ahí, borrar el proyecto viejo en Railway. Es irreversible: si no tomaste
el `pg_dump` del paso 0, esos datos no existen más.

---

## Verificado antes de escribir esto

Sobre un Postgres descartable y vacío, con estas mismas variables:

- `dotnet ef database update` aplica la única migración y crea las 21 tablas
  + `__EFMigrationsHistory`, con los 3 roles sembrados.
- Arrancar sólo la app contra una base **virgen** (sin paso de CLI, que es como
  lo hará Railway) crea todo igual: 22 tablas y las 2 cuentas.
- Login correcto con las dos cuentas, con sus roles (`Super Admin` / `CRM Admin`).
- Con el token de Susanne: `quick`, `by-phone`, `POST messages` (incluida la
  repetición idempotente → 200), los dos PATCH, el historial y `reminders`.
- Reinicio del servicio: no duplica usuarios.
