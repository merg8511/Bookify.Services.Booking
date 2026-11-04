# Bookify — Módulo 24.3 · Colección Postman guiada

> **Rutas de destino en el repositorio:** `docs/postman/Bookify.Booking.postman_collection.json`, `docs/postman/Bookify.Local.postman_environment.json` y `docs/postman/README.md`.
>
> **Estado:** entregables listos para importar. No se han ejecutado contra tu backend; no equivalen a una prueba de integración aprobada.

## Alcance

Esta colección contiene **23 solicitudes HTTP ordenadas** que consumen contratos reales de Bookify y cubren el flujo exigido por el roadmap: crear Property, crear Room, configurar precios, consultar disponibilidad y cotización, reservar, aprobar, iniciar pago, consultar estado, cancelar una segunda reserva y verificar lecturas administrativas.

**No es una importación de los 33 endpoints de OpenAPI.** Es un recorrido integral seleccionado con dependencias y aserciones de respuesta, mientras que 24.1/24.2 ya verificaron el contrato de las 33 operaciones.

## Requisitos previos

1. Ejecutar la API en `Development` utilizando su configuración ya validada, las migrations y su PostgreSQL habitual. Con el launch profile `https`: `https://localhost:7071`.
2. Tener un proveedor OIDC disponible y proporcionar un JWT real, válido y no expirado con rol **Owner** o **Admin**. Bookify no implementa un endpoint HTTP propio para emitir estos tokens. El token debe ser para la audiencia configurada de esta API y con los claims esperados.
3. En `Development`, `Payments:Provider` está configurado como `Fake` en el snapshot estudiado. Esta colección espera el comportamiento Fake, con `Payment/PaymentAttempt = Pending`. Si usas Stripe, esas aserciones deben revisarse: no se realizan cargos ni se simula el webhook aquí.
4. Usar la aplicación Postman local y el **Collection Runner**, en orden, en una ejecución única. Las variables creadas mediante `pm.variables.set` solo se conservan durante esa solicitud o ejecución.
5. Confiar en el certificado HTTPS de desarrollo usando `dotnet dev-certs https --trust` cuando sea necesario. No deshabilitar globalmente la verificación TLS.

## Importar los archivos

1. Copiar los tres archivos a `docs/postman/` en tu clon local.
2. Abrir Postman → **Import** y seleccionar `Bookify.Booking.postman_collection.json` y `Bookify.Local.postman_environment.json`.
3. Activar el entorno **Bookify — Local Development (24.3)**.
4. Dejar `baseUrl = https://localhost:7071` o ajustarlo a la URL real del launch profile.
5. Completar **únicamente el valor local** de `ownerAccessToken` con el JWT crudo, **sin** prefijo `Bearer `; el request utiliza el autenticador Bearer de Postman. El archivo compartido debe conservar ese valor vacío. No publicar valores locales ni el historial de solicitudes con credenciales.
6. Ejecutar **Run collection** con **una iteración** y todas las carpetas seleccionadas, conservando el orden original. Mantener activa la opción de parar cuando ocurra un error y desactivar el registro en consola del Runner para reducir exposición accidental. No activar opciones de persistencia de valores de la ejecución.

**Las credenciales Guest no deben pegarse a mano ni guardarse en variables del entorno.** Se capturan del header `Booking-Guest-Token` en las respuestas `201 Created` y pasan a variables temporales `paymentGuestToken` / `cancelGuestToken`. Se destruyen al finalizar la ejecución. No se imprime su valor. Si se ejecutan las solicitudes individualmente, se perderá la cadena de variables y **no** funcionará el flujo completo.

## Secuencia completa (23 solicitudes)

| Orden | Método | Ruta | Resultado |
|---|---|---|---|
| 01 | GET | `/health` | 200 e inicializa fechas/keys únicas |
| 02 | POST | `/api/v1/properties` | 201, `propertyId` |
| 03 | GET | `/api/v1/properties/{propertyId}` | 200 |
| 04 | POST | `/api/v1/admin/properties/{propertyId}/units` | 201, `unitId` |
| 05 | PUT | `/api/v1/admin/properties/{propertyId}/units/{unitId}/pricing` | 204 |
| 06 | GET | `/api/v1/admin/properties/{propertyId}/units/{unitId}/pricing` | 200 |
| 07 | GET | `/api/v1/properties/{propertyId}/availability` | 200, cotización disponible A |
| 08 | POST | `/api/v1/bookings` | 201, reserva A Guest, token solo en header |
| 09 | GET | `/api/v1/bookings/{identifier}` | 200, `PendingApproval` |
| 10 | POST | `/api/v1/admin/properties/{propertyId}/bookings/{bookingId}/approve` | 204 |
| 11 | GET | `/api/v1/bookings/{identifier}` | 200, `PendingPayment` |
| 12 | POST | `/api/v1/payments` | 200, intento Fake `Pending` |
| 13 | GET | `/api/v1/payments/bookings/{bookingId}/status` | 200, estado autoritativo `Pending` |
| 14 | GET | `/api/v1/admin/properties/{propertyId}/bookings/{bookingId}/payment` | 200, Payment y Attempts |
| 15 | GET | `/api/v1/properties/{propertyId}/availability` | 200, fechas B |
| 16 | POST | `/api/v1/bookings` | 201, reserva B Guest independiente |
| 17 | POST | `/api/v1/admin/properties/{propertyId}/bookings/{bookingId}/approve` | 204 |
| 18 | POST | `/api/v1/bookings/{bookingId}/cancel` | 204, sin pago previo |
| 19 | GET | `/api/v1/bookings/{identifier}` | 200, `Cancelled` |
| 20 | GET | `/api/v1/properties/{propertyId}/availability` | 200, inventario B liberado |
| 21 | GET | `/api/v1/admin/properties/{propertyId}/bookings` | 200, listado paginado A y B |
| 22 | GET | `/api/v1/admin/properties/{propertyId}/bookings/{bookingId}` | 200, detalle A |
| 23 | GET | `/api/v1/admin/properties/{propertyId}/calendar` | 200, reserva A bloquea inventario |

## Dependencias y variables

- **Entorno (configuración manual):** `baseUrl`, `ownerAccessToken`.
- **Variables temporales de cada ejecución:** `propertyName`, `propertyId`, `unitId`, fechas A/B, tres llaves `Idempotency-Key`, IDs/referencia de Booking A/B, `paymentId` y tokens Guest A/B.
- **Fechas:** 60–63 días para Booking A y 70–73 días para Booking B, relativas a UTC y calculadas al iniciar. La segunda estadía no se superpone con la primera; tampoco se usan los IDs o fechas preexistentes del seed.
- **Property:** zona `America/El_Salvador`, check-in `15:00:00`, check-out `11:00:00`.
- **Room:** `type = Room`, capacidad 4, huéspedes incluidos 2.
- **Pricing:** USD, tarifa regular 120, fin de semana 150, huésped extra 25.
- **Reserva:** dos huéspedes y datos de prueba claramente ficticios.

## Seguridad y semántica de pagos

- El token Owner se usa solo en operaciones de administración; la creación Guest establece `noauth` explícitamente. Un Bearer accidental en `Bookings.Create` produciría una reserva autenticada sin `Booking-Guest-Token`.
- `Idempotency-Key` se genera una vez por **operación lógica** y no se regenera para un retry de esa misma operación. Una ejecución nueva de la colección genera nuevas claves y una nueva Property.
- El raw Guest token aparece solamente en la primera respuesta de creación. Repetir la solicitud de creación con la misma clave puede reproducir una respuesta sin volver a entregar la credencial. La colección nunca hace esa repetición automáticamente.
- `clientSecret` del gateway aparece en la respuesta del inicio de pago. **La colección no lo guarda ni lo imprime**. No registrarlo en documentación, variables persistentes ni logs.
- El gateway Fake crea un intento **Pending**, no un pago Succeeded. Ni el inicio de sesión ni un redirect implican que la reserva esté pagada. Las transiciones de pago exitoso requieren el procedimiento real de reconciliación (Stripe CLI se documentará en **24.4**, mientras que Stripe Payment Element pertenece a **28**).
- No se exponen comandos `mark-as-paid`, `expire-payment` ni `complete`, porque son operaciones de sistema sin endpoint público.
- La colección crea nuevos registros y transiciones persistentes. Ejecutarla contra una **base local de pruebas**, no contra datos reales o producción.
- Se recomienda finalizar el flujo sin pausas prolongadas: la configuración de referencia incluye una ventana de pago de **30 minutos** después de aprobar la reserva.

## Verificaciones

La propia colección incluye pruebas de códigos HTTP, IDs, estados, pricing, credenciales Guest presentes sin revelar su valor, vínculos Payment/Booking, liberación de inventario y lecturas administrativas. La estructura JSON y la sintaxis JavaScript de los scripts fueron comprobadas localmente **sin enviar solicitudes HTTP**.

Después de ejecutarla, conserva solo el resumen de resultados y errores relevantes, con **tokens, client_secret y cabeceras sensibles redactados**. No compartas el export de entorno completado con JWT real.

Los tests automatizados adicionales y la auditoría documental acumulada pertenecen al submódulo **24.8 — Tests de OpenAPI y documentación**.
