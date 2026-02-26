namespace MexClub.Application.DTOs.Operaciones;

public record AportacionDto(
    long Id,
    long SocioId,
    string SocioNombre,
    int SocioNumSocio,
    string SocioDocumento,
    string? SocioFotoUrl,
    long UsuarioId,
    decimal CantidadAportada,
    DateTime Fecha,
    string Codigo
);

public record CreateAportacionRequest(long SocioId, long UsuarioId, decimal CantidadAportada);

public record RetiradaDto(
    long Id,
    long SocioId,
    string SocioNombre,
    int SocioNumSocio,
    string SocioDocumento,
    string? SocioFotoUrl,
    long ArticuloId,
    string ArticuloNombre,
    long UsuarioId,
    decimal PrecioArticulo,
    decimal Cantidad,
    decimal Total,
    string? FirmaUrl,
    DateTime Fecha
);

public record CreateRetiradaRequest(
    long SocioId,
    long ArticuloId,
    long UsuarioId,
    decimal Cantidad,
    string? FirmaBase64
);

public record CreateRetiradaListRequest(
    long SocioId,
    long UsuarioId,
    string? FirmaBase64,
    bool PermitirExcesoLimiteMensual,
    List<RetiradaItemRequest> Items
);

public record RetiradaItemRequest(long ArticuloId, decimal Cantidad);

public record CuotaDto(
    long Id,
    long SocioId,
    string SocioNombre,
    DateTime Fecha,
    int CantidadCuota,
    string Periodo,
    long UsuarioId,
    DateTime FechaAnterior
);

public record CreateCuotaRequest(long SocioId, long UsuarioId, int CantidadCuota, int Periodo);

public record AccesoDto(
    long Id,
    long SocioId,
    string SocioNombre,
    string TipoAcceso,
    DateTime FechaHora,
    string? Turno,
    string Accion
);

public record FicharRequest(long SocioId);

public record FicharResponse(string TipoAcceso, DateTime FechaHora);

public record DashboardDto(
    int TotalSociosActivos,
    int SociosDentro,
    decimal TotalAportaciones,
    decimal TotalRetiradas,
    List<AccesoDto> UltimosAccesos,
    List<AportacionDto> UltimasAportaciones,
    List<RetiradaDto> UltimasRetiradas
);
