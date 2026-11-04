# Bookify — Stripe CLI: webhooks locales y eventos de prueba

> **Módulo:** 24.4 — Stripe CLI  
> **Proyecto:** `Bookify.Services.Booking`  
> **Destino del archivo:** `docs/payments/stripe-cli-local-webhooks.md`  
> **Alcance:** entorno local **Development**, Stripe **test mode/sandbox**, terminal **PowerShell en Windows**.  
> **No ejecutar en producción ni con claves `sk_live_`.**

## 1. Objetivo y alcance

Reproducir localmente estos pasos sin modificar la API:

1. Ejecutar Bookify en `Development` con `Payments:Provider=Stripe`.
2. Escuchar eventos de Stripe con Stripe CLI y reenviarlos por HTTPS al webhook existente.
3. Configurar el secreto `whsec_...` específico del listener.
4. Emitir tres tipos de evento de prueba y observar su recepción.
5. Probar que una firma inválida es rechazada.
6. Diferenciar la **recepción de un evento** de una **reconciliación real con Booking/PaymentAttempt**.
7. Restaurar las variables de entorno para volver al proveedor Fake.

**No se añaden clases, endpoints, migrations, DI ni paquetes NuGet.**

## 2. Contratos reales de Bookify

| Componente | Contrato revisado |
|---|---|
| Webhook | `POST /api/v1/payments/webhooks/stripe` |
| HTTP local | `https://localhost:7071` (launch profile `https`) |
| Alternativa HTTP | `http://localhost:5148` (pero `UseHttpsRedirection()` está habilitado en Development) |
| Proveedor Development por defecto | `Payments:Provider=Fake` |
| Proveedor para esta guía | `Payments:Provider=Stripe` |
| API key Stripe | `Payments:Stripe:SecretKey` (`sk_test_...`) |
| Firma de webhook | `Payments:Stripe:WebhookSecret` (`whsec_...`) |
| Tolerancia | `Payments:Stripe:WebhookToleranceSeconds`, defecto `300` |
| Autenticación webhook | `Stripe-Signature` + cuerpo original, **sin JWT** |
| Eventos procesados | `payment_intent.succeeded`, `payment_intent.payment_failed`, `payment_intent.canceled` |
| Correlación con reserva | `data.object.metadata.bookify_booking_id` |
| Correlación con intento | `data.object.id` == `PaymentAttempt.ExternalReference` |
| Respuesta válida procesada/ignorada | HTTP `200 OK` |
| Firma ausente o inválida | HTTP `400`, ProblemDetails |

La firma usa `Stripe.EventUtility.ValidateSignature` sobre el **raw body**, cabecera y secreto. Si el evento está firmado correctamente, el handler utiliza `IPaymentWebhookEventStore` para registrar/procesar su identidad y aplica `PaymentReconciler` solo cuando el evento puede relacionarse con una reserva y un intento reales. Los eventos con tipos no soportados se registran como procesados sin efectuar una transición de pago.

## 3. Prerrequisitos

- Windows con PowerShell; .NET SDK y PostgreSQL del proyecto operativos.
- Bookify compilando con su configuración de desarrollo existente.
- Acceso a una cuenta/sandbox de Stripe **test**, nunca live.
- Stripe CLI instalada y sesión autenticada.
- Acceso local al perfil HTTPS `https://localhost:7071`.
- PostgreSQL de desarrollo con migraciones aplicadas. No utilizar la base real de clientes.
- Tres terminales: **A** para el listener, **B** para la API, **C** para eventos y verificaciones.

### 3.1. Instalar y autenticar Stripe CLI

En PowerShell:

```powershell
winget install --id Stripe.StripeCLI -e
```

Cerrar y abrir nuevamente la terminal si el comando `stripe` no se reconoce. Alternativa oficial: `npm install -g @stripe/cli` si ya trabajas con Node.js. Verificar:

```powershell
stripe version
stripe login
stripe login list
```

Si hay más de una cuenta o sandbox autorizado, seleccionar explícitamente el correcto:

```powershell
stripe switch
```

**Comprobar el entorno seleccionado antes de emitir eventos.** Omitir `--live` en todos los comandos de esta guía.

### 3.2. Preparar el certificado local

```powershell
dotnet dev-certs https --trust
```

El perfil inspeccionado expone `https://localhost:7071`. Stripe CLI permite `--skip-verify` para certificados de desarrollo autofirmados; úsalo **únicamente para el forwarding local**, nunca como instrucción genérica para entornos reales.

## 4. Terminal A — Iniciar el forwarding

Ejecutar **antes de iniciar la API**, para obtener el secreto de firma del listener:

```powershell
stripe listen `
    --events payment_intent.succeeded,payment_intent.payment_failed,payment_intent.canceled `
    --forward-to https://localhost:7071/api/v1/payments/webhooks/stripe `
    --skip-verify
```

Resultado ilustrativo (valores ficticios):

```text
Ready! Your webhook signing secret is whsec_<valor_real_de_tu_listener>
```

**No compartir ni registrar ese valor.** No es la API key `sk_test_` y no debe confundirse con el secreto de un endpoint registrado en el Dashboard de Stripe. Para esta sesión local utilizar el `whsec_` anunciado por `stripe listen`.

Mantener abierta la terminal A. El listener firma y reenvía los eventos al endpoint indicado, sin requerir que expongas `localhost` en Internet.

## 5. Terminal B — Ejecutar Bookify con Stripe

`appsettings.Development.json` utiliza `Fake` por defecto. No edites ese archivo ni agregues secretos al repositorio: para 24.4 usaremos **variables de entorno de alcance exclusivo a esta terminal PowerShell**. ASP.NET Core interpreta `__` como `:` en claves de configuración.

En una nueva terminal situada en la raíz de Bookify:

```powershell
# Cambiar el proveedor solamente para el proceso lanzado
# desde esta terminal.
$env:Payments__Provider = "Stripe"

# Introducir la API key de TEST sin mostrarla en consola.
$apiKeySecure = Read-Host "Stripe API secret (sk_test_...)" -AsSecureString
$env:Payments__Stripe__SecretKey = (
    [System.Net.NetworkCredential]::new("", $apiKeySecure).Password
)
$apiKeySecure.Dispose()

# Introducir el whsec_ que imprimió stripe listen.
$webhookSecure = Read-Host "Stripe CLI webhook signing secret (whsec_...)" -AsSecureString
$env:Payments__Stripe__WebhookSecret = (
    [System.Net.NetworkCredential]::new("", $webhookSecure).Password
)
$webhookSecure.Dispose()

# Opcional: documentar explícitamente la tolerancia vigente.
$env:Payments__Stripe__WebhookToleranceSeconds = "300"

# Verificar SOLO que existen las variables, SIN imprimir valores.
if (-not $env:Payments__Stripe__SecretKey.StartsWith("sk_test_")) {
    throw "Se requiere una Stripe API key de test."
}
if (-not $env:Payments__Stripe__WebhookSecret.StartsWith("whsec_")) {
    throw "El secreto del listener debe comenzar con whsec_."
}

# Ejecutar API en Development.
dotnet run --project src/Bookify.Services.Booking.Api --launch-profile https
```

**Advertencia:** Las variables de entorno son temporales para la sesión y sus procesos hijos, pero sus valores existen en memoria. No compartir dumps, historiales ni capturas que revelen secretos. Para un entorno permanente se debe utilizar el mecanismo de secretos apropiado, no copiar valores a `appsettings.json`.

**Precondición:** Debe existir `ConnectionStrings:Database`, configuración OIDC y PostgreSQL accesible, igual que en 24.3. Con `Payments:Provider=Stripe`, la API valida que tanto `SecretKey` como `WebhookSecret` estén configurados. Al cambiar el secreto del listener, **reinicia la API** con el valor nuevo para evitar rechazos de firma.

### 5.1. Health smoke test

En terminal C:

```powershell
Invoke-RestMethod -Uri "https://localhost:7071/health" -Method Get
```

Esperado: objeto con `status: Healthy` y servicio Bookify; no significa todavía que Stripe ni los webhooks estén comprobados.

## 6. Terminal C — Enviar eventos de prueba

Con listener y API activos:

```powershell
stripe trigger payment_intent.succeeded
stripe trigger payment_intent.payment_failed
stripe trigger payment_intent.canceled
```

Stripe CLI utiliza fixtures basadas en objetos del entorno de prueba y puede crear otros objetos/eventos durante su ejecución. Por ello, `stripe trigger` **no** constituye una prueba aislada sin efectos en Stripe. Tampoco equivale a un cargo real.

Comprobar en terminal A que cada evento objetivo aparece como enviado y observar el **HTTP devuelto por Bookify**. Lo esperado para payloads válidos, firmados y sin correspondencia con un PaymentAttempt de Bookify es generalmente `200`, con el evento registrado como procesado/ignorado, no una modificación de reservas.

> **Error conceptual que debemos evitar:** `stripe trigger payment_intent.succeeded` normalmente crea su propio `pi_...`, que no es el `PaymentAttempt.ExternalReference` de ninguna reserva que Bookify haya iniciado. Si tampoco contiene `metadata.bookify_booking_id`, Bookify acepta el evento pero no cambia ningún `Booking.Status` ni `Payment.Status`. **HTTP 200 no significa `Paid`.**

### 6.1. Matriz de pruebas de recepción

| Caso | Acción | Resultado esperado |
|---|---|---|
| T1 | `stripe trigger payment_intent.succeeded` | Listener entrega evento y webhook devuelve `200` si el payload es válido y no requiere una reconciliación imposible |
| T2 | `stripe trigger payment_intent.payment_failed` | Igual criterio; no asumir transición a `Failed` sin PaymentAttempt relacionado |
| T3 | `stripe trigger payment_intent.canceled` | Igual criterio; no asumir cancelación de Booking sin relación real |
| T4 | Solicitud sin `Stripe-Signature` | `400` con ProblemDetails |
| T5 | Solicitud con firma forjada | `400` con ProblemDetails |

### 6.2. Probar rechazo de firma sin modificar Bookify

```powershell
$webhookUrl = "https://localhost:7071/api/v1/payments/webhooks/stripe"

$body = '{"id":"evt_bookify_unauthorized_test","type":"payment_intent.succeeded"}'

try {
    $response = Invoke-WebRequest `
        -Uri $webhookUrl `
        -Method Post `
        -ContentType "application/json" `
        -Body $body `
        -ErrorAction Stop

    throw "FALLO: un webhook sin Stripe-Signature fue aceptado: $($response.StatusCode)"
}
catch {
    $status = [int]$_.Exception.Response.StatusCode
    if ($status -ne 400) {
        throw "FALLO: se esperaba HTTP 400, se obtuvo $status"
    }
    Write-Host "PASS: webhook sin firma rechazado con HTTP 400"
}

try {
    $response = Invoke-WebRequest `
        -Uri $webhookUrl `
        -Method Post `
        -ContentType "application/json" `
        -Headers @{ "Stripe-Signature" = "t=1,v1=invalid" } `
        -Body $body `
        -ErrorAction Stop

    throw "FALLO: una firma forjada fue aceptada: $($response.StatusCode)"
}
catch {
    $status = [int]$_.Exception.Response.StatusCode
    if ($status -ne 400) {
        throw "FALLO: se esperaba HTTP 400, se obtuvo $status"
    }
    Write-Host "PASS: firma forjada rechazada con HTTP 400"
}
```

No deshabilitar la validación de firmas para hacer que una prueba pase. Si el webhook responde `400` a los eventos válidos del CLI, revisar primero si el `whsec_` coincide con el listener activo y si la API fue reiniciada después de actualizarlo.

## 7. Prueba de reconciliación real (escenario controlado)

Esta prueba utiliza un **PaymentIntent real de test creado por Bookify**, no uno de `stripe trigger`. No forma parte del frontend y no requiere implementar Stripe Payment Element.

### 7.1. Preparar una reserva nueva

1. Con `Payments:Provider=Stripe` y el listener activo, crear una Property/Room/pricing de prueba o utilizar una propiedad de desarrollo con disponibilidad.
2. Crear una **nueva** Booking mediante `POST /api/v1/bookings` con su `Idempotency-Key`, sin Bearer si será Guest.
3. Con un JWT Owner/Admin, aprobar la reserva por el endpoint administrativo existente.
4. Ejecutar `POST /api/v1/payments` con `{ "bookingId": "..." }`, el `Booking-Guest-Token` inicial y una nueva `Idempotency-Key`. No reutilizar el intento Fake creado en 24.3.
5. Verificar HTTP `200` y obtener el identificador `paymentId`. No imprimir ni persistir `clientSecret`.
6. Con Owner/Admin consultar `GET /api/v1/admin/properties/{propertyId}/bookings/{bookingId}/payment`.
7. Del `payment.attempts` correspondiente, obtener `externalReference` y verificar que empieza por `pi_`. **Este es el PaymentIntent que debemos confirmar.**

La colección de 24.3 está diseñada y verificada con proveedor `Fake`; al operar con Stripe, sus supuestos de respuesta deben revisarse. No interpretar una ejecución anterior de 24.3 como evidencia de reconciliación Stripe.

### 7.2. Confirmar el mismo PaymentIntent en test mode

En terminal C, introducir **solo** el ID `pi_...` obtenido del endpoint administrativo (no el `clientSecret`):

```powershell
$paymentIntentId = Read-Host "PaymentIntent ID de Bookify (pi_...)"

if (-not $paymentIntentId.StartsWith("pi_")) {
    throw "El identificador debe ser un PaymentIntent de Stripe."
}

# La cuenta/sandbox activa del CLI debe coincidir
# con la clave sk_test_ usada por Bookify.
stripe post "/v1/payment_intents/$paymentIntentId/confirm" `
    -d "payment_method=pm_card_visa" `
    -d "return_url=https://example.com"
```

Stripe documenta la confirmación de PaymentIntents con un método de prueba `pm_card_visa`. Según la configuración de la cuenta y métodos permitidos, puede requerirse una acción adicional o aparecer un error de Stripe; **no fuerces estados en Bookify** ni sustituyas el `pi_` de ese intento por uno generado con `stripe trigger`.

Cuando Stripe confirme la operación como `succeeded`, el evento `payment_intent.succeeded` firmado debe llegar al listener local. Para que Bookify aplique la reconciliación deben cumplirse a la vez:

- El `bookify_booking_id` de Stripe es el Booking ID correcto.
- El `data.object.id` coincide con `PaymentAttempt.ExternalReference` persistido.
- El evento se verifica con el secreto actual.
- La transición del estado es admisible según `PaymentReconciler`.
- Los plazos y la base de datos de desarrollo permiten completar el proceso.

### 7.3. Comprobar el estado autoritativo

Con un JWT Owner/Admin, consultar:

```http
GET /api/v1/admin/properties/{propertyId}/bookings/{bookingId}
GET /api/v1/admin/properties/{propertyId}/bookings/{bookingId}/payment
```

Verificar que Payment, PaymentAttempt y Booking reflejan la reconciliación **según los valores devueltos por la API**, no únicamente por la salida del CLI. Para una transición exitosa y válida, se espera un pago `Succeeded` y la reserva `Paid`; si no, registrar el estado y el error exacto para diagnóstico.

**No es una prueba de modo live ni implica movimientos de dinero real.** Se trabaja siempre con `sk_test_` y método de pago de prueba.

## 8. Diagnóstico

| Síntoma | Causa probable | Acción |
|---|---|---|
| `stripe` no se reconoce | CLI fuera del PATH | Abrir terminal nueva; `stripe version`; reinstalar desde fuente oficial si aplica |
| `stripe login` no autoriza | Cuenta/sandbox o permisos | Revisar `stripe login list`, `stripe switch`, permisos CLI en Dashboard |
| Error de TLS al reenviar | Certificado local autofirmado | `dotnet dev-certs https --trust` o `--skip-verify` **solo en desarrollo local** |
| `404` al reenviar | Puerto o ruta incorrectos | Usar HTTPS `7071` y `POST /api/v1/payments/webhooks/stripe` |
| `400` con `Payments.Webhook.SignatureRequired` | Falta `Stripe-Signature` | Usar `stripe listen`; no enviar JSON arbitrario como si viniera de Stripe |
| `400` con `Payments.Webhook.InvalidSignature` | Secreto equivocado, payload alterado, reloj fuera de tolerancia | Actualizar `whsec_` desde el listener, reiniciar API y verificar tiempo del sistema |
| `500` al procesar evento | Error de persistencia o correlación | Revisar el campo `code` del ProblemDetails y logs sanitizados |
| `200`, pero Booking sigue `PendingPayment` | Evento válido sin booking metadata o intento relacionado | Confirmar el **mismo** `pi_` creado por Bookify; revisar `bookify_booking_id` y `ExternalReference` |
| API no inicia con proveedor Stripe | Clave o secreto ausentes | Verificar presencia de `Payments__Stripe__SecretKey` y `Payments__Stripe__WebhookSecret` sin mostrar valores |
| PaymentIntent no se confirma | API key de otra cuenta, método no permitido, status incompatible | Verificar sandbox y resultado de Stripe, no modificar la lógica de reconciliación |
| `429` | Límite de webhooks del API | Reducir el volumen y atender `Retry-After` cuando exista |

**Precaución:** No copiar logs completos si muestran claves, firmas, `client_secret`, credenciales Guest o payloads con datos personales. Redactar esos campos.

## 9. Limpieza y retorno a Fake

Detener la API (Ctrl+C) y el listener (Ctrl+C). En la terminal B que configuró Stripe:

```powershell
Remove-Item Env:\Payments__Provider -ErrorAction SilentlyContinue
Remove-Item Env:\Payments__Stripe__SecretKey -ErrorAction SilentlyContinue
Remove-Item Env:\Payments__Stripe__WebhookSecret -ErrorAction SilentlyContinue
Remove-Item Env:\Payments__Stripe__WebhookToleranceSeconds -ErrorAction SilentlyContinue
```

Al iniciar Bookify otra vez sin esas variables, `appsettings.Development.json` vuelve a seleccionar el proveedor `Fake`. Esto no elimina los PaymentAttempts generados durante las pruebas Stripe. No reutilizarlos como si fueran intentos Fake.

## 10. Definition of Done de 24.4

- [ ] Stripe CLI instalada y autenticada en el sandbox correcto.
- [ ] API arranca en Development con `Payments:Provider=Stripe`.
- [ ] Listener activo, con `whsec_` coincidente en la API.
- [ ] Forwarding hacia la ruta existente con respuestas HTTP observadas.
- [ ] Los tres eventos CLI documentados fueron disparados y registrados.
- [ ] Una solicitud sin firma y otra con firma forjada fueron rechazadas con `400`.
- [ ] Se documentó explícitamente que `200` no demuestra reconciliación.
- [ ] Se comprobó una reconciliación real si se ejecutó la prueba controlada de la sección 7; registrar esta evidencia **por separado**.
- [ ] Se volvió a Fake o se cerró el entorno temporal de pruebas.
- [ ] Se revisó que ningún secreto aparezca en cambios de Git.

> La documentación está preparada, pero el submódulo solo queda **verificado en entorno local** cuando el desarrollador ejecuta los comandos y aporta la evidencia correspondiente. La cobertura automatizada/regresión de documentación sigue en `DEFERRED → 24.8 — Tests de OpenAPI y documentación`.

## 11. Archivos, commit y verificación

**Archivo nuevo:** `docs/payments/stripe-cli-local-webhooks.md`.

```powershell
dotnet build Bookify.Services.Booking.slnx -c Release
dotnet format Bookify.Services.Booking.slnx --verify-no-changes

git status --short
git add docs/payments/stripe-cli-local-webhooks.md
git commit -m "docs(payments): document local stripe webhook testing"
```

No se realiza `git add .`; no se versionan configuraciones locales, secretos ni capturas con credenciales.

## 12. Límites del submódulo y diferidos

- `DEFERRED → 24.8 — Tests de OpenAPI y documentación`: pruebas de regresión del contrato y auditoría de guías/documentación del Módulo 24.
- `DEFERRED → 24.6 — README`: resumen de configuración Stripe y enlaces a esta guía dentro del README principal.
- `DEFERRED → 24.5 — Diagramas`: diagrama de secuencia del webhook, reconciliación e idempotencia.
- `DEFERRED → 24.7 — ADRs`: decisiones arquitectónicas consolidadas, incluida la relación API→Domain.
- `DEFERRED → 28.2 — Stripe Payment Element`: confirmación de pagos desde el frontend Next.js; no se crea en 24.4.

No se incorporan nuevas funcionalidades al backend, ni se cambian firmas de interfaces, ni se crean migraciones.

## 13. Fuentes oficiales

- [Stripe CLI: instalación](https://docs.stripe.com/cli/install)
- [Stripe CLI: listen](https://docs.stripe.com/cli/listen)
- [Stripe CLI: trigger](https://docs.stripe.com/cli/trigger)
- [Stripe: recepción de webhooks](https://docs.stripe.com/webhooks)
- [Stripe: confirmación de PaymentIntent](https://docs.stripe.com/api/payment_intents/confirm)
- [Stripe: métodos de prueba](https://docs.stripe.com/testing)
- [Stripe CLI: referencia de comandos](https://docs.stripe.com/cli)
