-- Check SocioDetalle table
SELECT "Id", "SocioId", "ExentoCuota", "DebeCuota", "CuotaFechaProxima" FROM "SocioDetalles" LIMIT 10;

-- Check if there's any other auxiliary table
SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' ORDER BY table_name;
