using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlServerCe;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace MigradorDatos
{
    /// <summary>
    /// Herramienta de migración de datos: ClubDB.sdf (SQL CE 4.0) → PostgreSQL
    /// 
    /// Uso:
    ///   MigradorDatos.exe "C:\Server\ClubDB.sdf" "Host=localhost;Port=4433;Database=MexClubDb;Username=mexclub;Password=pass"
    /// 
    /// Requisitos:
    ///   - La BD PostgreSQL debe existir con el esquema ya creado (001_create_schema.sql)
    ///   - El archivo ClubDB.sdf debe ser accesible
    /// </summary>
    class Program
    {
        static int _totalMigrated = 0;
        static HashSet<long> _usuarioIds = new HashSet<long>();
        static HashSet<long> _socioIds = new HashSet<long>();
        static HashSet<long> _articuloIds = new HashSet<long>();

        // Mapas para resolución de referencias
        static Dictionary<long, string> _referidoPorMap = new Dictionary<long, string>();
        static Dictionary<string, long> _codigoToId = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        static Dictionary<string, long> _nombreToId = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        static Dictionary<int, long> _numSocioToId = new Dictionary<int, long>();

        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("═══════════════════════════════════════════════");
            Console.WriteLine("  MexClub - Migrador de Datos");
            Console.WriteLine("  ClubDB.sdf (SQL CE) → PostgreSQL");
            Console.WriteLine("═══════════════════════════════════════════════");
            Console.WriteLine();

            string sdfPath = args.Length > 0 ? args[0] : @"C:\Server\ClubDB.sdf";
            string pgConn = args.Length > 1 ? args[1] : "Host=localhost;Port=4433;Database=MexClubDb;Username=mexclub;Password=mexclub";

            if (!File.Exists(sdfPath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ERROR: No se encuentra el archivo .sdf: {sdfPath}");
                Console.ResetColor();
                Console.WriteLine("Uso: MigradorDatos.exe <ruta_clubdb.sdf> <connection_string_postgresql>");
                return;
            }

            string ceConn = $"Data Source={sdfPath};Max Database Size=4091;";

            // Asegurar UTF-8 en la conexión PostgreSQL
            if (!pgConn.Contains("Client Encoding", StringComparison.OrdinalIgnoreCase))
                pgConn += ";Client Encoding=UTF8";
            if (!pgConn.Contains("Encoding", StringComparison.OrdinalIgnoreCase))
                pgConn += ";Encoding=UTF8";

            try
            {
                using (var ceConnection = new SqlCeConnection(ceConn))
                using (var pgConnection = new NpgsqlConnection(pgConn))
                {
                    ceConnection.Open();
                    pgConnection.Open();

                    // Forzar UTF-8 en la sesión PostgreSQL para evitar conversiones WIN1252
                    ExecutePg(pgConnection, "SET client_encoding TO 'UTF8';");

                    Console.WriteLine($"Origen:  {sdfPath}");
                    Console.WriteLine($"Destino: PostgreSQL ({pgConnection.Host}:{pgConnection.Port}/{pgConnection.Database})");
                    Console.WriteLine();

                    // Borrado previo para recarga limpia (orden respetando FKs)
                    PurgeTargetTables(pgConnection);

                    // Orden de migración respeta dependencias FK
                    MigrateUsuarios(ceConnection, pgConnection);
                    MigrateSocios(ceConnection, pgConnection);

                    MigrateFamilias(ceConnection, pgConnection);
                    MigrateArticulos(ceConnection, pgConnection);

                    // Recuperar huérfanos (crear padres ficticios para FKs rotas)
                    RecoverOrphans(ceConnection, pgConnection);

                    // Resolver ReferidoPor (ahora que ya existen posibles huérfanos recuperados)
                    ResolveReferidos(pgConnection);

                    MigrateSocioDetalles(ceConnection, pgConnection);
                    MigrateUserLogins(ceConnection, pgConnection);
                    MigrateAportaciones(ceConnection, pgConnection);
                    MigrateRetiradas(ceConnection, pgConnection);
                    MigrateCuotas(ceConnection, pgConnection);
                    MigrateAccesos(ceConnection, pgConnection);
                    MigrateBorrados(ceConnection, pgConnection);

                    // Asegurar admin tras borrado (cascade delete lo elimina al borrar usuarios)
                    CreateAdminUser(pgConnection);

                    // Ajustar secuencias SERIAL al máximo ID insertado
                    ResetSequences(pgConnection);
                }

                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"═══ MIGRACIÓN COMPLETADA: {_totalMigrated} registros totales ═══");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ERROR FATAL: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Console.ResetColor();
            }

            Console.WriteLine();
            Console.WriteLine("Pulsa cualquier tecla para salir...");
            Console.ReadKey();
        }

        // ════════════════════════════════════════════
        // USUARIOS
        // ════════════════════════════════════════════
        static void MigrateUsuarios(SqlCeConnection ce, NpgsqlConnection pg)
        {
            var sql = @"INSERT INTO ""Usuarios"" 
                (""Id"", ""Nombre"", ""Apellidos"", ""TipoDocumento"", ""Documento"", ""Pais"", ""Provincia"", ""Localidad"", 
                 ""Direccion"", ""CodigoPostal"", ""Telefono1"", ""Telefono2"", ""Email"", ""FechaAlta"", ""IsActive"", ""CreatedAt"")
                VALUES 
                (@Id, @Nombre, @Apellidos, @TipoDocumento, @Documento, @Pais, @Provincia, @Localidad,
                 @Direccion, @CP, @Tel1, @Tel2, @Email, @FechaAlta, @Activo, @FechaAlta)
                ON CONFLICT (""Id"") DO NOTHING";

            int count = 0;
            using (var reader = ReadCe(ce, "SELECT * FROM Usuario"))
            {
                while (reader.Read())
                {
                    using (var cmd = new NpgsqlCommand(sql, pg))
                    {
                        long id = (long)reader["IdUsuario"];
                        _usuarioIds.Add(id);

                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.Parameters.AddWithValue("@Nombre", reader["Nombre"]?.ToString() ?? "");
                        cmd.Parameters.AddWithValue("@Apellidos", reader["Apellidos"]?.ToString() ?? "");
                        cmd.Parameters.AddWithValue("@TipoDocumento", reader["TipoDocumento"]?.ToString() ?? "DNI");
                        cmd.Parameters.AddWithValue("@Documento", reader["Documento"]?.ToString() ?? "");
                        cmd.Parameters.AddWithValue("@Pais", OrNull(reader["Pais"]));
                        cmd.Parameters.AddWithValue("@Provincia", OrNull(reader["Provincia"]));
                        cmd.Parameters.AddWithValue("@Localidad", OrNull(reader["Localidad"]));
                        cmd.Parameters.AddWithValue("@Direccion", OrNull(reader["Direccion"]));
                        cmd.Parameters.AddWithValue("@CP", OrNull(reader["CP"]));
                        cmd.Parameters.AddWithValue("@Tel1", OrNull(reader["Telefono1"]));
                        cmd.Parameters.AddWithValue("@Tel2", OrNull(reader["Telefono2"]));
                        cmd.Parameters.AddWithValue("@Email", OrNull(reader["Email"]));
                        cmd.Parameters.AddWithValue("@FechaAlta", (DateTime)reader["FechaAlta"]);
                        cmd.Parameters.AddWithValue("@Activo", (bool)reader["Activo"]);
                        cmd.ExecuteNonQuery();
                        count++;
                    }
                }
            }
            LogTable("Usuarios", count);
        }

        // ════════════════════════════════════════════
        // SOCIOS
        // ════════════════════════════════════════════
        static void MigrateSocios(SqlCeConnection ce, NpgsqlConnection pg)
        {
            var sql = @"INSERT INTO ""Socios"" 
                (""Id"", ""NumSocio"", ""Codigo"", ""Nombre"", ""PrimerApellido"", ""SegundoApellido"",
                 ""TipoDocumento"", ""Documento"", ""Pais"", ""Provincia"", ""Localidad"", ""Direccion"",
                 ""CodigoPostal"", ""Telefono"", ""Email"", ""FechaNacimiento"", ""FechaAlta"",
                 ""FotoUrl"", ""FotoAnversoDniUrl"", ""FotoReversoDniUrl"",
                 ""Estrellas"", ""ConsumicionMaximaMensual"", ""EsTerapeutica"", ""EsExento"", ""PagoConTarjeta"",
                 ""Comentario"", ""IsActive"", ""CreatedAt"")
                VALUES 
                (@Id, @NumSocio, @Codigo, @Nombre, @Ap1, @Ap2,
                 @TipoDoc, @Doc, @Pais, @Prov, @Loc, @Dir,
                 @CP, @Tel, @Email, @FechaNac, @FechaAlta,
                 @Foto, @FotoDniA, @FotoDniR,
                 @Estrellas, @ConsMax, @Terap, @Exento, @Tarjeta,
                 @Comentario, @Activo, @FechaAlta)
                ON CONFLICT (""Id"") DO NOTHING";

            // Limpiar mapas estáticos por si acaso
            _referidoPorMap.Clear();
            _codigoToId.Clear();
            _nombreToId.Clear();
            _numSocioToId.Clear();

            int count = 0;
            using (var reader = ReadCe(ce, "SELECT * FROM Socio"))
            {
                while (reader.Read())
                {
                    long id = (long)reader["IdSocio"];
                    string codigo = CleanText(reader["Codigo"]) ?? "";
                    string nombre = CleanText(reader["Nombre"]) ?? "";
                    string ap1 = CleanText(reader["PrimerApellido"]) ?? "";
                    string ap2 = CleanText(reader["SegundoApellido"]) ?? "";
                    string nombreCompleto = $"{nombre} {ap1} {ap2}".Trim();
                    int numSocio = (int)reader["NumSocio"];

                    // Buscar columna ReferidoPor con varios nombres posibles
                    string referidoPor = ReadText(reader, "ReferidoPor", "Referido_Por", "IdReferido", "Referido Por", "CodigoReferidoPor", "Referido");

                    if (!string.IsNullOrWhiteSpace(codigo)) _codigoToId[codigo] = id;
                    if (!string.IsNullOrWhiteSpace(nombreCompleto)) _nombreToId[nombreCompleto] = id;
                    _numSocioToId[numSocio] = id;

                    if (!string.IsNullOrWhiteSpace(referidoPor))
                        _referidoPorMap[id] = referidoPor;

                    _socioIds.Add(id);

                    using (var cmd = new NpgsqlCommand(sql, pg))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.Parameters.AddWithValue("@NumSocio", numSocio);
                        cmd.Parameters.AddWithValue("@Codigo", codigo);
                        cmd.Parameters.AddWithValue("@Nombre", nombre);
                        cmd.Parameters.AddWithValue("@Ap1", ap1);
                        cmd.Parameters.AddWithValue("@Ap2", OrNull(ap2));
                        cmd.Parameters.AddWithValue("@TipoDoc", CleanText(reader["TipoDocumento"]) ?? "DNI");
                        cmd.Parameters.AddWithValue("@Doc", CleanText(reader["Documento"])) ;
                        cmd.Parameters.AddWithValue("@Pais", OrNull(CleanText(reader["Pais"])));
                        cmd.Parameters.AddWithValue("@Prov", OrNull(CleanText(reader["Provincia"])));
                        cmd.Parameters.AddWithValue("@Loc", OrNull(CleanText(reader["Localidad"])));
                        cmd.Parameters.AddWithValue("@Dir", OrNull(CleanText(reader["Direccion"])));
                        cmd.Parameters.AddWithValue("@CP", OrNull(CleanText(reader["CP"])));
                        cmd.Parameters.AddWithValue("@Tel", OrNull(CleanText(reader["Telefono"])));
                        cmd.Parameters.AddWithValue("@Email", OrNull(CleanText(reader["Email"])));
                        cmd.Parameters.AddWithValue("@FechaNac", OrNullDate(reader["FechaCumple"]));
                        cmd.Parameters.AddWithValue("@FechaAlta", (DateTime)reader["FechaAlta"]);
                        cmd.Parameters.AddWithValue("@Foto", PrefixPath(CleanText(reader["Foto"]), "/resources/fotos/"));
                        cmd.Parameters.AddWithValue("@FotoDniA", PrefixPath(CleanText(reader["FotoAnversoDNI"]), "/resources/documentos/"));
                        cmd.Parameters.AddWithValue("@FotoDniR", PrefixPath(CleanText(reader["FotoReversoDNI"]), "/resources/documentos/"));
                        
                        // Buscar Estrellas con varios nombres posibles
                        cmd.Parameters.AddWithValue("@Estrellas", ReadShort(reader, "NEstrellas", "N_Estrellas", "Estrellas", "NumeroEstrellas", "Stars"));
                        
                        cmd.Parameters.AddWithValue("@ConsMax", (int)reader["ConsumicionMaxima"]);
                        cmd.Parameters.AddWithValue("@Terap", (bool)reader["Terapeutica"]);
                        cmd.Parameters.AddWithValue("@Exento", (bool)reader["Exento"]);
                        cmd.Parameters.AddWithValue("@Tarjeta", (bool)reader["PagoTarjeta"]);
                        cmd.Parameters.AddWithValue("@Comentario", OrNull(CleanText(reader["Comentario"])));
                        cmd.Parameters.AddWithValue("@Activo", (bool)reader["Activo"]);
                        cmd.ExecuteNonQuery();
                        count++;
                    }
                }
            }

            LogTable("Socios", count);
        }

        // ════════════════════════════════════════════
        // RESOLVER REFERIDOS
        // ════════════════════════════════════════════
        static void ResolveReferidos(NpgsqlConnection pg)
        {
            Console.WriteLine("  Resolviendo campo 'ReferidoPor'...");

            int refs = 0;
            int failed = 0;
            foreach (var kv in _referidoPorMap)
            {
                long refId = 0;
                string refValue = kv.Value.Trim();

                // 1. Por Código
                if (_codigoToId.TryGetValue(refValue, out long byCode)) refId = byCode;
                // 2. Por ID directo (si es número)
                else if (long.TryParse(refValue, out long byId) && _socioIds.Contains(byId)) refId = byId;
                // 3. Por NumSocio (si es número)
                else if (int.TryParse(refValue, out int byNum) && _numSocioToId.TryGetValue(byNum, out long byNumId)) refId = byNumId;
                // 4. Por Nombre Completo
                else if (_nombreToId.TryGetValue(refValue, out long byName)) refId = byName;

                if (refId != 0)
                {
                    ExecutePg(pg, @"UPDATE ""Socios"" SET ""ReferidoPorSocioId"" = @ref WHERE ""Id"" = @id",
                        ("@ref", (object)refId), ("@id", (object)kv.Key));
                    refs++;
                }
                else
                {
                    failed++;
                    if (failed <= 5) Console.WriteLine($"      ⚠ No se pudo resolver 'ReferidoPor': '{refValue}' (SocioId: {kv.Key})");
                }
            }

            if (_referidoPorMap.Count > 0)
            {
                Console.WriteLine($"    └─ {refs} resueltos, {failed} no encontrados (de {_referidoPorMap.Count})");
            }
            Console.WriteLine();
        }

        // ════════════════════════════════════════════
        // SOCIO DETALLES (TablaAuxiliarSocio)
        // ════════════════════════════════════════════
        static void MigrateSocioDetalles(SqlCeConnection ce, NpgsqlConnection pg)
        {
            var sql = @"INSERT INTO ""SocioDetalles"" 
                (""SocioId"", ""CuotaFechaProxima"", ""ConsumicionDelMes"", ""AportacionDelDia"",
                 ""FechaUltimaConsumicion"", ""FechaUltimaAportacion"", ""ExentoCuota"", ""DebeCuota"", ""Aprovechable"")
                VALUES 
                (@SocioId, @CuotaProx, @ConsMes, @AporDia,
                 @FechaCons, @FechaApor, @Exento, @Debe, @Aprove)
                ON CONFLICT (""SocioId"") DO NOTHING";

            int count = 0;
            using (var reader = ReadCe(ce, "SELECT * FROM TablaAuxiliarSocio"))
            {
                while (reader.Read())
                {
                    long socioId = (long)reader["IdSocio"];
                    if (!_socioIds.Contains(socioId))
                    {
                        Console.WriteLine($"  ⚠ SocioDetalle para Socio {socioId} saltado: Socio sigue sin existir tras recuperación.");
                        continue; 
                    }

                    using (var cmd = new NpgsqlCommand(sql, pg))
                    {
                        cmd.Parameters.AddWithValue("@SocioId", socioId);
                        cmd.Parameters.AddWithValue("@CuotaProx", OrNullDate(reader["CuotaFechaProxima"]));
                        cmd.Parameters.AddWithValue("@ConsMes", ToDecimal(reader["ConsumicionDelMes"]));
                        cmd.Parameters.AddWithValue("@AporDia", OrNullDecimal(reader["AportacionDelDia"]));
                        cmd.Parameters.AddWithValue("@FechaCons", OrNullDate(reader["FechaUltimaConsumicion"]));
                        cmd.Parameters.AddWithValue("@FechaApor", OrNullDate(reader["FechaUltimaAportacion"]));
                        cmd.Parameters.AddWithValue("@Exento", (bool)reader["ExentoCuota"]);
                        cmd.Parameters.AddWithValue("@Debe", (bool)reader["DebeCuota"]);
                        cmd.Parameters.AddWithValue("@Aprove", ToDecimal(reader["Aprovechable"]));
                        cmd.ExecuteNonQuery();
                        count++;
                    }
                }
            }
            LogTable("SocioDetalles", count);
        }

        // ════════════════════════════════════════════
        // USERLOGINS (passwords en texto plano → SHA-256)
        // ════════════════════════════════════════════
        static void MigrateUserLogins(SqlCeConnection ce, NpgsqlConnection pg)
        {
            var sql = @"INSERT INTO ""UserLogins"" 
                (""Id"", ""Username"", ""PasswordHash"", ""Rol"", ""FechaAlta"", ""SocioId"", ""IsActive"", ""CreatedAt"")
                VALUES (@Id, @User, @Hash, @Rol, @FechaAlta, @SocioId, TRUE, @FechaAlta)
                ON CONFLICT (""Id"") DO NOTHING";

            int count = 0;
            using (var reader = ReadCe(ce, "SELECT * FROM UserLogin"))
            {
                while (reader.Read())
                {
                    long socioId = (long)reader["IdSocio"];
                    if (!_socioIds.Contains(socioId))
                    {
                        Console.WriteLine($"  ⚠ UserLogin {reader["IdUserLogin"]} saltado: Socio {socioId} no existe.");
                        continue;
                    }

                    string plainPass = reader["Userpassword"]?.ToString() ?? "";
                    string hash = HashPassword(plainPass);
                    string mode = reader["Usermode"]?.ToString() ?? "socio";
                    string rol = mode.Equals("admin", StringComparison.OrdinalIgnoreCase) ? "admin" : "socio";

                    using (var cmd = new NpgsqlCommand(sql, pg))
                    {
                        cmd.Parameters.AddWithValue("@Id", (long)reader["IdUserLogin"]);
                        cmd.Parameters.AddWithValue("@User", reader["Username"]?.ToString() ?? "");
                        cmd.Parameters.AddWithValue("@Hash", hash);
                        cmd.Parameters.AddWithValue("@Rol", rol);
                        cmd.Parameters.AddWithValue("@FechaAlta", (DateTime)reader["FechaAlta"]);
                        cmd.Parameters.AddWithValue("@SocioId", socioId);
                        cmd.ExecuteNonQuery();
                        count++;
                    }
                }
            }
            LogTable("UserLogins", count);
        }

        // ════════════════════════════════════════════
        // FAMILIAS
        // ════════════════════════════════════════════
        static void MigrateFamilias(SqlCeConnection ce, NpgsqlConnection pg)
        {
            var sql = @"INSERT INTO ""Familias"" (""Id"", ""Nombre"", ""Descuento"", ""IsActive"", ""CreatedAt"")
                VALUES (@Id, @Nombre, @Desc, @Activo, @Now)
                ON CONFLICT (""Id"") DO NOTHING";

            int count = 0;
            using (var reader = ReadCe(ce, "SELECT * FROM Familia"))
            {
                while (reader.Read())
                {
                    using (var cmd = new NpgsqlCommand(sql, pg))
                    {
                        cmd.Parameters.AddWithValue("@Id", (long)reader["IdFamilia"]);
                        cmd.Parameters.AddWithValue("@Nombre", reader["Nombre"]?.ToString() ?? "");
                        cmd.Parameters.AddWithValue("@Desc", OrNullInt(reader["Descuento"]));
                        cmd.Parameters.AddWithValue("@Activo", (bool)reader["Activo"]);
                        cmd.Parameters.AddWithValue("@Now", DateTime.UtcNow);
                        cmd.ExecuteNonQuery();
                        count++;
                    }
                }
            }
            LogTable("Familias", count);
        }

        // ════════════════════════════════════════════
        // ARTICULOS
        // ════════════════════════════════════════════
        static void MigrateArticulos(SqlCeConnection ce, NpgsqlConnection pg)
        {
            var sql = @"INSERT INTO ""Articulos"" 
                (""Id"", ""FamiliaId"", ""Nombre"", ""Descripcion"", ""Precio"", ""Cantidad1"", ""Cantidad2"", ""Cantidad3"", ""Cantidad4"",
                 ""EsDecimal"", ""IsActive"", ""CreatedAt"")
                VALUES (@Id, @FamId, @Nombre, @Desc, @Precio, @C1, @C2, @C3, @C4, @EsDec, @Activo, @Now)
                ON CONFLICT (""Id"") DO NOTHING";

            int count = 0;
            using (var reader = ReadCe(ce, "SELECT * FROM Articulo"))
            {
                while (reader.Read())
                {
                    long id = (long)reader["IdArticulo"];
                    _articuloIds.Add(id);

                    using (var cmd = new NpgsqlCommand(sql, pg))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.Parameters.AddWithValue("@FamId", (long)reader["IdFamilia"]);
                        cmd.Parameters.AddWithValue("@Nombre", reader["Nombre"]?.ToString() ?? "");
                        cmd.Parameters.AddWithValue("@Desc", OrNull(reader["Descripcion"]));
                        cmd.Parameters.AddWithValue("@Precio", ToDecimal(reader["Precio"]));
                        cmd.Parameters.AddWithValue("@C1", ToDecimal(reader["Cantidad1"]));
                        cmd.Parameters.AddWithValue("@C2", OrNullDecimal(reader["Cantidad2"]));
                        cmd.Parameters.AddWithValue("@C3", OrNullDecimal(reader["Cantidad3"]));
                        cmd.Parameters.AddWithValue("@C4", OrNullDecimal(reader["Cantidad4"]));
                        cmd.Parameters.AddWithValue("@EsDec", (bool)reader["Decimal"]);
                        cmd.Parameters.AddWithValue("@Activo", (bool)reader["Activo"]);
                        cmd.Parameters.AddWithValue("@Now", DateTime.UtcNow);
                        cmd.ExecuteNonQuery();
                        count++;
                    }
                }
            }
            LogTable("Articulos", count);
        }

        // ════════════════════════════════════════════
        // RECUPERACIÓN DE HUÉRFANOS
        // ════════════════════════════════════════════
        static void RecoverOrphans(SqlCeConnection ce, NpgsqlConnection pg)
        {
            Console.WriteLine("  Analizando integridad referencial y recuperando huérfanos...");
            
            // 1. Recolectar referencias usadas en tablas hijas
            var usedSocioIds = new HashSet<long>();
            var usedUsuarioIds = new HashSet<long>();
            var usedArticuloIds = new HashSet<long>();

            // Añadir también los IDs de referidos si son numéricos (para recuperar socios antiguos borrados que siguen referenciados)
            foreach (var val in _referidoPorMap.Values)
            {
                if (long.TryParse(val, out long idRef)) usedSocioIds.Add(idRef);
            }

            // Helper local para acumular
            void ScanTable(string query, string colSocio, string colUser, string colArt)
            {
                using (var reader = ReadCe(ce, query))
                {
                    while (reader.Read())
                    {
                        if (colSocio != null && reader[colSocio] != DBNull.Value) usedSocioIds.Add(Convert.ToInt64(reader[colSocio]));
                        if (colUser != null && reader[colUser] != DBNull.Value) usedUsuarioIds.Add(Convert.ToInt64(reader[colUser]));
                        if (colArt != null && reader[colArt] != DBNull.Value) usedArticuloIds.Add(Convert.ToInt64(reader[colArt]));
                    }
                }
            }

            // Escaneo
            try { ScanTable("SELECT IdSocio FROM TablaAuxiliarSocio", "IdSocio", null, null); } catch { }
            try { ScanTable("SELECT IdSocio, IdUsuario FROM Aportacion", "IdSocio", "IdUsuario", null); } catch { }
            try { ScanTable("SELECT IdSocio, IdUsuario, IdArticulo FROM Retirada", "IdSocio", "IdUsuario", "IdArticulo"); } catch { }
            try { ScanTable("SELECT IdSocio, IdUsuario FROM Cuota", "IdSocio", "IdUsuario", null); } catch { }
            try { ScanTable("SELECT IdSocio FROM Acceso", "IdSocio", null, null); } catch { }
            try { ScanTable("SELECT IdSocio FROM UserLogin", "IdSocio", null, null); } catch { }

            // 2. Crear Socios Faltantes
            int sociosRec = 0;
            foreach (var id in usedSocioIds)
            {
                if (!_socioIds.Contains(id))
                {
                    // Insertar socio placeholder
                    var sql = @"INSERT INTO ""Socios"" 
                        (""Id"", ""NumSocio"", ""Codigo"", ""Nombre"", ""PrimerApellido"", ""TipoDocumento"", ""Documento"", ""FechaAlta"", ""IsActive"", ""CreatedAt"")
                        VALUES 
                        (@Id, @Num, @Cod, @Nom, @Ap1, 'DNI', @Doc, @Now, FALSE, @Now)
                        ON CONFLICT (""Id"") DO NOTHING";
                    
                    ExecutePg(pg, sql, 
                        ("@Id", id),
                        ("@Num", (int)(900000 + id)), // NumSocio ficticio alto
                        ("@Cod", $"REC-{id}"),
                        ("@Nom", $"Socio Recuperado {id}"),
                        ("@Ap1", "(Datos perdidos)"),
                        ("@Doc", $"REC-{id}"),
                        ("@Now", DateTime.UtcNow)
                    );
                    
                    _socioIds.Add(id);
                    sociosRec++;
                }
            }
            if (sociosRec > 0) Console.WriteLine($"    └─ Recuperados {sociosRec} Socios huérfanos (marcados como inactivos)");

            // 3. Crear Usuarios Faltantes
            int usersRec = 0;
            foreach (var id in usedUsuarioIds)
            {
                if (!_usuarioIds.Contains(id))
                {
                    var sql = @"INSERT INTO ""Usuarios"" 
                        (""Id"", ""Nombre"", ""Apellidos"", ""TipoDocumento"", ""Documento"", ""FechaAlta"", ""IsActive"", ""CreatedAt"")
                        VALUES 
                        (@Id, @Nom, @Ap, 'DNI', @Doc, @Now, FALSE, @Now)
                        ON CONFLICT (""Id"") DO NOTHING";

                    ExecutePg(pg, sql,
                        ("@Id", id),
                        ("@Nom", $"Usuario Recuperado {id}"),
                        ("@Ap", "(Origen desconocido)"),
                        ("@Doc", $"USR-{id}"),
                        ("@Now", DateTime.UtcNow)
                    );

                    _usuarioIds.Add(id);
                    usersRec++;
                }
            }
            if (usersRec > 0) Console.WriteLine($"    └─ Recuperados {usersRec} Usuarios huérfanos");

            // 4. Crear Articulos Faltantes
            int artsRec = 0;
            long familiaRecId = 9999;
            bool familiaCreated = false;

            foreach (var id in usedArticuloIds)
            {
                if (!_articuloIds.Contains(id))
                {
                    if (!familiaCreated)
                    {
                        // Crear familia para artículos recuperados si no existe
                        ExecutePg(pg, @"INSERT INTO ""Familias"" (""Id"", ""Nombre"", ""IsActive"", ""CreatedAt"") 
                            VALUES (@Id, 'Recuperados', FALSE, NOW()) ON CONFLICT (""Id"") DO NOTHING", 
                            ("@Id", familiaRecId));
                        familiaCreated = true;
                    }

                    var sql = @"INSERT INTO ""Articulos"" 
                        (""Id"", ""FamiliaId"", ""Nombre"", ""Descripcion"", ""Precio"", ""Cantidad1"", ""EsDecimal"", ""IsActive"", ""CreatedAt"")
                        VALUES 
                        (@Id, @FamId, @Nom, 'Recuperado por integridad referencial', 0, 0, FALSE, FALSE, NOW())
                        ON CONFLICT (""Id"") DO NOTHING";

                    ExecutePg(pg, sql,
                        ("@Id", id),
                        ("@FamId", familiaRecId),
                        ("@Nom", $"Articulo {id} (Recuperado)")
                    );

                    _articuloIds.Add(id);
                    artsRec++;
                }
            }
            if (artsRec > 0) Console.WriteLine($"    └─ Recuperados {artsRec} Artículos huérfanos (en familia 'Recuperados')");
            Console.WriteLine();
        }

        // ════════════════════════════════════════════
        // APORTACIONES
        // ════════════════════════════════════════════
        static void MigrateAportaciones(SqlCeConnection ce, NpgsqlConnection pg)
        {
            var sql = @"INSERT INTO ""Aportaciones"" 
                (""Id"", ""SocioId"", ""UsuarioId"", ""CantidadAportada"", ""Fecha"", ""Codigo"", ""IsActive"", ""CreatedAt"")
                VALUES (@Id, @SocioId, @UsuarioId, @Cantidad, @Fecha, @Codigo, TRUE, @Fecha)
                ON CONFLICT (""Id"") DO NOTHING";

            int count = 0;
            using (var reader = ReadCe(ce, "SELECT * FROM Aportacion"))
            {
                while (reader.Read())
                {
                    long socioId = (long)reader["IdSocio"];
                    long usuarioId = (long)reader["IdUsuario"];

                    if (!_socioIds.Contains(socioId) || !_usuarioIds.Contains(usuarioId))
                    {
                        Console.WriteLine($"  ⚠ Aportación {reader["IdAportacion"]} saltada: Socio {socioId} o Usuario {usuarioId} no existen tras recuperación.");
                        continue;
                    }

                    using (var cmd = new NpgsqlCommand(sql, pg))
                    {
                        cmd.Parameters.AddWithValue("@Id", (long)reader["IdAportacion"]);
                        cmd.Parameters.AddWithValue("@SocioId", socioId);
                        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
                        cmd.Parameters.AddWithValue("@Cantidad", ToDecimal(reader["CantidadAportada"]));
                        cmd.Parameters.AddWithValue("@Fecha", (DateTime)reader["Fecha"]);
                        cmd.Parameters.AddWithValue("@Codigo", reader["Codigo"]?.ToString() ?? "");
                        cmd.ExecuteNonQuery();
                        count++;
                    }
                }
            }
            LogTable("Aportaciones", count);
        }

        // ════════════════════════════════════════════
        // RETIRADAS
        // ════════════════════════════════════════════
        static void MigrateRetiradas(SqlCeConnection ce, NpgsqlConnection pg)
        {
            var sql = @"INSERT INTO ""Retiradas"" 
                (""Id"", ""SocioId"", ""ArticuloId"", ""UsuarioId"", ""PrecioArticulo"", ""Cantidad"", ""Total"", ""FirmaUrl"", ""Fecha"", ""IsActive"", ""CreatedAt"")
                VALUES (@Id, @SocioId, @ArtId, @UsrId, @Precio, @Cant, @Total, @Firma, @Fecha, TRUE, @Fecha)
                ON CONFLICT (""Id"") DO NOTHING";

            int count = 0;
            using (var reader = ReadCe(ce, "SELECT * FROM Retirada"))
            {
                while (reader.Read())
                {
                    long socioId = (long)reader["IdSocio"];
                    long artId = (long)reader["IdArticulo"];
                    long usrId = (long)reader["IdUsuario"];

                    if (!_socioIds.Contains(socioId) || !_articuloIds.Contains(artId) || !_usuarioIds.Contains(usrId))
                    {
                        Console.WriteLine($"  ⚠ Retirada {reader["IdRetirada"]} saltada por FK faltante.");
                        continue;
                    }

                    using (var cmd = new NpgsqlCommand(sql, pg))
                    {
                        cmd.Parameters.AddWithValue("@Id", (long)reader["IdRetirada"]);
                        cmd.Parameters.AddWithValue("@SocioId", socioId);
                        cmd.Parameters.AddWithValue("@ArtId", artId);
                        cmd.Parameters.AddWithValue("@UsrId", usrId);
                        cmd.Parameters.AddWithValue("@Precio", ToDecimal(reader["PrecioArticulo"]));
                        cmd.Parameters.AddWithValue("@Cant", ToDecimal(reader["Cantidad"]));
                        cmd.Parameters.AddWithValue("@Total", ToDecimal(reader["Total"]));
                        cmd.Parameters.AddWithValue("@Firma", PrefixPath(CleanText(reader["Firma"]), "/resources/firmas/"));
                        cmd.Parameters.AddWithValue("@Fecha", (DateTime)reader["Fecha"]);
                        cmd.ExecuteNonQuery();
                        count++;
                    }
                }
            }
            LogTable("Retiradas", count);
        }

        // ════════════════════════════════════════════
        // CUOTAS
        // ════════════════════════════════════════════
        static void MigrateCuotas(SqlCeConnection ce, NpgsqlConnection pg)
        {
            var sql = @"INSERT INTO ""Cuotas"" 
                (""Id"", ""SocioId"", ""Fecha"", ""CantidadCuota"", ""Periodo"", ""UsuarioId"", ""FechaAnterior"", ""IsActive"", ""CreatedAt"")
                VALUES (@Id, @SocioId, @Fecha, @Cant, @Per, @UsrId, @FechaAnt, TRUE, @Fecha)
                ON CONFLICT (""Id"") DO NOTHING";

            int count = 0;
            using (var reader = ReadCe(ce, "SELECT * FROM Cuota"))
            {
                while (reader.Read())
                {
                    long socioId = (long)reader["IdSocio"];
                    long usrId = (long)reader["IdUsuario"];

                    if (!_socioIds.Contains(socioId) || !_usuarioIds.Contains(usrId))
                    {
                        Console.WriteLine($"  ⚠ Cuota {reader["IdCuota"]} saltada por FK faltante.");
                        continue;
                    }

                    using (var cmd = new NpgsqlCommand(sql, pg))
                    {
                        cmd.Parameters.AddWithValue("@Id", (long)reader["IdCuota"]);
                        cmd.Parameters.AddWithValue("@SocioId", socioId);
                        cmd.Parameters.AddWithValue("@Fecha", (DateTime)reader["Fecha"]);
                        cmd.Parameters.AddWithValue("@Cant", (int)reader["CantidadCuota"]);
                        cmd.Parameters.AddWithValue("@Per", (int)reader["Periodo"]);
                        cmd.Parameters.AddWithValue("@UsrId", usrId);
                        cmd.Parameters.AddWithValue("@FechaAnt", (DateTime)reader["FechaAnterior"]);
                        cmd.ExecuteNonQuery();
                        count++;
                    }
                }
            }
            LogTable("Cuotas", count);
        }

        // ════════════════════════════════════════════
        // ACCESOS
        // ════════════════════════════════════════════
        static void MigrateAccesos(SqlCeConnection ce, NpgsqlConnection pg)
        {
            var sql = @"INSERT INTO ""Accesos"" 
                (""Id"", ""SocioId"", ""TipoAcceso"", ""FechaHora"", ""Turno"", ""Accion"", ""IsActive"", ""CreatedAt"")
                VALUES (@Id, @SocioId, @Tipo, @Fecha, @Turno, @Accion, TRUE, @Fecha)
                ON CONFLICT (""Id"") DO NOTHING";

            int count = 0;
            using (var reader = ReadCe(ce, "SELECT * FROM Acceso"))
            {
                while (reader.Read())
                {
                    long socioId = (long)reader["IdSocio"];
                    if (!_socioIds.Contains(socioId))
                    {
                        Console.WriteLine($"  ⚠ Acceso {reader["IdAcceso"]} saltado por FK faltante.");
                        continue;
                    }

                    using (var cmd = new NpgsqlCommand(sql, pg))
                    {
                        cmd.Parameters.AddWithValue("@Id", (long)reader["IdAcceso"]);
                        cmd.Parameters.AddWithValue("@SocioId", socioId);
                        cmd.Parameters.AddWithValue("@Tipo", reader["TipoAcceso"]?.ToString() ?? "");
                        cmd.Parameters.AddWithValue("@Fecha", (DateTime)reader["FechaYHora"]);
                        cmd.Parameters.AddWithValue("@Turno", OrNull(reader["Turno"]));
                        cmd.Parameters.AddWithValue("@Accion", reader["Accion"]?.ToString() ?? "");
                        cmd.ExecuteNonQuery();
                        count++;
                    }
                }
            }
            LogTable("Accesos", count);
        }

        // ════════════════════════════════════════════
        // BORRADOS → RegistrosBorrados
        // ════════════════════════════════════════════
        static void MigrateBorrados(SqlCeConnection ce, NpgsqlConnection pg)
        {
            var sql = @"INSERT INTO ""RegistrosBorrados"" 
                (""Id"", ""Fecha"", ""Tipo"", ""Descripcion"", ""SocioId"", ""FamiliaId"", ""UsuarioId"", ""ArticuloId"", ""CuotaId"",
                 ""Cantidad"", ""Total"", ""RealizadoPorUsuarioId"", ""IsActive"", ""CreatedAt"")
                VALUES (@Id, @Fecha, @Tipo, @Desc, @SocioId, @FamId, @UsrId, @ArtId, @CuoId,
                 @Cant, @Total, @Realizado, TRUE, @Fecha)
                ON CONFLICT (""Id"") DO NOTHING";

            int count = 0;
            try
            {
                using (var reader = ReadCe(ce, "SELECT * FROM Borrado"))
                {
                    while (reader.Read())
                    {
                        using (var cmd = new NpgsqlCommand(sql, pg))
                        {
                            cmd.Parameters.AddWithValue("@Id", (long)reader["IdBorrado"]);
                            cmd.Parameters.AddWithValue("@Fecha", (DateTime)reader["Fecha"]);
                            cmd.Parameters.AddWithValue("@Tipo", reader["Tipo"]?.ToString() ?? "");
                            cmd.Parameters.AddWithValue("@Desc", reader["Descripcion"]?.ToString() ?? "");
                            cmd.Parameters.AddWithValue("@SocioId", OrNullLong(reader["IdSocio"]));
                            cmd.Parameters.AddWithValue("@FamId", OrNullLong(reader["IdFamilia"]));
                            cmd.Parameters.AddWithValue("@UsrId", OrNullLong(reader["IdUsuario"]));
                            cmd.Parameters.AddWithValue("@ArtId", OrNullLong(reader["IdArticulo"]));
                            cmd.Parameters.AddWithValue("@CuoId", OrNullLong(reader["IdCuota"]));
                            cmd.Parameters.AddWithValue("@Cant", OrNullDecimal(reader["Cantidad"]));
                            cmd.Parameters.AddWithValue("@Total", OrNullDecimal(reader["Total"]));
                            cmd.Parameters.AddWithValue("@Realizado", (long)(int)reader["RealizadoPor"]);
                            cmd.ExecuteNonQuery();
                            count++;
                        }
                    }
                }
            }
            catch (SqlCeException)
            {
                Console.WriteLine("  ⚠ Tabla 'Borrado' no encontrada en .sdf, saltando...");
            }
            LogTable("RegistrosBorrados", count);
        }

        // ════════════════════════════════════════════
        // BORRADO PREVIO (orden por dependencias FK)
        // ════════════════════════════════════════════
        static void PurgeTargetTables(NpgsqlConnection pg)
        {
            Console.WriteLine("  Limpiando tablas de destino...");

            // Hijas -> Padres. Logins NO se toca por petición.
            string[] deleteOrder =
            {
                "RefreshTokens",
                "Accesos",
                "Cuotas",
                "Retiradas",
                "Aportaciones",
                "RegistrosBorrados",
                "UserLogins",
                "SocioDetalles",
                "Articulos",
                "Familias",
                "Socios",
                "Usuarios"
            };

            foreach (var table in deleteOrder)
            {
                ExecutePg(pg, $@"DELETE FROM ""{table}"";");
            }

            Console.WriteLine("  ✓ Tablas limpiadas");
            Console.WriteLine();
        }

        // ════════════════════════════════════════════
        // CREAR ADMIN
        // ════════════════════════════════════════════
        static void CreateAdminUser(NpgsqlConnection pg)
        {
            Console.WriteLine("  Asegurando usuario 'admin'...");

            // 1. Buscar o Crear Usuario Admin
            long userId = 0;
            using (var cmd = new NpgsqlCommand(@"SELECT ""Id"" FROM ""Usuarios"" WHERE ""Documento"" = '00000000A'", pg))
            {
                var res = cmd.ExecuteScalar();
                if (res != null && res != DBNull.Value)
                {
                    userId = Convert.ToInt64(res);
                }
                else
                {
                    // Calcular siguiente ID
                    using (var maxCmd = new NpgsqlCommand(@"SELECT COALESCE(MAX(""Id""), 0) FROM ""Usuarios""", pg))
                    {
                        userId = Convert.ToInt64(maxCmd.ExecuteScalar()) + 1;
                    }

                    var insertUser = @"INSERT INTO ""Usuarios"" 
                        (""Id"", ""Nombre"", ""Apellidos"", ""TipoDocumento"", ""Documento"", ""FechaAlta"", ""IsActive"", ""CreatedAt"")
                        VALUES 
                        (@Id, 'Admin', 'Sistema', 'DNI', '00000000A', NOW(), TRUE, NOW())";
                    
                    ExecutePg(pg, insertUser, ("@Id", userId));
                }
            }

            // 2. Crear Login Admin
            using (var cmd = new NpgsqlCommand(@"SELECT ""Id"" FROM ""Logins"" WHERE ""NombreUsuario"" = 'admin'", pg))
            {
                var res = cmd.ExecuteScalar();
                if (res == null || res == DBNull.Value)
                {
                     // Hash de "admin"
                     var hash = "jGl25bVBBBW96Qi9Te4V37Fnqchz/Eu4qB9vKrRIqRg=";
                     
                     long loginId = 0;
                     using (var maxCmd = new NpgsqlCommand(@"SELECT COALESCE(MAX(""Id""), 0) FROM ""Logins""", pg))
                     {
                        loginId = Convert.ToInt64(maxCmd.ExecuteScalar()) + 1;
                     }

                     var insertLogin = @"INSERT INTO ""Logins"" 
                        (""Id"", ""UsuarioId"", ""NombreUsuario"", ""PasswordHash"", ""IsActive"", ""CreatedAt"")
                        VALUES 
                        (@Id, @UserId, 'admin', @Hash, TRUE, NOW())";

                     ExecutePg(pg, insertLogin, 
                        ("@Id", loginId),
                        ("@UserId", userId),
                        ("@Hash", hash));
                        
                     Console.WriteLine($"    └─ Creado usuario 'admin' (pass: admin)");
                }
                else
                {
                     Console.WriteLine($"    └─ Usuario 'admin' ya existe");
                }
            }
        }

        // ════════════════════════════════════════════
        // Ajustar secuencias SERIAL tras insertar con IDs explícitos
        // ════════════════════════════════════════════
        static void ResetSequences(NpgsqlConnection pg)
        {
            Console.WriteLine();
            Console.WriteLine("  Ajustando secuencias SERIAL...");

            string[] tables = { "Usuarios", "Logins", "Socios", "UserLogins", "Familias",
                "Articulos", "Aportaciones", "Retiradas", "Cuotas", "Accesos", "RefreshTokens", "RegistrosBorrados" };

            foreach (var t in tables)
            {
                try
                {
                    var sql = $@"SELECT setval(pg_get_serial_sequence('""{t}""', 'Id'), COALESCE(MAX(""Id""), 1)) FROM ""{t}""";
                    ExecutePg(pg, sql);
                }
                catch { /* tabla vacía o sin secuencia, ignorar */ }
            }
            Console.WriteLine("  ✓ Secuencias ajustadas");
        }

        // ════════════════════════════════════════════
        // HELPERS
        // ════════════════════════════════════════════
        static SqlCeDataReader ReadCe(SqlCeConnection conn, string sql)
        {
            var cmd = new SqlCeCommand(sql, conn);
            return cmd.ExecuteReader();
        }

        static void ExecutePg(NpgsqlConnection conn, string sql, params (string name, object value)[] pars)
        {
            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                foreach (var p in pars)
                    cmd.Parameters.AddWithValue(p.name, p.value);
                cmd.ExecuteNonQuery();
            }
        }

        static string HashPassword(string password)
        {
            using (var sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(bytes);
            }
        }

        static object OrNull(object val)
        {
            if (val == null || val == DBNull.Value) return DBNull.Value;
            string s = val.ToString();
            return string.IsNullOrWhiteSpace(s) ? (object)DBNull.Value : s;
        }

        static object OrNullDate(object val)
        {
            if (val == null || val == DBNull.Value) return DBNull.Value;
            return (DateTime)val;
        }

        static object OrNullDecimal(object val)
        {
            if (val == null || val == DBNull.Value) return DBNull.Value;
            return Convert.ToDecimal(val);
        }

        static object OrNullInt(object val)
        {
            if (val == null || val == DBNull.Value) return DBNull.Value;
            return Convert.ToInt32(val);
        }

        static object OrNullLong(object val)
        {
            if (val == null || val == DBNull.Value) return DBNull.Value;
            return Convert.ToInt64(val);
        }

        static bool HasColumn(IDataRecord reader, string columnName)
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (reader.GetName(i).Equals(columnName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        static string ReadText(IDataRecord reader, params string[] candidates)
        {
            foreach (var c in candidates)
            {
                if (!HasColumn(reader, c)) continue;
                return CleanText(reader[c]);
            }
            return null;
        }

        static short ReadShort(IDataRecord reader, params string[] candidates)
        {
            foreach (var c in candidates)
            {
                if (!HasColumn(reader, c)) continue;
                var val = reader[c];
                if (val == null || val == DBNull.Value) return 0;
                return Convert.ToInt16(val);
            }
            return 0;
        }

        // Elimina caracteres fuera de WIN1252 (ej. emojis) para evitar errores de codificación
        static string CleanText(object val)
        {
            if (val == null || val == DBNull.Value) return null;
            var s = val.ToString();
            if (string.IsNullOrWhiteSpace(s)) return null;
            var sb = new StringBuilder(s.Length);
            foreach (var ch in s)
            {
                // 0x00-0xFF rango básico; descarta sustitutos/emoji
                if (ch <= 0xFF) sb.Append(ch);
            }
            var result = sb.ToString().Trim();
            return result.Length == 0 ? null : result;
        }

        // Prefija un nombre de archivo suelto con la ruta del servidor (ej. "foto.jpg" → "/resources/fotos/foto.jpg")
        static object PrefixPath(string val, string prefix)
        {
            if (string.IsNullOrWhiteSpace(val)) return DBNull.Value;
            // Si ya tiene ruta absoluta, dejarlo
            if (val.StartsWith("/")) return val;
            return prefix + val;
        }

        static decimal ToDecimal(object val) => Convert.ToDecimal(val ?? 0);

        static void LogTable(string table, int count)
        {
            _totalMigrated += count;
            Console.ForegroundColor = count > 0 ? ConsoleColor.Cyan : ConsoleColor.DarkGray;
            Console.WriteLine($"  ✓ {table,-25} → {count,6} registros");
            Console.ResetColor();
        }
    }
}
