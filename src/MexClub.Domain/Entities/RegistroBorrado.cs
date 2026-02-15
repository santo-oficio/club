namespace MexClub.Domain.Entities;

public class RegistroBorrado : BaseEntity
{
    public DateTime Fecha { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public long? SocioId { get; set; }
    public long? FamiliaId { get; set; }
    public long? UsuarioId { get; set; }
    public long? ArticuloId { get; set; }
    public long? CuotaId { get; set; }
    public decimal? Cantidad { get; set; }
    public decimal? Total { get; set; }
    public long RealizadoPorUsuarioId { get; set; }

    // Navigation
    public Usuario? RealizadoPor { get; set; }
}
