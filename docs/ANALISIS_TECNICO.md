# MexClub - Análisis Técnico del Sistema Legacy

## 1. Resumen Ejecutivo

El sistema actual es una aplicación de gestión para una asociación de socios, compuesta por:
- **Backend**: Web Services ASMX (.NET Framework 4.0) publicados en IIS
- **Cliente**: Aplicación Android nativa en Xamarin
- **Base de datos**: SQL Server Compact Edition (.sdf)

El sistema presenta **deuda técnica crítica** en todas sus capas.

---

## 2. Análisis del Backend

### 2.1 Tecnología
- **Framework**: .NET Framework 4.0 (obsoleto, sin soporte)
- **Tipo de servicio**: ASMX Web Services (SOAP) - tecnología discontinuada
- **ORM**: Entity Framework 6.4 con Database-First (EDMX)
- **Base de datos**: SQL Server Compact Edition 4.0 (descontinuado por Microsoft)
- **Serialización**: Newtonsoft.Json 10.0.2

### 2.2 Servicios Identificados (20 servicios ASMX)

| Servicio | Responsabilidad | Métodos | Problemas |
|----------|----------------|---------|-----------|
| WebServiceAcceso | Control de accesos/fichajes | 7 | Lógica duplicada con WebServiceFichar |
| WebServiceAportacion | Gestión de aportaciones | 7 | Sin validación de entrada |
| WebServiceArticulo | CRUD de artículos | 6 | Duplicado en WebServiceProductos |
| WebServiceConsumicion | Gestión de consumiciones | 6 | Lógica compleja sin separación |
| WebServiceCuota | Gestión de cuotas | 5 | Acoplamiento con TablaAuxiliar |
| WebServiceFamilia | CRUD de familias | 5 | Duplicado en WebServiceProductos |
| WebServiceFichar | Fichaje de socios | 5 | Duplicación de WebServiceAcceso |
| WebServiceListaCompra | Carrito de compra temporal | 10 | Estado en servidor, no escalable |
| WebServiceLog | Logging a archivo | 1 | Código idéntico a LogException |
| WebServiceLogException | Logging excepciones | 1 | Código idéntico a Log |
| WebServiceLogin | Login administrativo | 2 | Contraseñas en texto plano |
| WebServicePing | Health check | 1 | - |
| WebServiceProductos | Familias + Artículos | 12 | Duplica completamente Familia y Articulo |
| WebServiceReset | Reset de base de datos | 1 | Conexión SQL hardcoded, peligroso |
| WebServiceSocio | CRUD de socios | 12 | El más grande, sin paginación |
| WebServiceTablaAuxiliarSocio | Datos auxiliares socios | 6 | Tabla desnormalizada |
| WebServiceUploadFile | Subida de archivos | 1 | Sin validación, path traversal posible |
| WebServiceUserLogin | Login de usuarios/socios | 4 | Contraseñas en texto plano |
| WebServiceUsuario | CRUD de usuarios admin | 4 | Sin validación |

### 2.3 Problemas Críticos Detectados

#### Seguridad (CRÍTICO)
1. **Contraseñas almacenadas en texto plano** - Login.Password y UserLogin.Userpassword sin hash
2. **Sin autenticación ni autorización** - Cualquier persona puede invocar cualquier endpoint
3. **Connection string hardcoded** en WebServiceReset (`DESKTOP-0LBS0HS\\SQLEXPRESS`)
4. **Inyección SQL potencial** - WebServiceReset usa SQL directo sin parametrizar
5. **Upload sin validación** - Acepta cualquier archivo sin verificar tipo/tamaño
6. **Errores expuestos** - Se devuelven mensajes de excepción internos al cliente
7. **Sin HTTPS forzado**
8. **Sin protección CSRF/XSS**
9. **Sin rate limiting**

#### Rendimiento (ALTO)
1. **Todas las consultas cargan tablas completas en memoria** - `.ToList()` antes de filtrar con LINQ
2. **Sin paginación** - GetAllSocio, GetAllConsumicion, etc. devuelven todos los registros
3. **Sin caché** de ningún tipo
4. **Sin índices** optimizados en la base de datos
5. **Múltiples instancias de DbContext** creadas sin control (no thread-safe)

#### Arquitectura (ALTO)
1. **Sin separación de responsabilidades** - Lógica de negocio mezclada con acceso a datos
2. **Duplicación masiva de código**:
   - WebServiceProductos = WebServiceFamilia + WebServiceArticulo (100% duplicado)
   - WebServiceFichar ≈ WebServiceAcceso (lógica casi idéntica)
   - WebServiceLog ≡ WebServiceLogException (código idéntico)
3. **Singleton de Database roto** - No es thread-safe, cada servicio crea sus propias instancias
4. **Acoplamiento entre servicios** - Servicios instancian otros servicios directamente
5. **Sin inyección de dependencias**
6. **Sin manejo global de errores**
7. **Tablas temporales como workaround** - SocioTemporal, tablaTemporalSocio, ListaCompraTemporal

#### Diseño de Base de Datos (MEDIO)
1. **Sin integridad referencial** - No hay foreign keys definidas
2. **Tablas duplicadas** - Socio, SocioTemporal y tablaTemporalSocio son casi idénticas
3. **TablaAuxiliarSocio desnormalizada** - Datos calculados almacenados redundantemente
4. **Nombres inconsistentes** - Mezcla de español e inglés, camelCase y PascalCase
5. **Tipos de datos subóptimos** - `string` para ReferidoPor (debería ser FK numérica)

---

## 3. Análisis del Cliente Xamarin

### 3.1 Pantallas Identificadas (22 Activities)
1. **Login** - Autenticación de usuario
2. **MenuPrincipal** - Dashboard con navegación a todas las funciones
3. **AccesoUsuarios** - Gestión de accesos
4. **NuevoSocio / EditarSocio** - Alta y edición de socios
5. **NuevaAportacion** - Registro de aportaciones
6. **NuevaFamilia / EditarFamilia** - CRUD de familias de productos
7. **NuevoProducto / EditarProducto** - CRUD de artículos
8. **Cuota** - Gestión de cuotas de socios
9. **Fichar** - Control de entrada/salida
10. **RetiradaSala2** - Dispensario / punto de venta
11. **TotalAportaciones / UltimasAportaciones** - Reportes de aportaciones
12. **TotalConsumiciones / UltimasConsumiciones** - Reportes de consumiciones
13. **UltimosSocios** - Listado de últimos socios
14. **CambiarPassword** - Cambio de contraseña
15. **ReferidoPor** - Consulta de referidos
16. **ActividadReciente** - Log de actividad
17. **Configuracion** - Configuración de la app
18. **DialogFirmaActivity** - Captura de firma digital

### 3.2 Patrones de Consumo
- Usa **Web References SOAP** para comunicarse con el backend
- **AsyncTask** para operaciones asíncronas (patrón obsoleto)
- Serialización JSON manual para enviar datos
- Sin caché local
- Sin manejo offline

---

## 4. Esquema de Base de Datos Actual

### Entidades principales (14 tablas útiles + 2 sistema)

```
Socio (IdSocio PK, IdUsuario, NumSocio, Codigo, ReferidoPor, Nombre, 
       PrimerApellido, SegundoApellido, TipoDocumento, Documento, Pais, 
       Provincia, Localidad, Direccion, CP, Telefono, Email, FechaCumple, 
       FechaAlta, Foto, FotoAnversoDNI, FotoReversoDNI, Activo, NEstrellas, 
       ConsumicionMaxima, Terapeutica, Exento, PagoTarjeta, Comentario)

Usuario (IdUsuario PK, Nombre, Apellidos, TipoDocumento, Documento, Pais, 
         Provincia, Localidad, Direccion, CP, Telefono1, Telefono2, Fax, 
         Email, FechaAlta, Activo)

Familia (IdFamilia PK, IdUsuario, Nombre, Activo, Descuento)

Articulo (IdArticulo PK, IdFamilia, IdUsuario, Nombre, Descripcion, 
          Precio, Cantidad1, Cantidad2, Cantidad3, Cantidad4, Activo, Decimal)

Aportacion (IdAportacion PK, IdSocio, IdUsuario, CantidadAportada, Fecha, Codigo)

Retirada (IdRetirada PK, IdSocio, IdArticulo, IdUsuario, PrecioArticulo, 
          Cantidad, Total, Firma, Fecha)

Cuota (IdCuota PK, IdSocio, Fecha, CantidadCuota, Periodo, IdUsuario, FechaAnterior)

Acceso (IdAcceso PK, IdSocio, TipoAcceso, FechaYHora, Turno, Accion)

TablaAuxiliarSocio (IdSocio PK/FK, CuotaFechaProxima, ConsumicionDelMes, 
                    AportacionDelDia, FechaUltimaConsumicion, FechaUltimaAportacion, 
                    ExentoCuota, DebeCuota, Aprovechable)

Login (IdLogin PK, IdUsuario, NombreUsuario, Password)
UserLogin (IdUserLogin PK, Username, Userpassword, Usermode, FechaAlta, IdSocio)

Borrado (IdBorrado PK, Fecha, Tipo, Descripcion, IdSocio, IdFamilia, 
         IdUsuario, IdArticulo, IdCuota, Cantidad, Total, RealizadoPor)

BorradoCuota (IdBorradoCuota PK, FechaUltimaCuota, Periodo, Cantidad)
ListaCompraTemporal (Id PK, IdSocio, IdArticulo, Cantidad, Parcial)
```

### Tablas a eliminar en migración
- **SocioTemporal** - Duplicado de Socio
- **tablaTemporalSocio** - Duplicado de Socio
- **sysdiagrams** - Metadatos de diseñador

---

## 5. Evaluación de Riesgos

| Riesgo | Impacto | Probabilidad | Mitigación |
|--------|---------|-------------|------------|
| Pérdida de datos en migración DB | Alto | Medio | Script de migración con rollback, backup previo |
| Contraseñas irrecuperables | Medio | Alto | Forzar reset de contraseñas post-migración |
| Funcionalidad no documentada | Medio | Medio | Análisis exhaustivo del código fuente |
| Incompatibilidad de tipos de datos | Bajo | Medio | Mapeo explícito en scripts de migración |
| Resistencia al cambio de UI | Medio | Bajo | Diseño intuitivo, mismo flujo funcional |

---

## 6. Decisión Técnica: Base de Datos

**Elección: SQL Server** (Express para desarrollo, Standard/Enterprise para producción)

### Justificación:
1. **Migración natural** desde SQL Server Compact - tipos de datos compatibles
2. **Integración nativa** con .NET / EF Core - proveedor de primera clase
3. **Herramientas maduras** - SSMS, Azure Data Studio, migrations
4. **Edición Express gratuita** - suficiente para el volumen actual
5. **Escalabilidad** - ruta clara a SQL Server Standard/Enterprise o Azure SQL
6. **Soporte empresarial** - documentación, comunidad, soporte Microsoft
7. **Características avanzadas** - índices, vistas, procedimientos, cifrado TDE
