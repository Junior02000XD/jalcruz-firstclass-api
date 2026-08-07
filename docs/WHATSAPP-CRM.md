# Endpoints para el agente de WhatsApp (Fase A)

Lo que el módulo First Class le expone al agente de IA que corre en n8n. Todo
usa el mismo esquema de autorización que el resto del CRM
(`[Authorize(Roles = "CRM Admin,Super Admin")]`), así que el usuario de servicio
de n8n necesita rol **CRM Admin**.

El contrato JSON es el de siempre: claves en `snake_case` y valores de enum en
español.

## Cómo se autentica n8n: cuenta de servicio y token largo

n8n **no inicia sesión con email y contraseña**. Hay una cuenta de servicio que
no puede iniciar sesión de ninguna manera, y un endpoint que le emite un token.

**La cuenta** (`agente-whatsapp@firstclass.local`, rol *CRM Admin*) la crea sola
`DbSeeder` al arrancar, sin ninguna variable con secretos: su hash de contraseña
es de 32 bytes aleatorios que se descartan en el acto, así que no existe un valor
que pase por `/api/login`. Además `IsServiceAccount` la rechaza ahí de forma
explícita, para que la garantía no dependa de cómo se creó la fila — si alguien
le pone una contraseña a mano en la base, sigue sin poder entrar.

Cambiar el nombre o el identificador: `Seed:AgentUserEmail`, `Seed:AgentUserName`.
Es idempotente por email, así que un redeploy no la duplica. Y si **ya existe
alguna** cuenta de servicio, el seeder no crea la suya: si no, cada despliegue
dejaría una cuenta paralela sin uso y dos candidatas para el token.

**Si el bot ya existía como usuario normal**, creado a mano antes de que esto
existiera, no hay que rehacerlo — borrarlo se llevaría puestos los prospectos que
tenga asignados. Se lo convierte:

```
POST   /api/users/{id}/service-account        (sólo Super Admin)
DELETE /api/users/{id}/service-account        para volver atrás; pide new_password
```

Al convertirlo deja de poder iniciar sesión y **su contraseña se destruye**, que
es justamente el punto: la que alguien haya anotado al crear la cuenta deja de
servir. **No se puede convertir la cuenta propia** — quien lo intentara quedaría
sin acceso al panel, ni siquiera para deshacerlo.

> ⚠️ Revertir **no invalida los tokens ya emitidos** para esa cuenta: son JWT sin
> estado. Lo que de verdad limita lo que un token puede hacer es el rol, así que
> para cortarlo hay que quitarle los roles o rotar `Jwt:Key`.

**Desde el panel:** *Control de Usuarios* (sólo Super Admin) tiene todo esto en
botones — **Hacer bot**, **Token** y deshacer. El token se muestra una sola vez,
con botón de copiar y la instrucción de dónde pegarlo en n8n.

**El token:**

```
POST /api/service-token          (sólo Super Admin)
Authorization: Bearer <token del Super Admin>
Body: {"id": 3}  ·  {"email": "..."}  ·  o vacío si hay una sola cuenta
```

Devuelve `access_token`, `token_type` y `expires_at`. Es el **mismo** JWT de
siempre —mismos claims, mismos roles, misma firma, mismo middleware de
validación—; lo único distinto es la expiración: `Jwt:ServiceTokenDays`, 10 años
por defecto. **El login normal de las personas sigue en 12 h y no cambió.**

Se eligió no inventar un segundo mecanismo de autenticación justamente para no
tener dos superficies que auditar.

### Por qué un token y no la contraseña de un usuario

Guardar email + contraseña en n8n entrega la cuenta entera: quien los tenga puede
entrar por el frontend y cambiar esa misma contraseña para quedarse adentro. Y es
una credencial más viajando en cada respaldo del volumen de n8n. El token sólo
alcanza lo que el rol permite y no sirve para iniciar sesión.

### ⚠️ Revocar un token exige rotar `Jwt:Key`

Al ser JWT sin estado, no hay forma de invalidar uno ya emitido salvo cambiar la
clave de firma — y eso **cierra la sesión de todos los usuarios a la vez**. Se
aceptó ese costo en lugar de mantener una lista de revocación que habría que
consultar en cada petición. Si el token se filtra: rotar `Jwt:Key` en Railway,
volver a emitirlo y cargarlo en n8n; todos vuelven a iniciar sesión una vez.

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
| `media_asset_id` | FK a `media_assets` (desde la Fase B). Al borrar el archivo queda en null y el texto se conserva |
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


---

# Fase B: contenido de negocio y multi-número

Lo que la IA *sabe* del instituto, cargado por los dueños desde el panel
(`/firstclass/contenido`) y leído por el agente en cada mensaje.

## `GET /api/agent-context/{phoneNumberId}` — la llamada principal

Todo lo necesario para armar el prompt, en una sola consulta: la voz del número
y el contenido encendido, con sus archivos.

```json
{
  "persona":  { "id": 1, "phone_number_id": "...", "style_guide": "...", "user_id": 2, "user_name": "Susanne" },
  "entries": [
    { "id": 1, "type": "pregunta_respuesta", "title": "...", "content": "...", "media": [] },
    { "id": 3, "type": "promocion", "valid_until": "2026-12-31", "restricted_zone_id": 1,
      "restricted_zone_name": "Norte", "conditions_text": "...",
      "media": [ { "id": 1, "type": "imagen", "url_r2": "https://...", "label": "...", "transcript": "..." } ] },
    { "id": 4, "type": "flujo", "next_action": "pedir_nombre", "handoff_to_user_id": null, "media": [] }
  ]
}
```

**404 = número pausado, no error.** Si no hay persona activa para ese
`phone_number_id`, responde 404 con `"paused": true`. Es la forma de apagar el
agente en un número sin tocar nada más.

### Filtrado del lado del servidor

- Sólo entradas con `active = true` (el interruptor del panel).
- **Se excluyen las promociones vencidas.** El vencimiento se compara contra la
  fecha **local de Bolivia (UTC-4)**: con UTC, una promo que vence hoy se
  apagaría a las 20:00 de Santa Cruz, en pleno horario de atención.
- `restricted_zone_id` **sí viaja**: la restricción por zona la aplica la IA
  conversando, porque cuando llega el primer mensaje todavía no se sabe de dónde
  es el prospecto.

### La respuesta es determinística

Con el mismo contenido, dos llamadas devuelven **exactamente los mismos bytes**
— verificado con 10 llamadas seguidas comparando el hash. Eso es lo que permitirá
encender el prompt caching de Claude sin rehacer nada: un orden que cambiara entre
llamadas invalidaría el caché en cada mensaje.

Lo que lo sostiene, y que conviene no romper:

- El orden de las entradas se fija en memoria por **valor del enum** (preguntas,
  reglas, promociones, flujos) y después por id. Ordenar por la columna ataría el
  orden al texto español de cada enum.
- Los archivos de cada entrada van ordenados por id.
- Se serializan **DTOs sin timestamps**, no las entidades.

## Multimedia (`/api/media`)

`POST` es `multipart/form-data`: `file`, `label`, `transcript`.

- Formatos: `image/jpeg`, `image/png` y audio `aac / amr / mpeg / mp4 / ogg`.
- Tamaño: **5 MB** para imagen y **16 MB** para audio, que son los límites de la
  Cloud API — aceptar más sería guardar algo que después no se puede enviar.
- `DELETE` borra **también el objeto en R2**. Si R2 falla, la fila no se borra:
  es preferible una ficha viva a un archivo huérfano que nadie sabe que existe.

**`transcript` es lo único que la IA sabe del archivo.** Nunca se procesa el
contenido en tiempo de respuesta: el agente decide si mandar un audio leyendo su
transcripción. Un archivo sin transcripción, en la práctica, no se usa.

### Por qué el bucket es público

Meta descarga el archivo desde sus servidores cuando la IA lo manda, así que la
URL tiene que ser alcanzable sin credenciales. Se evaluaron URLs prefirmadas y se
descartaron: cambian en cada generación, con lo que la respuesta de
`agent-context` dejaría de ser byte a byte estable. La clave del objeto lleva un
sufijo aleatorio, así que el contenido es público para quien tenga el enlace pero
no se puede adivinar ni enumerar.

## Contenido (`/api/context-entries`)

CRUD con `media_asset_ids` para asociar archivos (la lista **reemplaza** a la
anterior; omitirla no la toca). `PATCH /{id}/active` es el interruptor de la
tarjeta, aparte del PUT para que encender o apagar no dependa de reenviar bien
todo el formulario.

Los campos de un tipo se limpian al cambiar de tipo: si una promoción pasa a ser
regla, su vencimiento no puede quedar colgado y reapareciendo en el prompt.

## Personas (`/api/personas`)

Una **activa por `phone_number_id`**, garantizado por un índice único filtrado
(`WHERE active`). Intentar una segunda devuelve **409** con un mensaje claro en
vez de un 500. Las inactivas no molestan: sirven para conservar versiones
anteriores del estilo.

`PATCH /{id}/active` pausa o reanuda el agente en ese número.
