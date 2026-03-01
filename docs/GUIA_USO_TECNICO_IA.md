# Guía de Uso Técnico para IAs (MexClub)

## 1. Resumen del Proyecto

**MexClub** es un sistema de gestión para asociaciones de socios con dispensario/retiradas, completamente reescrito desde un sistema legacy (ASMX + Xamarin) a una arquitectura moderna:

- **Backend**: ASP.NET Core 10 + Entity Framework Core + PostgreSQL
- **Frontend**: PWA (Progressive Web App) con HTML/CSS/JavaScript vanilla
- **Arquitectura**: REST API + JWT + Service Worker (offline-first)
- **Despliegue**: IIS + HTTPS (todo servido desde el mismo proyecto)

---

## 2. Estructura del Proyecto

```
src/
├── MexClub.Api/                    # Proyecto API (ASP.NET Core)
│   ├── Controllers/                # Endpoints REST
│   ├── Models/                     # Entidades EF Core
│   ├── Application/                # Lógica de negocio (Services, DTOs)
│   ├── Infrastructure/             # Repositories, Configs EF
│   └── wwwroot/                   # Frontend (la "app")
│       ├── js/                    # JavaScript vanilla
│       ├── css/                   # Estilos
│       ├── index.html             # SPA principal
│       └── manifest.json          # Configuración PWA
├── MexClub.Domain/                 # Entidades puras (sin EF)
├── MexClub.Application/           # DTOs, interfaces de servicios
└── MexClub.Infrastructure/        # Implementaciones concretas
```

---

## 3. Base de Datos (PostgreSQL)

### 3.1 Esquema Principal

```sql
-- Entidades principales
Socios                 -- Datos del socio (nombre, documento, etc.)
SocioDetalles          -- Datos calculados (saldo, consumición, cuotas)
Usuarios               -- Usuarios administrativos
UserLogins             -- Login/autenticación
Familias               -- Categorías de productos
Articulos              -- Productos del dispensario
Aportaciones           -- Ingresos de dinero
Retiradas              -- Retiradas del dispensario
Cuotas                 -- Pagos de cuotas mensuales
Accesos                -- Fichajes (entradas/salidas)
```

### 3.2 Campos Clave para Entender

**SocioDetalles** (tabla auxiliar pero crucial):
- `Aprovechable`: saldo disponible del socio (en euros)
- `ConsumicionDelMes`: gramos consumidos este mes
- `CuotaFechaProxima`: próxima fecha de pago de cuota
- `DebeCuota`: booleano si está al día con cuotas

**Retiradas**:
- `Cantidad`: gramos retirados
- `Total`: importe en euros (cantidad * precio por gramo)
- `FirmaUrl`: ruta a archivo de imagen de firma

---

## 4. API REST (Endpoints)

### 4.1 Autenticación

```http
POST /api/auth/login
{
  "username": "admin",
  "password": "admin"
}
```

Respuesta:
```json
{
  "success": true,
  "data": {
    "token": "eyJ...",
    "refreshToken": "abc...",
    "userId": 1,
    "username": "admin",
    "rol": "admin"
  }
}
```

### 4.2 Endpoints Principales

| Entidad | GET | POST | PUT | DELETE |
|---------|-----|------|-----|--------|
| Socios | `/api/socios` | `/api/socios` | `/api/socios/{id}` | `/api/socios/{id}` |
| Artículos | `/api/articulos` | `/api/articulos` | `/api/articulos/{id}` | `/api/articulos/{id}` |
| Retiradas | `/api/retiradas` | `/api/retiradas/batch` | - | `/api/retiradas/{id}` |
| Aportaciones | `/api/aportaciones` | `/api/aportaciones` | - | `/api/aportaciones/{id}` |
| Cuotas | `/api/cuotas` | `/api/cuotas` | - | `/api/cuotas/{id}` |
| Accesos | `/api/accesos` | `/api/accesos/fichar` | - | - |

### 4.3 Patrones de Uso

**Paginación**:
```http
GET /api/socios?page=1&pageSize=20&soloActivos=true
```

**Búsqueda**:
```http
GET /api/socios/search?q=juan&limit=30
```

**Filtros**:
```http
GET /api/articulos?soloActivos=true&familiaId=5
```

---

## 5. Frontend (JavaScript Vanilla)

### 5.1 Arquitectura

- **Módulo principal**: `MexClub` en `app.js`
- **API client**: `MexClubApi` en `api.js`
- **Patrón**: Namespaces + módulos funcionales (Socios, Articulos, Retiradas, etc.)

### 5.2 Módulos Clave

```javascript
// Autenticación
MexClubApi.login(user, pass)
MexClubApi.loadAuth()
MexClubApi.isAdmin()

// Operaciones principales
MexClubApi.getSocios(page, pageSize, soloActivos)
MexClubApi.createRetiradaBatch(data)  // Retiradas en lote
MexClubApi.deleteRetirada(id)

// UI
MexClub.Nav.go('pageSocios')
showModal(title, body, footer)
showToast(title, message, type)
```

### 5.3 Componentes UI Importantes

**Retiradas (POS)**:
- Búsqueda inteligente de socios (autocomplete)
- Carrito de productos con botones rápidos (10/20/30/50 €)
- Firma digital obligatoria (canvas)
- Validaciones de saldo y límite mensual

**Dashboard**:
- Estadísticas en tiempo real
- Acciones rápidas (Aportación, Retirada, Cuota)
- Listas de últimas operaciones con borrado por pulsación larga

**Artículos**:
- Selección múltiple con checkboxes
- Borrado masivo con confirmación
- Filtro por familia

---

## 6. Lógica de Negocio Crítica

### 6.1 Retiradas (Dispensario)

**Flujo completo**:
1. Seleccionar socio → se cargan `Aprovechable` y `ConsumicionDelMes`
2. Seleccionar artículos → se añaden al carrito
3. Validar:
   - Saldo suficiente (`Aprovechable >= totalCarrito`)
   - Límite mensual (`ConsumicionDelMes + cantidadNueva <= limiteMensual`)
4. Si excede límite mensual → modal de advertencia (puede continuar)
5. Firma digital obligatoria
6. Confirmar → se crea lote de retiradas

**Actualización de saldos**:
```csharp
// En CreateBatch (OperacionesController)
socio.Detalle.Aprovechable -= totalGeneral;           // Restar del disponible
socio.Detalle.ConsumicionDelMes += gramosTotales;     // Sumar al consumido mensual
```

### 6.2 Límite Mensual

- Se reinicia automáticamente cada mes/cambio de año
- Se mide en **gramos**, no en euros
- Validación: `ConsumicionDelMes` (gramos) vs `Socio.ConsumicionMaximaMensual`

### 6.3 Dashboard: "Visitas Hoy"

- Cuenta **socios únicos** con actividad en el día
- Actividad = aportación, retirada o cuota
- Evita duplicados por múltiples operaciones del mismo socio

---

## 7. Patrones de Código

### 7.1 Controllers (API)

```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]  // JWT
public class SociosController : ControllerBase
{
    private readonly ISocioService _service;
    
    [HttpGet]
    public async Task<ActionResult<ServiceResult<PagedResult<SocioDto>>>> GetAll(
        [FromQuery] PaginationParams pagination,
        [FromQuery] bool? soloActivos = null)
    {
        var result = await _service.GetAllAsync(pagination, soloActivos);
        return Ok(result);
    }
}
```

### 7.2 Services (Lógica de Negocio)

```csharp
public class SocioService : ISocioService
{
    public async Task<ServiceResult<SocioDto>> CreateAsync(CreateSocioRequest request)
    {
        // Validaciones
        // Mapeo entidad → DTO
        // Persistencia via repository
        // Return ServiceResult<T>
    }
}
```

### 7.3 Frontend: Módulos

```javascript
var Retiradas = {
    _state: { socio: null, cart: [], ... },
    
    showCreate: function () {
        // Inicializar estado, cargar datos, renderizar UI
    },
    
    _addToCart: function (articuloId) {
        // Validar socio, añadir al carrito, actualizar UI
    }
};
```

---

## 8. Configuración y Secrets

### 8.1 appsettings.json (no secrets)

```json
{
  "ConnectionStrings": {
    "MexClubDb": "Host=localhost;Port=4433;Database=MexClubDb;Username=mexclub;Password="
  },
  "Jwt": {
    "Key": "",
    "Issuer": "MexClub.Api",
    "Audience": "MexClub.Client"
  }
}
```

### 8.2 Variables de Entorno (producción)

```
ConnectionStrings__MexClubDb=Host=localhost;Port=4433;Database=MexClubDb;Username=mexclub;Password=REAL_PASSWORD
Jwt__Key=CLAVE_SECRETA_MINIMO_32_CARACTERES_ALEATORIA
```

---

## 9. Despliegue y Operación

### 9.1 Publicación

```powershell
cd src/MexClub.Api
dotnet publish -c Release -o C:\publish\MexClub
```

### 9.2 IIS Configuration

- **Application Pool**: .NET CLR Version = No Managed Code
- **Site**: HTTPS obligatorio para PWA
- **web.config**: generado automáticamente por `dotnet publish`

### 9.3 Base de Datos

```sql
-- Crear BD
CREATE DATABASE "MexClubDb" OWNER mexclub;

-- Ejecutar schema
psql -U mexclub -d MexClubDb -f scripts/migration/001_create_schema.sql
```

---

## 10. Migración desde Sistema Legacy

### 10.1 Herramienta de Migración

Ubicación: `scripts/migration/MigradorDatos/`

**Lee**: `ClubDB.sdf` (SQL Server Compact)  
**Escribe**: PostgreSQL (nuevo esquema)

### 10.2 Transformaciones Aplicadas

| Campo Legacy | Campo Nuevo | Transformación |
|--------------|-------------|----------------|
| `Password` (texto plano) | `PasswordHash` | SHA-256 + Base64 |
| `ReferidoPor` (código) | `ReferidoPorSocioId` | Resolución a FK |
| `Firma` (Base64) | `FirmaUrl` | Guardar como archivo |
| `Decimal` (bool) | `EsDecimal` | Renombrado |

---

## 11. Debugging y Troubleshooting

### 11.1 Logs

- **API**: Serilog → archivos en `logs/`
- **Frontend**: `console.log` + Service Worker events
- **DB**: PostgreSQL logs (activar si necesario)

### 11.2 Problemas Comunes

**PWA no instala**:
- Verificar HTTPS
- Comprobar `manifest.json` y `service-worker.js`

**Login fallido**:
- Revisar variables de entorno (`Jwt__Key`)
- Verificar connection string a BD

**Retiradas bloqueadas**:
- Validar `Aprovechable` del socio
- Revisar límite mensual (`ConsumicionDelMes`)

---

## 12. Extensiones y Mejoras Recientes

### 12.1 Multiselección en Artículos

- Checkboxes por fila
- Botón "Eliminar seleccionados (N)"
- Borrado en lote via `DELETE /articulos/{id}`

### 12.2 Mejoras en Retiradas POS

- Muestra última retirada del socio
- Autoselección de familia al elegir artículo
- Resaltado visual del artículo seleccionado
- Limpieza de búsqueda al cambiar familia

### 12.3 Dashboard

- Acciones rápidas responsive
- "Visitas hoy" = socios únicos con actividad
- Borrado por pulsación larga en listas

---

## 13. Consideraciones para Otras IAs

### 13.1 Al Modificar este Código

1. **No romper el patrón ServiceResult**:
   ```csharp
   return Ok(ServiceResult<T>.Ok(data));
   return BadRequest(ServiceResult.Fail("mensaje"));
   ```

2. **Mantener consistencia en DTOs**:
   - Nombres en PascalCase
   - Fechas en UTC
   - Decimales con 2 lugares para dinero

3. **Frontend vanilla**:
   - Usar namespaces (`MexClub.Modulo`)
   - Event delegation para elementos dinámicos
   - `debounce` para búsquedas

### 13.2 Patrones Reutilizables

- **Autenticación JWT**: middleware ya configurado
- **Paginación**: `PaginationParams` en todos los listados
- **Validaciones**: FluentValidation en DTOs
- **Error handling**: global exception filter

### 13.3 Tests (si se añaden)

- Unit tests para Services
- Integration tests para Controllers
- E2E tests via Playwright para PWA

---

## 14. Arquitectura Futura (Opcional)

### 14.1 Posibles Mejoras

- **SignalR** para notificaciones en tiempo real
- **Background jobs** (Hangfire) para reportes
- **Caching** (Redis) para consultas frecuentes
- **File storage** (Azure Blob/S3) para firmas/fotos

### 14.2 Microservicios (si crece)

- `Socios Service` (gestión de socios)
- `Dispensario Service` (retiradas, artículos)
- `Tesorería Service` (aportaciones, cuotas)
- `Accesos Service` (fichajes)

---

## 15. Contacto y Soporte

- **Documentación técnica**: `docs/ANALISIS_TECNICO.md`
- **Guía de instalación**: `docs/GUIA_INSTALACION.md`
- **Código legacy referencia**: `docs/RetiradaSala2_Legacy.cs`

**Nota**: Este sistema está diseñado para ser mantenible por desarrolladores .NET junior/intermedio. La arquitectura es simple pero robusta, con separación clara de responsabilidades y patrones modernos.
