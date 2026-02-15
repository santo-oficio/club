# MexClub - Sistema de Gestión Modernizado

## Descripción

Sistema moderno de gestión para asociación, migrado desde una arquitectura legacy (.NET Framework 4.0 + ASMX + SQL Server Compact Edition) a una solución moderna basada en **Clean Architecture**.

## Stack Tecnológico

| Capa | Tecnología |
|---|---|
| **Backend** | .NET 10, ASP.NET Core REST API |
| **ORM** | Entity Framework Core |
| **Base de Datos** | SQL Server |
| **Autenticación** | JWT Bearer Tokens + Refresh Tokens |
| **Frontend** | HTML5, Bootstrap 5.3, jQuery, CSS3 |
| **Logging** | Serilog (Consola + Archivo) |
| **Documentación API** | Swagger / OpenAPI |
| **Rate Limiting** | AspNetCoreRateLimit |

## Arquitectura (Clean Architecture)

```
src/
├── MexClub.Domain/          # Entidades, interfaces, enums (sin dependencias externas)
├── MexClub.Application/     # DTOs, interfaces de servicio, validaciones
├── MexClub.Infrastructure/  # EF Core DbContext, repositorios, servicios
└── MexClub.Api/             # Controllers, middleware, Program.cs, frontend
    └── wwwroot/             # Frontend web (HTML/CSS/JS)
```

## Requisitos Previos

- .NET SDK 10.0+
- SQL Server (Express o superior)
- Node.js (opcional, solo si se necesita tooling frontend)

## Configuración

### 1. Base de Datos

Ejecutar el script de migración:

```sql
-- scripts/migration/001_create_schema.sql
```

### 2. Secrets (NO hardcodear en appsettings.json)

Usar User Secrets para desarrollo:

```bash
cd src/MexClub.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:MexClubDb" "Server=TU_SERVER;Database=MexClubDb;Trusted_Connection=True;TrustServerCertificate=True"
dotnet user-secrets set "Jwt:Key" "TU-CLAVE-SECRETA-MINIMO-32-CARACTERES-AQUI"
```

Para producción, usar variables de entorno o Azure Key Vault.

### 3. Ejecutar

```bash
cd src/MexClub.Api
dotnet run
```

La aplicación estará disponible en:
- **Web**: https://localhost:5001
- **Swagger**: https://localhost:5001/swagger

## API Endpoints

| Método | Ruta | Descripción | Auth |
|---|---|---|---|
| POST | `/api/auth/login` | Login | No |
| POST | `/api/auth/refresh` | Refresh token | No |
| POST | `/api/auth/change-password` | Cambiar contraseña | Sí |
| GET | `/api/socios` | Listar socios | Sí |
| GET | `/api/socios/{id}` | Detalle socio | Sí |
| POST | `/api/socios` | Crear socio | Admin |
| PUT | `/api/socios/{id}` | Actualizar socio | Admin |
| DELETE | `/api/socios/{id}` | Desactivar socio | Admin |
| GET | `/api/familias` | Listar familias | Sí |
| POST | `/api/familias` | Crear familia | Admin |
| GET | `/api/articulos` | Listar artículos | Sí |
| POST | `/api/articulos` | Crear artículo | Admin |
| GET | `/api/aportaciones` | Listar aportaciones | Sí |
| POST | `/api/aportaciones` | Crear aportación | Admin |
| POST | `/api/accesos/fichar` | Fichaje entrada/salida | Admin |
| GET | `/api/cuotas` | Listar cuotas | Sí |
| POST | `/api/cuotas` | Crear cuota | Admin |
| GET | `/api/ping` | Health check | No |

## Seguridad

- JWT con clave simétrica configurable (nunca hardcodeada)
- Refresh tokens con revocación
- Rate limiting (5 intentos/min en login, 60 req/min general)
- Global exception handling (sin stack traces expuestos)
- Passwords hasheados con SHA-256
- CORS configurable
- Protección anti-XSS via Content Security headers
- Roles: `admin` (gestión completa), `socio` (lectura + fichaje)

## Frontend

Aplicación web responsive mobile-first servida como SPA desde `wwwroot/`:

- **Bootstrap 5.3** para UI responsive
- **jQuery 3.7** para interactividad
- **PWA manifest** preparado para instalación
- Bottom navigation estilo app nativa
- Diseño discreto y profesional
- Compatible con móviles, tablets y desktop

## Estructura de la Base de Datos

Las tablas principales son:

- **Socios** + **SocioDetalles** - Miembros de la asociación
- **Usuarios** + **Logins** - Personal administrativo
- **UserLogins** - Acceso de socios a la app
- **Familias** + **Artículos** - Catálogo de productos
- **Aportaciones** - Contribuciones económicas
- **Retiradas** - Consumiciones/dispensaciones
- **Cuotas** - Pagos periódicos
- **Accesos** - Registro de entrada/salida
- **RefreshTokens** - Tokens de sesión
- **RegistrosBorrados** - Auditoría de eliminaciones

## Migración desde Sistema Legacy

El script `scripts/migration/001_create_schema.sql` crea el esquema desde cero. Para migrar datos existentes desde `clubdb.sdf`:

1. Exportar datos del .sdf a CSV o INSERT scripts
2. Ejecutar `001_create_schema.sql` en SQL Server
3. Importar datos adaptando los nombres de columna
4. Hashear las contraseñas existentes (estaban en texto plano)

## Licencia

Proyecto privado. Todos los derechos reservados.
