using CarniApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarniApp.Infrastructure.Persistence;

/// <summary>
/// DbContext principal. Sprint 0 mapeo las entidades base (Negocio,
/// Sucursal, Rol, Permiso, Usuario); Sprint 1 agrega RefreshToken
/// (RF-AUTH-002/007). El resto de modulos (Inventario, POS, Clientes,
/// etc.) se agregan conforme avanzan los siguientes sprints.
///
/// IMPORTANTE: los scripts SQL en Database/Scripts/ crean las tablas
/// en SINGULAR (Negocio, Sucursal, Rol, Permiso, RolPermiso, Usuario,
/// RefreshToken), mientras que la convencion de EF Core pluraliza el
/// nombre del DbSet por defecto. Por eso cada entidad tiene su
/// ToTable(...) explicito abajo - si agregas una entidad nueva, agrega
/// tambien su ToTable() usando el mismo nombre (singular) que uses en
/// el script SQL.
/// </summary>
public class CarniAppDbContext : DbContext
{
    public CarniAppDbContext(DbContextOptions<CarniAppDbContext> options) : base(options)
    {
    }

    public DbSet<Negocio> Negocios => Set<Negocio>();
    public DbSet<Sucursal> Sucursales => Set<Sucursal>();
    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<Permiso> Permisos => Set<Permiso>();
    public DbSet<RolPermiso> RolPermisos => Set<RolPermiso>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Negocio>().ToTable("Negocio");
        modelBuilder.Entity<Sucursal>().ToTable("Sucursal");
        modelBuilder.Entity<Rol>().ToTable("Rol");
        modelBuilder.Entity<Permiso>().ToTable("Permiso");
        modelBuilder.Entity<RolPermiso>().ToTable("RolPermiso");
        modelBuilder.Entity<Usuario>().ToTable("Usuario");
        modelBuilder.Entity<RefreshToken>().ToTable("RefreshToken");

        modelBuilder.Entity<RolPermiso>(entity =>
        {
            entity.HasKey(rp => new { rp.RolId, rp.PermisoId });

            entity.HasOne(rp => rp.Rol)
                .WithMany(r => r.RolPermisos)
                .HasForeignKey(rp => rp.RolId);

            entity.HasOne(rp => rp.Permiso)
                .WithMany(p => p.RolPermisos)
                .HasForeignKey(rp => rp.PermisoId);
        });

        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.HasIndex(u => u.NombreUsuario).IsUnique();

            entity.HasOne(u => u.Rol)
                .WithMany(r => r.Usuarios)
                .HasForeignKey(u => u.RolId);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasIndex(rt => rt.TokenHash).IsUnique();
            entity.HasIndex(rt => rt.UsuarioId);

            entity.HasOne(rt => rt.Usuario)
                .WithMany()
                .HasForeignKey(rt => rt.UsuarioId);
        });

        base.OnModelCreating(modelBuilder);
    }
}
