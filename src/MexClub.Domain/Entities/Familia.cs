namespace MexClub.Domain.Entities;

public class Familia : BaseEntity
{
    public string Nombre { get; set; } = string.Empty;
    public int? Descuento { get; set; }

    // Navigation
    public ICollection<Articulo> Articulos { get; set; } = new List<Articulo>();
}
