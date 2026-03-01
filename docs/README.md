# MexClub - Documentación del Proyecto

## 📚 Documentación Disponible

Este repositorio incluye la siguiente documentación técnica y de uso:

### 🚀 [Guía de Instalación](./GUIA_INSTALACION.md)
- **Público**: Administradores de sistemas, personal técnico
- **Contenido**: Instalación completa del servidor (API + BD) y despliegue de la PWA
- **Cubre**: PostgreSQL, IIS, HTTPS, PWA, migración de datos, troubleshooting

### 🔧 [Guía de Uso Técnico para IAs](./GUIA_USO_TECNICO_IA.md)
- **Público**: Desarrolladores, otras IAs, equipo técnico
- **Contenido**: Arquitectura del sistema, patrones de código, API REST, lógica de negocio
- **Cubre**: Estructura del proyecto, endpoints, frontend, base de datos, migración

### 📊 [Análisis Técnico del Sistema Legacy](./ANALISIS_TECNICO.md)
- **Público**: Arquitectos, equipo de desarrollo
- **Contenido**: Análisis detallado del sistema antiguo (ASMX + Xamarin)
- **Cubre**: Deuda técnica, problemas de seguridad, rendimiento, arquitectura

### 💻 [Código Legacy de Referencia](./RetiradaSala2_Legacy.cs)
- **Público**: Desarrolladores
- **Contenido**: Código fuente original del módulo de retiradas
- **Uso**: Referencia para entender la lógica de negocio original durante la migración

---

## 🏗️ Arquitectura del Sistema

```
┌─────────────────────────────────────────────────────┐
│                    SERVIDOR                          │
│                                                     │
│  ┌──────────────┐    ┌──────────────────────────┐   │
│  │  PostgreSQL   │◄──│  MexClub.Api (.NET 10)   │   │
│  │  (MexClubDb)  │    │                          │   │
│  └──────────────┘    │  • REST API  (/api/...)   │   │
│                      │  • Archivos estáticos     │   │
│                      │    (wwwroot/ = la "app")   │   │
│                      │  • Swagger  (/swagger)    │   │
│                      └─────────┬────────────────┘   │
│                                │ HTTPS              │
└────────────────────────────────┼─────────────────────┘
                                 │
                    ┌────────────┴────────────┐
                    │       INTERNET           │
                    └────────────┬────────────┘
                                 │
              ┌──────────────────┼──────────────────┐
              │                  │                   │
        ┌─────┴─────┐    ┌─────┴─────┐     ┌──────┴──────┐
        │  Tablet    │    │  Móvil     │     │  PC/Mac     │
        │  (PWA)     │    │  (PWA)     │     │  (Navegador)│
        └───────────┘    └───────────┘     └─────────────┘
```

---

## 🚀 Empezar Rápido

### 1. Para Instalar el Sistema
Lee [**Guía de Instalación**](./GUIA_INSTALACION.md) → Configuración completa del servidor y despliegue

### 2. Para Desarrollar o Modificar
Lee [**Guía de Uso Técnico para IAs**](./GUIA_USO_TECNICO_IA.md) → Arquitectura, patrones y buenas prácticas

### 3. Para Entender el Sistema Original
Lee [**Análisis Técnico**](./ANALISIS_TECNICO.md) → Problemas del sistema legacy y decisiones de diseño

---

## 📋 Resumen del Proyecto

**MexClub** es un sistema de gestión para asociaciones de socios con dispensario, completamente modernizado:

- **Backend**: ASP.NET Core 10 + Entity Framework Core + PostgreSQL
- **Frontend**: PWA (Progressive Web App) con HTML/CSS/JavaScript vanilla
- **Características**: Gestión de socios, dispensario/retiradas, cuotas, fichajes, dashboard
- **Despliegue**: Todo servido desde el mismo proyecto (API + frontend estático)

### Módulos Principales

| Módulo | Funcionalidad | Estado |
|--------|---------------|--------|
| **Socios** | CRUD, búsqueda, fotos, referidos | ✅ Completo |
| **Artículos** | CRUD por familia, multiselección | ✅ Completo |
| **Retiradas** | POS con carrito, firma digital, límites | ✅ Completo |
| **Aportaciones** | Ingresos de dinero, dashboard | ✅ Completo |
| **Cuotas** | Pagos mensuales/anuales, estado | ✅ Completo |
| **Accesos** | Fichaje de entrada/salida | ✅ Completo |
| **Dashboard** | Estadísticas, acciones rápidas | ✅ Completo |

---

## 🔧 Tecnologías Clave

### Backend
- **.NET 10** (última versión)
- **ASP.NET Core Web API**
- **Entity Framework Core** (PostgreSQL)
- **JWT Authentication**
- **Serilog** (logging)
- **AutoMapper** (DTOs)

### Frontend
- **HTML5 + CSS3 + JavaScript ES6+** (vanilla)
- **Bootstrap 5** (UI components)
- **Service Worker** (PWA, offline)
- **Signature Pad** (firmas digitales)

### Base de Datos
- **PostgreSQL 15+**
- **Migraciones automáticas**
- **Índices optimizados**

### Despliegue
- **IIS** (Windows Server)
- **HTTPS obligatorio** (PWA)
- **Let's Encrypt** (certificados)

---

## 📞 Soporte y Contacto

- **Documentación**: Revisa las guías detalladas en este directorio
- **Código fuente**: Explora el repositorio para entender implementaciones
- **Issues**: Reporta problemas o sugerencias en el gestor de proyectos

---

## 📝 Notas de Versión

- **v2.0**: Reescritura completa desde sistema legacy
- **Última actualización**: Multiselección artículos, mejoras Retiradas POS, acciones rápidas dashboard
- **Estado**: Producción estable

---

> **Importante**: Esta documentación está diseñada para ser comprensible por desarrolladores .NET junior/intermedio y otras IAs. La arquitectura prioriza la simplicidad y mantenibilidad sobre complejidad innecesaria.
