
# Bookify — Result vs Exception

## 1. Objetivo

Bookify distingue entre resultados funcionales y fallos
tecnicos inesperados.

La distincion se aplica en Domain, Application,
Infrastructure, API y Background Jobs.

No introducimos un segundo Result Pattern ni
una jerarquia de excepciones de negocio.

## 2. Result

Result representa resultados alternativos conocidos
del caso de uso.

Se utiliza cuando:

- La solicitud no cumple una regla.
- Un recurso solicitado no existe.
- El estado actual impide una operacion.
- Existe un conflicto funcional.
- Un adaptador devuelve un fallo operativo conocido
  mediante su contrato explicito.

Ejemplos:

- Booking no encontrada.
- Fechas invalidas.
- Capacidad excedida.
- Inventario ocupado.
- Payment ya completado.
- Operacion de pago rechazada.

Las validaciones de Domain y Application pueden
devolver Result o Result<T>.

## 3. ErrorType

Conservamos los tipos existentes:

None
Failure
Validation
NotFound
Conflict

HTTP utiliza el mapeo existente:

Validation -> 400
NotFound  -> 404
Conflict  -> 409
Failure   -> 500

ErrorType.Failure puede representar un fallo
operativo conocido y normalizado.

No significa que todas las excepciones deban
convertirse en Result.Failure.

## 4. Exception

Las excepciones representan fallos que no forman
parte de los resultados funcionales normales.

Ejemplos:

- Dependencia de infraestructura falla inesperadamente.
- Contrato interno inconsistente.
- Estado imposible del sistema.
- Violacion de una invariante de programacion.
- Configuracion critica invalida.
- Respuesta externa incompatible con una garantia
  interna de consistencia.

No utilizar excepciones para representar normalmente
BookingNotFound, Validation o Conflict.

No crear catch genericos que conviertan cualquier
Exception en Result.Failure.

## 5. Payment e idempotencia

El gateway puede normalizar respuestas conocidas
del proveedor mediante PaymentGatewayErrors.

La politica de retries, errores transitorios
y backoff pertenece al modulo 20.5.

Una referencia externa diferente para un intento
existente con la misma clave de idempotencia
es una inconsistencia tecnica.

Debe provocar una excepcion y rollback.

No continuar la reconciliacion con una referencia
externa incompatible.

No registrar client_secret, tokens, secretos
ni payloads sensibles en excepciones o logs.

## 6. Persistencia

Los errores inesperados de PostgreSQL o EF Core
no se convierten genericamente en Result.

Las transacciones deben preservar atomicidad.

Una excepcion no puede justificar un commit parcial
de una operacion que requiere consistencia atomica.

## 7. Cancellation

Una cancelacion solicitada debe propagarse
mediante OperationCanceledException.

No representar automaticamente una cancelacion
solicitada como un fallo de negocio.

La propagacion completa y los request timeouts
pertenecen al modulo 20.3.

## 8. HTTP

Los resultados funcionales se traducen mediante
ErrorHttpExtensions y ResultHttpExtensions.

Las excepciones no controladas deben producir
respuestas HTTP seguras.

IExceptionHandler y Problem Details globales
pertenecen al modulo 20.2.

No exponer stack traces ni detalles internos
en respuestas de produccion.

## 9. Background Jobs

Los resultados funcionales y las excepciones
no se deben confundir.

Un job puede reconocer un resultado fallido,
registrar el fallo y conservar el trabajo pendiente.

Las excepciones inesperadas no deben provocar
la perdida silenciosa del trabajo.

La politica durable de retries, intentos,
backoff y estado terminal de Outbox/Email
pertenece al modulo 20.6.

## 10. Reglas de desarrollo

Antes de introducir un nuevo error:

1. Determinar si representa un resultado esperado.
2. Revisar errores y excepciones existentes.
3. Respetar las invariantes de Domain.
4. No duplicar abstracciones.
5. Preservar transacciones e idempotencia.
6. Evitar exponer detalles sensibles.
7. Crear pruebas segun la estrategia del modulo.

## 11. Testing

Los tests deben comprobar:

- Result para errores funcionales.
- Exceptions para fallos tecnicos inesperados.
- Propagacion correcta.
- Rollback cuando corresponda.
- Respuestas seguras.
- Ausencia de duplicacion de efectos externos.

Cobertura exhaustiva:

DEFERRED -> 20.7 — Tests del modulo.
