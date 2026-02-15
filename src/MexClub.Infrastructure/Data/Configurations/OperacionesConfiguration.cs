using MexClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MexClub.Infrastructure.Data.Configurations;

public class AccesoConfiguration : IEntityTypeConfiguration<Acceso>
{
    public void Configure(EntityTypeBuilder<Acceso> builder)
    {
        builder.ToTable("Accesos");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("Id").ValueGeneratedOnAdd();

        builder.HasIndex(a => a.SocioId);
        builder.HasIndex(a => a.FechaHora);

        builder.Property(a => a.TipoAcceso).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.Accion).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.Turno).HasMaxLength(50);

        builder.HasOne(a => a.Socio)
            .WithMany(s => s.Accesos)
            .HasForeignKey(a => a.SocioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AportacionConfiguration : IEntityTypeConfiguration<Aportacion>
{
    public void Configure(EntityTypeBuilder<Aportacion> builder)
    {
        builder.ToTable("Aportaciones");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("Id").ValueGeneratedOnAdd();

        builder.HasIndex(a => a.SocioId);
        builder.HasIndex(a => a.Fecha);

        builder.Property(a => a.CantidadAportada).HasPrecision(18, 2);
        builder.Property(a => a.Codigo).HasMaxLength(10);

        builder.HasOne(a => a.Socio)
            .WithMany(s => s.Aportaciones)
            .HasForeignKey(a => a.SocioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Usuario)
            .WithMany()
            .HasForeignKey(a => a.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class RetiradaConfiguration : IEntityTypeConfiguration<Retirada>
{
    public void Configure(EntityTypeBuilder<Retirada> builder)
    {
        builder.ToTable("Retiradas");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("Id").ValueGeneratedOnAdd();

        builder.HasIndex(r => r.SocioId);
        builder.HasIndex(r => r.Fecha);

        builder.Property(r => r.PrecioArticulo).HasPrecision(18, 2);
        builder.Property(r => r.Cantidad).HasPrecision(18, 4);
        builder.Property(r => r.Total).HasPrecision(18, 2);
        builder.Property(r => r.FirmaUrl).HasMaxLength(500);

        builder.HasOne(r => r.Socio)
            .WithMany(s => s.Retiradas)
            .HasForeignKey(r => r.SocioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Articulo)
            .WithMany(a => a.Retiradas)
            .HasForeignKey(r => r.ArticuloId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Usuario)
            .WithMany()
            .HasForeignKey(r => r.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class CuotaConfiguration : IEntityTypeConfiguration<Cuota>
{
    public void Configure(EntityTypeBuilder<Cuota> builder)
    {
        builder.ToTable("Cuotas");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("Id").ValueGeneratedOnAdd();

        builder.HasIndex(c => c.SocioId);
        builder.HasIndex(c => c.Fecha);

        builder.Property(c => c.Periodo).HasConversion<int>();

        builder.HasOne(c => c.Socio)
            .WithMany(s => s.Cuotas)
            .HasForeignKey(c => c.SocioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Usuario)
            .WithMany()
            .HasForeignKey(c => c.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("Id").ValueGeneratedOnAdd();

        builder.HasIndex(r => r.Token).IsUnique();
        builder.Property(r => r.Token).HasMaxLength(500).IsRequired();
    }
}

public class RegistroBorradoConfiguration : IEntityTypeConfiguration<RegistroBorrado>
{
    public void Configure(EntityTypeBuilder<RegistroBorrado> builder)
    {
        builder.ToTable("RegistrosBorrados");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("Id").ValueGeneratedOnAdd();

        builder.Property(r => r.Tipo).HasMaxLength(50);
        builder.Property(r => r.Descripcion).HasMaxLength(500);
        builder.Property(r => r.Cantidad).HasPrecision(18, 4);
        builder.Property(r => r.Total).HasPrecision(18, 2);
    }
}
