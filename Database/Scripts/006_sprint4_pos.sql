/* =====================================================================
   Sprint 4 - POS basico.
   Agrega el permiso 'pos.anular' (RN-003: anular una venta pide permiso
   y motivo) y lo asigna solo a Super Administrador. Idempotente.
   Las tablas Venta / VentaDetalle / VentaPago / MetodoPago ya existen
   desde 002_schema_mvp.sql.
   ===================================================================== */

USE CarniAppDb;
GO

IF NOT EXISTS (SELECT * FROM Permiso WHERE Clave = 'pos.anular')
BEGIN
    INSERT INTO Permiso (Clave, Descripcion)
    VALUES ('pos.anular', 'Anular ventas ya registradas (requiere motivo)');
END
GO

INSERT INTO RolPermiso (RolId, PermisoId)
SELECT r.Id, p.Id
FROM Rol r
CROSS JOIN Permiso p
WHERE r.Nombre = 'Super Administrador'
  AND p.Clave = 'pos.anular'
  AND NOT EXISTS (
      SELECT 1 FROM RolPermiso rp WHERE rp.RolId = r.Id AND rp.PermisoId = p.Id
  );
GO
