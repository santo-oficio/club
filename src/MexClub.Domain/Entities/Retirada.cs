namespace MexClub.Domain.Entities;

public class Retirada : BaseEntity
{
    public long SocioId { get; set; }
    public long ArticuloId { get; set; }
    public long UsuarioId { get; set; }
    public decimal PrecioArticulo { get; set; }
    public decimal Cantidad { get; set; }
    public decimal Total { get; set; }
    public string? FirmaUrl { get; set; }
    public DateTime Fecha { get; set; }

    // Navigation
    public Socio Socio { get; set; } = null!;
    public Articulo Articulo { get; set; } = null!;
    public Usuario Usuario { get; set; } = null!;
}
