/* =====================================================================
   CarniApp - Datos semilla para desarrollo
   Roles base, permisos iniciales, metodos de pago y negocio placeholder.
   Idempotente: puede ejecutarse varias veces sin duplicar datos.
   ===================================================================== */

USE CarniAppDb;
GO

-- Negocio (RF-CONFIG-003) - editar con los datos reales del cliente
IF NOT EXISTS (SELECT * FROM Negocio)
BEGIN
    INSERT INTO Negocio (NombreComercial, Telefono, Moneda, ZonaHoraria)
    VALUES ('Mi Carniceria', '55-0000-0000', 'MXN', 'America/Mexico_City');
END
GO

-- Sucursal principal (SUP-01)
IF NOT EXISTS (SELECT * FROM Sucursal)
BEGIN
    INSERT INTO Sucursal (Nombre, EsPrincipal)
    VALUES ('Sucursal principal', 1);
END
GO

-- Roles base (SUP-03: catalogo configurable, estos 3 son el punto de partida)
IF NOT EXISTS (SELECT * FROM Rol WHERE Nombre = 'Super Administrador')
BEGIN
    INSERT INTO Rol (Nombre, Descripcion) VALUES
        ('Super Administrador', 'Control total del sistema'),
        ('Cajera', 'Ventas, clientes, pedidos, cobro de creditos, caja'),
        ('Repartidor', 'Pedidos asignados, navegacion, confirmacion de entregas');
END
GO

-- Permisos base (se amplia conforme avanzan los sprints)
IF NOT EXISTS (SELECT * FROM Permiso WHERE Clave = 'pos.vender')
BEGIN
    INSERT INTO Permiso (Clave, Descripcion) VALUES
        ('pos.vender', 'Registrar ventas en el punto de venta'),
        ('inventario.consultar', 'Consultar inventario de cortes'),
        ('inventario.ajustar', 'Realizar ajustes manuales de inventario'),
        ('caja.cortar', 'Generar cortes de caja'),
        ('caja.reabrir', 'Reabrir un corte de caja ya cerrado'),
        ('config.critica.editar', 'Editar configuracion critica del sistema'),
        ('clientes.administrar', 'Dar de alta y editar clientes'),
        ('creditos.administrar', 'Registrar creditos y abonos');
END
GO

-- Asignar todos los permisos a Super Administrador
INSERT INTO RolPermiso (RolId, PermisoId)
SELECT r.Id, p.Id
FROM Rol r
CROSS JOIN Permiso p
WHERE r.Nombre = 'Super Administrador'
  AND NOT EXISTS (
      SELECT 1 FROM RolPermiso rp WHERE rp.RolId = r.Id AND rp.PermisoId = p.Id
  );
GO

-- Permisos tipicos de Cajera
INSERT INTO RolPermiso (RolId, PermisoId)
SELECT r.Id, p.Id
FROM Rol r
CROSS JOIN Permiso p
WHERE r.Nombre = 'Cajera'
  AND p.Clave IN ('pos.vender', 'inventario.consultar', 'caja.cortar', 'clientes.administrar', 'creditos.administrar')
  AND NOT EXISTS (
      SELECT 1 FROM RolPermiso rp WHERE rp.RolId = r.Id AND rp.PermisoId = p.Id
  );
GO

-- Metodos de pago (RF-POS-008)
IF NOT EXISTS (SELECT * FROM MetodoPago)
BEGIN
    INSERT INTO MetodoPago (Nombre) VALUES
        ('Efectivo'), ('Tarjeta'), ('Transferencia'), ('Credito');
END
GO

-- Tipos de animal (RF-INV-001)
IF NOT EXISTS (SELECT * FROM TipoAnimal)
BEGIN
    INSERT INTO TipoAnimal (Nombre) VALUES ('Res'), ('Cerdo'), ('Pollo');
END
GO

-- IMPORTANTE: el usuario Super Administrador inicial se crea desde el API
-- (endpoint de registro inicial, Sprint 1), nunca con un hash de prueba
-- escrito a mano en este script, para no dejar una contrasena conocida
-- en el repositorio de codigo.
