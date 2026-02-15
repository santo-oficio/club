namespace MexClub.Domain.Entities;

public class Aportacion : BaseEntity
{
    public long SocioId { get; set; }
    public long UsuarioId { get; set; }
    public decimal CantidadAportada { get; set; }
    public DateTime Fecha { get; set; }
    public string Codigo { get; set; } = string.Empty;

    // Navigation
    public Socio Socio { get; set; } = null!;
    public Usuario Usuario { get; set; } = null!;
}
