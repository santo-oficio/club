namespace MexClub.Application.DTOs.Productos;

public record FamiliaDto(long Id, string Nombre, bool IsActive, int? Descuento);

public record CreateFamiliaRequest(string Nombre, int? Descuento);
public record UpdateFamiliaRequest(long Id, string Nombre, int? Descuento, bool? IsActive);

public record ArticuloDto(
    long Id,
    long FamiliaId,
    string FamiliaNombre,
    string Nombre,
    string? Descripcion,
    decimal Precio,
    decimal Cantidad1,
    decimal? Cantidad2,
    decimal? Cantidad3,
    decimal? Cantidad4,
    bool EsDecimal,
    bool IsActive
);

public record CreateArticuloRequest(
    long FamiliaId,
    string Nombre,
    string? Descripcion,
    decimal Precio,
    decimal Cantidad1,
    decimal? Cantidad2,
    decimal? Cantidad3,
    decimal? Cantidad4,
    bool EsDecimal
);

public record UpdateArticuloRequest(
    long Id,
    long FamiliaId,
    string Nombre,
    string? Descripcion,
    decimal Precio,
    decimal Cantidad1,
    decimal? Cantidad2,
    decimal? Cantidad3,
    decimal? Cantidad4,
    bool EsDecimal,
    bool? IsActive
);
