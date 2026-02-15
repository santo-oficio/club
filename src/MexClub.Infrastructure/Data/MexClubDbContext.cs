using MexClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MexClub.Infrastructure.Data;

public class MexClubDbContext : DbContext
{
    public MexClubDbContext(DbContextOptions<MexClubDbContext> options) : base(options) { }

    public DbSet<Socio> Socios => Set<Socio>();
    public DbSet<SocioDetalle> SocioDetalles => Set<SocioDetalle>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Familia> Familias => Set<Familia>();
    public DbSet<Articulo> Articulos => Set<Articulo>();
    public DbSet<Aportacion> Aportaciones => Set<Aportacion>();
    public DbSet<Retirada> Retiradas => Set<Retirada>();
    public DbSet<Cuota> Cuotas => Set<Cuota>();
    public DbSet<Acceso> Accesos => Set<Acceso>();
    public DbSet<Login> Logins => Set<Login>();
    public DbSet<UserLogin> UserLogins => Set<UserLogin>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<RegistroBorrado> RegistrosBorrados => Set<RegistroBorrado>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MexClubDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            // Auto-trim all string properties
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                foreach (var prop in entry.Properties)
                {
                    if (prop.Metadata.ClrType == typeof(string) && prop.CurrentValue is string s)
                        prop.CurrentValue = s.Trim();
                }
            }

            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    if (entry.Entity.CreatedAt.Kind == DateTimeKind.Unspecified)
                        entry.Entity.CreatedAt = DateTime.SpecifyKind(entry.Entity.CreatedAt, DateTimeKind.Utc);
                    break;
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
