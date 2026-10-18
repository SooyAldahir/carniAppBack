using CarniApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CarniApp.Infrastructure.Persistence;

/// <summary>
/// DbContext principal. Sprint 0: Negocio, Sucursal, Rol, Permiso,
/// Usuario. Sprint 1: RefreshToken. Sprint 2: TipoAnimal, Corte.
/// Sprint 3: Inventario, MovimientoInventario. Sprint 4: MetodoPago,
/// Venta, VentaDetalle, VentaPago. El resto de modulos se
/// agregan conforme avanzan los siguientes sprints.
///
/// IMPORTANTE: los scripts SQL en Database/Scripts/ crean las tablas
/// en SINGULAR, mientras que la convencion de EF Core pluraliza el
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
    public DbSet<TipoAnimal> TiposAnimal => Set<TipoAnimal>();
    public DbSet<Corte> Cortes => Set<Corte>();
    public DbSet<Inventario> Inventarios => Set<Inventario>();
    public DbSet<MovimientoInventario> MovimientosInventario => Set<MovimientoInventario>();
    public DbSet<MetodoPago> MetodosPago => Set<MetodoPago>();
    public DbSet<Venta> Ventas => Set<Venta>();
    public DbSet<VentaDetalle> VentasDetalle => Set<VentaDetalle>();
    public DbSet<VentaPago> VentasPago => Set<VentaPago>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<AbonoCredito> AbonosCredito => Set<AbonoCredito>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Negocio>().ToTable("Negocio");
        modelBuilder.Entity<Sucursal>().ToTable("Sucursal");
        modelBuilder.Entity<Rol>().ToTable("Rol");
        modelBuilder.Entity<Permiso>().ToTable("Permiso");
        modelBuilder.Entity<RolPermiso>().ToTable("RolPermiso");
        modelBuilder.Entity<Usuario>().ToTable("Usuario");
        modelBuilder.Entity<RefreshToken>().ToTable("RefreshToken");
        modelBuilder.Entity<TipoAnimal>().ToTable("TipoAnimal");
        modelBuilder.Entity<Corte>().ToTable("Corte");
        modelBuilder.Entity<Inventario>().ToTable("Inventario");
        modelBuilder.Entity<MovimientoInventario>().ToTable("MovimientoInventario");
        modelBuilder.Entity<MetodoPago>().ToTable("MetodoPago");
        modelBuilder.Entity<Venta>().ToTable("Venta");
        modelBuilder.Entity<VentaDetalle>().ToTable("VentaDetalle");
        modelBuilder.Entity<VentaPago>().ToTable("VentaPago");
        modelBuilder.Entity<Cliente>().ToTable("Cliente");
        modelBuilder.Entity<AbonoCredito>().ToTable("AbonoCredito");

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

        modelBuilder.Entity<Corte>(entity =>
        {
            entity.Property(c => c.UnidadVenta)
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.Property(c => c.PrecioBase).HasColumnType("decimal(10,2)");
            entity.Property(c => c.StockMinimo).HasColumnType("decimal(10,3)");
            entity.Property(c => c.CodigoBarras).HasMaxLength(50);

            entity.HasOne(c => c.TipoAnimal)
                .WithMany(t => t.Cortes)
                .HasForeignKey(c => c.TipoAnimalId);
        });

        modelBuilder.Entity<Inventario>(entity =>
        {
            entity.HasKey(i => i.Id);
            // Token de concurrencia: si dos operaciones leen el mismo stock y ambas lo
            // modifican, la segunda falla en vez de pisar a la primera (evita sobreventa,
            // RF-POS-011). Funciona sin cambiar el esquema SQL.
            entity.Property(i => i.StockActual).HasColumnType("decimal(10,3)").IsConcurrencyToken();
            entity.HasIndex(i => new { i.CorteId, i.SucursalId }).IsUnique();

            entity.HasOne(i => i.Corte).WithMany().HasForeignKey(i => i.CorteId);
            entity.HasOne(i => i.Sucursal).WithMany().HasForeignKey(i => i.SucursalId);
        });

        modelBuilder.Entity<MovimientoInventario>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Cantidad).HasColumnType("decimal(10,3)");
            entity.Property(m => m.TipoMovimiento).HasConversion<string>().HasMaxLength(30);

            entity.HasOne(m => m.Corte).WithMany().HasForeignKey(m => m.CorteId);
            entity.HasOne(m => m.Sucursal).WithMany().HasForeignKey(m => m.SucursalId);
            entity.HasOne(m => m.Usuario).WithMany().HasForeignKey(m => m.UsuarioId);
        });

        modelBuilder.Entity<Venta>(entity =>
        {
            entity.HasKey(v => v.Id);

            // Folio es IDENTITY en SQL Server. Con SQLite (solo pruebas) no existe ese
            // concepto, asi que se deja un valor por defecto para que el INSERT funcione.
            var folio = entity.Property(v => v.Folio).ValueGeneratedOnAdd();
            folio.Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
            if (Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
            {
                folio.HasDefaultValue(0);
            }

            entity.Property(v => v.Subtotal).HasColumnType("decimal(10,2)");
            entity.Property(v => v.Descuento).HasColumnType("decimal(10,2)");
            entity.Property(v => v.Total).HasColumnType("decimal(10,2)");
            entity.Property(v => v.Estado).HasMaxLength(20);
            entity.Property(v => v.MotivoAnulacion).HasMaxLength(300);
            entity.Property(v => v.Nota).HasMaxLength(200);
            entity.HasIndex(v => v.Fecha);

            entity.HasOne(v => v.Sucursal).WithMany().HasForeignKey(v => v.SucursalId);
            entity.HasOne(v => v.Usuario).WithMany().HasForeignKey(v => v.UsuarioId);
            entity.HasOne(v => v.Cliente).WithMany().HasForeignKey(v => v.ClienteId);
            entity.HasMany(v => v.Detalles).WithOne(d => d.Venta).HasForeignKey(d => d.VentaId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(v => v.Pagos).WithOne(p => p.Venta).HasForeignKey(p => p.VentaId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<VentaDetalle>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.Property(d => d.Cantidad).HasColumnType("decimal(10,3)");
            entity.Property(d => d.PrecioUnitario).HasColumnType("decimal(10,2)");
            entity.Property(d => d.Importe).HasColumnType("decimal(10,2)");
            entity.HasOne(d => d.Corte).WithMany().HasForeignKey(d => d.CorteId);
        });

        modelBuilder.Entity<VentaPago>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Importe).HasColumnType("decimal(10,2)");
            entity.HasOne(p => p.MetodoPago).WithMany().HasForeignKey(p => p.MetodoPagoId);
        });

        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.Property(c => c.Nombre).HasMaxLength(150);
            entity.Property(c => c.Telefono).HasMaxLength(30);
            entity.Property(c => c.Direccion).HasMaxLength(300);
            entity.Property(c => c.Referencias).HasMaxLength(500);
            entity.Property(c => c.FotoFachadaUrl).HasMaxLength(500);
            entity.Property(c => c.Latitud).HasColumnType("decimal(10,7)");
            entity.Property(c => c.Longitud).HasColumnType("decimal(10,7)");
            entity.Property(c => c.LimiteCredito).HasColumnType("decimal(10,2)");
            // Token de concurrencia: dos ventas a credito (o un abono) simultaneos no
            // pueden pisarse el saldo ni exceder el limite.
            entity.Property(c => c.SaldoCredito).HasColumnType("decimal(10,2)").IsConcurrencyToken();
            entity.Ignore(c => c.CreditoDisponible);
        });

        modelBuilder.Entity<AbonoCredito>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Importe).HasColumnType("decimal(10,2)");
            entity.HasOne(a => a.Cliente).WithMany().HasForeignKey(a => a.ClienteId);
            entity.HasOne(a => a.Usuario).WithMany().HasForeignKey(a => a.UsuarioId);
        });

        modelBuilder.Entity<MetodoPago>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Nombre).HasMaxLength(50);
        });

        base.OnModelCreating(modelBuilder);
    }
}
