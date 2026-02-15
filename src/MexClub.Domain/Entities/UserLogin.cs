namespace MexClub.Domain.Entities;

public class UserLogin : BaseEntity
{
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Rol { get; set; } = "socio";
    public DateTime FechaAlta { get; set; }
    public long SocioId { get; set; }

    // Navigation
    public Socio Socio { get; set; } = null!;
}
