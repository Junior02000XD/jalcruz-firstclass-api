# Jalcruz First Class API

API REST del CRM a medida de las dos empresas de la familia Cruz, reescrita en
**ASP.NET Core 10 (Web API)** para reemplazar el backend anterior en Laravel.
Mantiene el **mismo contrato JSON** (claves en `snake_case`, valores de enum como
`"asistio"`, `"inscrito"`, etc.) para que el frontend React (Cloudflare Pages) siga
funcionando solo cambiando la URL de la API.

Dos módulos sobre la misma base de datos PostgreSQL:

- **Jalcruz (RRHH):** empresas, áreas de trabajo, trabajadores, planillas, asistencias, export a Excel.
- **First Class (CRM):** campañas, prospectos, clases de prueba, profesores, embudo de conversión, ROI.

## Stack

| Pieza | Tecnología |
|------|------------|
| Runtime | .NET 10 / ASP.NET Core Web API (controllers) |
| ORM | Entity Framework Core 10 + Npgsql (PostgreSQL) |
| Naming | `snake_case` en tablas/columnas (espeja el esquema de Laravel → migración de datos directa) |
| Auth | JWT Bearer (reemplaza Sanctum) |
| Roles | Tablas propias `roles` / `user_roles` (reemplaza Spatie) |
| Excel | ClosedXML (reemplaza Maatwebsite/Excel) |
| Hashing | BCrypt |

## Estructura

```
Domain/        Enums.cs (mapas enum↔string), Entities.cs (modelo de dominio + RBAC)
Data/          AppDbContext.cs, DbSeeder.cs, DesignTimeDbContextFactory.cs
Dtos/          Dtos.cs (inputs de request validados con DataAnnotations)
Services/      JwtTokenService, PayrollExportService, EnumJsonConverters,
               PhoneNormalizer, R2StorageService
Controllers/   Auth, Users, People, Cities, Zones, Companies, WorkAreas,
               WorkerDetails, Payrolls, Attendances, Teachers, Campaigns,
               Prospects, TrialClasses, Reports, Reminders, Messages,
               MediaAssets, ContextEntries, Personas, AgentContext
Migrations/    InitialCreate (esquema base) + ContenidoDelAgente (Fase B)
Program.cs     DI, JWT, CORS, JSON snake_case, migración+seed al arrancar
```

## Endpoints (mismos paths que el Laravel original)

Público: `POST /api/login`. El resto —incluido `POST /api/register`, que es
sólo del Super Admin— va con `Authorization: Bearer <token>` y su rol.

| Recurso | Rol | Notas |
|---------|-----|-------|
| `people`, `cities`, `zones` | HR · CRM · Super | Núcleo compartido |
| `users`, `users/{id}/roles` | Super | Gestión de usuarios |
| `companies`, `work-areas`, `payrolls`, `attendances`, `worker-details` | HR · Super | Módulo Jalcruz |
| `payrolls/{id}/export` | HR · Super | Descarga `.xlsx` |
| `reports/payroll/{id}` | HR · Super | Total a pagar (incluye `extra_amount`) |
| `campaigns`, `prospects`, `trial-classes`, `teachers` | CRM · Super | Módulo First Class |
| `reminders`, `messages` | CRM · Super | Seguimientos e historial de chat |
| `context-entries`, `media`, `personas` | CRM · Super | Contenido que usa la IA para responder |
| `agent-context/{phoneNumberId}` | CRM · Super | Todo el contexto del agente en una llamada |
| `reports/funnel`, `reports/marketing-roi` | CRM · Super | Embudo y ROI |

Los que consume el agente de WhatsApp en n8n — con sus reglas de idempotencia,
que no son obvias — están en **[docs/WHATSAPP-CRM.md](docs/WHATSAPP-CRM.md)**:
`GET prospects/by-phone/{numero}`, `POST prospects/quick`,
`PATCH prospects/{id}/status`, `PATCH prospects/{id}/assignment`,
`POST messages`, `GET prospects/{id}/messages` y
`GET agent-context/{phoneNumberId}`.

> **Mejoras frente al Laravel original:** se corrigió el bug de `marketing-roi`
> (comparaba `'Inscrito'` con mayúscula y siempre daba 0 inscritos) y el
> `payroll/summary` ahora suma también `extra_amount`.

## Desarrollo local

Requisitos: .NET 10 SDK y un PostgreSQL local.

```bash
# 1. Postgres rápido con Docker
docker run -d --name jalcruz-pg -e POSTGRES_USER=postgres \
  -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=jalcruz_firstclass \
  -p 5432:5432 postgres:18-alpine

# 2. La cadena de conexión y credenciales de seed están en appsettings.Development.json
#    (admin@jalcruz.com / ChangeMe123!). Ejecuta:
dotnet run

# Al arrancar aplica migraciones y crea el Super Admin inicial.
```

Prueba rápida:

```bash
curl -s -X POST http://localhost:5080/api/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@jalcruz.com","password":"ChangeMe123!"}'
```

### Migraciones EF Core

```bash
dotnet ef migrations add NombreDelCambio   # tras modificar entidades
dotnet ef database update                   # aplicar manualmente (la app también lo hace al arrancar)
```

## Despliegue en Railway (recomendado: import por GitHub)

Mantén el flujo por **GitHub** (no CLI): deploy automático en cada push, atado a un
commit, fácil de revertir. El CLI (`railway up`/`railway logs`) queda para debug puntual.

1. Sube este repo a GitHub.
2. En Railway: **New Project → Deploy from GitHub repo** → selecciona este repo.
   Railway detecta el `Dockerfile` y construye la imagen.
3. Añade un servicio **PostgreSQL** en el mismo proyecto (o reutiliza el existente
   con los datos actuales — ver guía de migración).
4. En el servicio de la API → **Variables**, define (ver `.env.example`):
   - `DATABASE_URL` → referencia a la del Postgres (`${{Postgres.DATABASE_URL}}`)
   - `Jwt__Key` (clave nueva y larga), `Jwt__Issuer`, `Jwt__Audience`
   - `Cors__AllowedOrigins` → tu dominio de Cloudflare Pages
   - `Seed__AdminEmail`, `Seed__AdminPassword`
   - `Seed__CrmUserName`, `Seed__CrmUserEmail`, `Seed__CrmUserPassword` → cuenta
     que recibe los hand-off de WhatsApp; sin las dos últimas no se crea
5. Deploy. La API migra y siembra sola al arrancar. Healthcheck: `GET /health`.

El procedimiento completo del recambio (proyecto nuevo, verificación, usuario de
servicio para n8n, baja del viejo) está en
**[docs/RECAMBIO-RAILWAY.md](docs/RECAMBIO-RAILWAY.md)**.

### Conectar el frontend

En `jalcruz-firstclass-web`, cambia `VITE_API_URL` al nuevo dominio de Railway y
redeploya en Cloudflare. Como el contrato JSON es idéntico, no hay más cambios.
Para volver atrás, apunta `VITE_API_URL` de vuelta al Laravel anterior.

## Base de datos: se arranca vacía

**No se migra ningún dato** del sistema anterior — decisión tomada con los
interesados. El proyecto nuevo nace con una base vacía que llenan las
migraciones y el seeder: ver **[docs/RECAMBIO-RAILWAY.md](docs/RECAMBIO-RAILWAY.md)**.

`docs/MIGRACION_DATOS.md` quedó obsoleto por esa decisión; se conserva sólo como
referencia del mapeo entre el esquema de Laravel y el de EF Core.
