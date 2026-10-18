/* =====================================================================
   Permiso 'pos.recargo.ajustar': cambiar el % de cargo por credito al cobrar
   (el valor por defecto sale de Pos:RecargoCreditoPorcentaje). Super
   Administrador lo recibe; se puede asignar a otros roles desde la tabla
   RolPermiso. El usuario debe volver a iniciar sesion para recibirlo.
   Idempotente.
   ===================================================================== */

USE CarniAppDb;
GO

IF NOT EXISTS (SELECT * FROM Permiso WHERE Clave = 'pos.recargo.ajustar')
    INSERT INTO Permiso (Clave, Descripcion)
    VALUES ('pos.recargo.ajustar', 'Cambiar el porcentaje de cargo por credito al cobrar una venta');
GO

INSERT INTO RolPermiso (RolId, PermisoId)
SELECT r.Id, p.Id
FROM Rol r
CROSS JOIN Permiso p
WHERE r.Nombre = 'Super Administrador'
  AND p.Clave = 'pos.recargo.ajustar'
  AND NOT EXISTS (
      SELECT 1 FROM RolPermiso rp WHERE rp.RolId = r.Id AND rp.PermisoId = p.Id
  );
GO
