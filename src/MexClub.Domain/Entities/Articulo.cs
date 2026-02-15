namespace MexClub.Domain.Entities;

public class Articulo : BaseEntity
{
    public long FamiliaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal Precio { get; set; }
    public decimal Cantidad1 { get; set; }
    public decimal? Cantidad2 { get; set; }
    public decimal? Cantidad3 { get; set; }
    public decimal? Cantidad4 { get; set; }
    public bool EsDecimal { get; set; }

    // Navigation
    public Familia Familia { get; set; } = null!;
    public ICollection<Retirada> Retiradas { get; set; } = new List<Retirada>();
}
