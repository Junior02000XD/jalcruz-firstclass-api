# Endpoints para el agente de WhatsApp (Fase A)

Lo que el módulo First Class le expone al agente de IA que corre en n8n. Todo
usa el mismo esquema de autorización que el resto del CRM
(`[Authorize(Roles = "CRM Admin,Super Admin")]`), así que el usuario de servicio
de n8n necesita rol **CRM Admin**.

El contrato JSON es el de siempre: claves en `snake_case` y valores de enum en
español.

## El problema que resuelven estos endpoints

Los mensajes llegan por webhooks de Meta, y Meta:

- **reintenta** el webhook si no recibe el 200 a tiempo → el mismo mensaje puede
  entregarse dos veces;
- **no espera** → una ráfaga de mensajes seguidos dispara varias ejecuciones de
  n8n en paralelo para el mismo prospecto.

Por eso ninguna deduplicación de acá es "buscar y después insertar": ese patrón
falla justo cuando dos ejecuciones buscan antes de que cualquiera inserte. La
garantía es siempre del motor de base de datos.

## Teléfonos: la forma canónica

Meta manda el número con código de país (`59171234567`); en el CRM se carga a
mano y casi siempre local (`71234567`, `7123-4567`). Sin una forma común, cada
conversación crearía un prospecto nuevo.

`phones.normalized_number` guarda el número con sólo dígitos y con el 591
adelante si quedan 8 o menos — la regla vive en `Services/PhoneNormalizer.cs`.

El frontend **no** recalcula esa regla: `whatsappLink()` en `src/lib/crm.js` usa
el `normalized_number` que devuelve la API, así el enlace `wa.me` del panel
apunta a la misma conversación que busca el agente. Conserva la normalización
local sólo como respaldo, para números que todavía no pasaron por el backend.

La mantiene `AppDbContext.SaveChanges`, no los controllers: una columna
desincronizada vuelve al prospecto invisible para el lookup, y eso se traduce en
duplicados.

### Por qué el índice NO es único

`phones` la comparten los dos módulos. En RRHH es legítimo que dos trabajadores
compartan un teléfono de contacto, así que un índice único global rompería
altas válidas de un módulo que está fuera de este trabajo.

La unicidad que sí importa —un prospecto por número— la garantiza un
**advisory lock de Postgres** en `POST /api/prospects/quick`.

Para ver si algún día conviene promoverlo a único (sólo si los datos lo
permiten):

```sql
SELECT normalized_number, count(DISTINCT person_id) AS personas
FROM phones
WHERE normalized_number IS NOT NULL
GROUP BY normalized_number
HAVING count(DISTINCT person_id) > 1;
```

Cero filas significa que hoy ningún número está compartido, no que no pueda
estarlo: mientras `phones` siga siendo del núcleo compartido, el único seguiría
prohibiéndole a RRHH un caso legítimo.

## Endpoints

### `GET /api/prospects/by-phone/{numero}`

Primera llamada ante cada mensaje entrante. Acepta el número en cualquier
formato: lo normaliza antes de buscar. Si una persona tiene más de un prospecto
devuelve el más reciente.

`200` con el prospecto (incluye `person.phones`, `campaign`, `zone` y
`assigned_to`) · `404` si no existe · `400` si el número no tiene dígitos.

### `POST /api/prospects/quick`

Crea Persona + Teléfono + Prospecto. **Idempotente por teléfono**: si el número
ya pertenece a un prospecto devuelve ese con `200` en vez de duplicarlo; `201`
sólo cuando lo crea de verdad.

Contra la ráfaga de n8n, la transacción toma primero
`pg_advisory_xact_lock(hashtextextended(<normalizado>, 0))`: la segunda
ejecución espera y encuentra el prospecto ya creado. El lock es del motor, así
que aguanta varias instancias de la API, y sólo serializa a quienes traen el
mismo número.

### `PATCH /api/prospects/{id}/status`

Cuerpo: `{ "status": "contactado" }`. Toca **sólo** el estado — el `PUT`
completo pisa con null cualquier campo que el payload no traiga.

Un estado mal escrito devuelve `400` con la lista de valores válidos, en vez de
caer silenciosamente en `nuevo` y perder el avance del embudo.

### `PATCH /api/prospects/{id}/assignment`

Cuerpo: `{ "assigned_to_user_id": 2 }` para derivar la conversación a un humano,
`{ "assigned_to_user_id": null }` para devolvérsela a la IA.

Va aparte del PATCH de estado porque son decisiones independientes (derivar no
mueve el embudo) y porque, al ser el único campo del cuerpo, `null` significa
"limpiar" sin confundirse con "no lo mandé".

`assigned_to_user_id` se serializa **siempre**, incluso en null: es la bandera
que el agente consulta antes de responder.

La cuenta destino se siembra con `Seed:CrmUserEmail` / `Seed:CrmUserPassword`
(ver `.env.example`). Sin esas variables no se crea ninguna cuenta: una
contraseña por defecto escrita en el repo sería una puerta abierta.

### `POST /api/messages`

Registra un mensaje del historial. **Idempotente por `whatsapp_message_id`**
(el wamid de Meta): un reintento devuelve `200` con el mensaje ya guardado.

La garantía es el índice único **filtrado** `WHERE whatsapp_message_id IS NOT
NULL`, más la captura de la violación de unicidad (23505). Es filtrado porque el
wamid es opcional: un saliente se registra antes de que Meta devuelva su id, y
varios NULL no chocan entre sí.

| Campo | Notas |
|---|---|
| `direction` | `entrante` \| `saliente` |
| `origin` | `ia` \| `humano`. Omitido = `humano` |
| `content` | Opcional; un adjunto sin epígrafe se guarda con `""` |
| `media_asset_id` | Sin FK: `MediaAsset` llega en la Fase B |
| `whatsapp_media_url` | URL de Meta, caduca |
| `whatsapp_message_id` | Clave de idempotencia |

**`origin` es la base de la detección de hand-off:** los ecos de Coexistence
llegan como salientes, así que un saliente con `origin: "humano"` es la señal de
que el dueño del número contestó a mano desde su teléfono.

### `GET /api/prospects/{id}/messages`

Historial en orden cronológico (más viejo primero), que es como lo espera el
modelo. Con `?limit=N` devuelve los **últimos** N manteniendo ese orden —
recortar por el principio daría el arranque de la conversación en vez de lo que
se acaba de hablar.

### `GET /api/reminders`

Filtros opcionales: `?is_done=false`, `?due_before=<ISO>`, `?prospect_id=N`.
Ordena del más próximo al más lejano. CRUD completo en `/api/reminders`.

`due_before` sin zona horaria se interpreta como UTC (`remind_at` es
`timestamptz` y Npgsql rechaza fechas sin zona).
