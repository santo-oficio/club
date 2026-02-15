using MexClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MexClub.Infrastructure.Data.Configurations;

public class FamiliaConfiguration : IEntityTypeConfiguration<Familia>
{
    public void Configure(EntityTypeBuilder<Familia> builder)
    {
        builder.ToTable("Familias");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasColumnName("Id").ValueGeneratedOnAdd();

        builder.HasIndex(f => f.Nombre);
        builder.Property(f => f.Nombre).HasMaxLength(100).IsRequired();
    }
}

public class ArticuloConfiguration : IEntityTypeConfiguration<Articulo>
{
    public void Configure(EntityTypeBuilder<Articulo> builder)
    {
        builder.ToTable("Articulos");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("Id").ValueGeneratedOnAdd();

        builder.HasIndex(a => a.Nombre);
        builder.HasIndex(a => a.IsActive);

        builder.Property(a => a.Nombre).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Descripcion).HasMaxLength(500);
        builder.Property(a => a.Precio).HasPrecision(18, 2);
        builder.Property(a => a.Cantidad1).HasPrecision(18, 4);
        builder.Property(a => a.Cantidad2).HasPrecision(18, 4);
        builder.Property(a => a.Cantidad3).HasPrecision(18, 4);
        builder.Property(a => a.Cantidad4).HasPrecision(18, 4);

        builder.HasOne(a => a.Familia)
            .WithMany(f => f.Articulos)
            .HasForeignKey(a => a.FamiliaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
