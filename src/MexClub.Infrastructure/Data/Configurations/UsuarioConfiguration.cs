using MexClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MexClub.Infrastructure.Data.Configurations;

public class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("Usuarios");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("Id").ValueGeneratedOnAdd();

        builder.HasIndex(u => u.Documento).IsUnique();

        builder.Property(u => u.Nombre).HasMaxLength(100).IsRequired();
        builder.Property(u => u.Apellidos).HasMaxLength(200).IsRequired();
        builder.Property(u => u.TipoDocumento).HasMaxLength(20).IsRequired();
        builder.Property(u => u.Documento).HasMaxLength(20).IsRequired();
        builder.Property(u => u.Pais).HasMaxLength(50);
        builder.Property(u => u.Provincia).HasMaxLength(50);
        builder.Property(u => u.Localidad).HasMaxLength(100);
        builder.Property(u => u.Direccion).HasMaxLength(200);
        builder.Property(u => u.CodigoPostal).HasMaxLength(10);
        builder.Property(u => u.Telefono1).HasMaxLength(20);
        builder.Property(u => u.Telefono2).HasMaxLength(20);
        builder.Property(u => u.Email).HasMaxLength(150);

        builder.HasOne(u => u.Login)
            .WithOne(l => l.Usuario)
            .HasForeignKey<Login>(l => l.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class LoginConfiguration : IEntityTypeConfiguration<Login>
{
    public void Configure(EntityTypeBuilder<Login> builder)
    {
        builder.ToTable("Logins");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("Id").ValueGeneratedOnAdd();

        builder.HasIndex(l => l.NombreUsuario).IsUnique();

        builder.Property(l => l.NombreUsuario).HasMaxLength(100).IsRequired();
        builder.Property(l => l.PasswordHash).HasMaxLength(500).IsRequired();
    }
}

public class UserLoginConfiguration : IEntityTypeConfiguration<UserLogin>
{
    public void Configure(EntityTypeBuilder<UserLogin> builder)
    {
        builder.ToTable("UserLogins");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("Id").ValueGeneratedOnAdd();

        builder.HasIndex(u => u.Username).IsUnique();

        builder.Property(u => u.Username).HasMaxLength(100).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(500).IsRequired();
        builder.Property(u => u.Rol).HasMaxLength(50).IsRequired().HasDefaultValue("socio");

        builder.HasOne(u => u.Socio)
            .WithOne(s => s.UserLogin)
            .HasForeignKey<UserLogin>(u => u.SocioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
