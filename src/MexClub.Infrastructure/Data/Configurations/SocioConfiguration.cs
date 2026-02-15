using MexClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MexClub.Infrastructure.Data.Configurations;

public class SocioConfiguration : IEntityTypeConfiguration<Socio>
{
    public void Configure(EntityTypeBuilder<Socio> builder)
    {
        builder.ToTable("Socios");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("Id").ValueGeneratedOnAdd();

        builder.HasIndex(s => s.NumSocio).IsUnique();
        builder.HasIndex(s => s.Documento).IsUnique();
        builder.HasIndex(s => s.Codigo);
        builder.HasIndex(s => s.IsActive);
        builder.HasIndex(s => s.FechaAlta);

        builder.Property(s => s.NumSocio).IsRequired();
        builder.Property(s => s.Codigo).HasMaxLength(50).IsRequired();
        builder.Property(s => s.Nombre).HasMaxLength(100).IsRequired();
        builder.Property(s => s.PrimerApellido).HasMaxLength(100).IsRequired();
        builder.Property(s => s.SegundoApellido).HasMaxLength(100);
        builder.Property(s => s.TipoDocumento).HasMaxLength(20).IsRequired();
        builder.Property(s => s.Documento).HasMaxLength(20).IsRequired();
        builder.Property(s => s.Pais).HasMaxLength(50);
        builder.Property(s => s.Provincia).HasMaxLength(50);
        builder.Property(s => s.Localidad).HasMaxLength(100);
        builder.Property(s => s.Direccion).HasMaxLength(200);
        builder.Property(s => s.CodigoPostal).HasMaxLength(10);
        builder.Property(s => s.Telefono).HasMaxLength(20);
        builder.Property(s => s.Email).HasMaxLength(150);
        builder.Property(s => s.FotoUrl).HasMaxLength(500);
        builder.Property(s => s.FotoAnversoDniUrl).HasMaxLength(500);
        builder.Property(s => s.FotoReversoDniUrl).HasMaxLength(500);
        builder.Property(s => s.Comentario).HasMaxLength(1000);

        builder.HasOne(s => s.ReferidoPor)
            .WithMany(s => s.Referidos)
            .HasForeignKey(s => s.ReferidoPorSocioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Detalle)
            .WithOne(d => d.Socio)
            .HasForeignKey<SocioDetalle>(d => d.SocioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class SocioDetalleConfiguration : IEntityTypeConfiguration<SocioDetalle>
{
    public void Configure(EntityTypeBuilder<SocioDetalle> builder)
    {
        builder.ToTable("SocioDetalles");
        builder.HasKey(d => d.SocioId);
        builder.Property(d => d.Aprovechable).HasPrecision(18, 2);
        builder.Property(d => d.ConsumicionDelMes).HasPrecision(18, 2);
        builder.Property(d => d.AportacionDelDia).HasPrecision(18, 2);
    }
}
