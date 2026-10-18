/* =====================================================================
   Sprint 5 - POS avanzado (RF-POS-004: descuentos con permisos
   diferenciados segun magnitud).
     pos.descuento       -> descuentos hasta el % configurado (Pos:DescuentoMaximoSinAutorizacionPorcentaje, 10% por defecto)
     pos.descuento.alto  -> descuentos de cualquier magnitud
   Super Administrador recibe ambos; Cajera solo el basico. Idempotente.
   ===================================================================== */

USE CarniAppDb;
GO

IF NOT EXISTS (SELECT * FROM Permiso WHERE Clave = 'pos.descuento')
    INSERT INTO Permiso (Clave, Descripcion)
    VALUES ('pos.descuento', 'Aplicar descuentos hasta el porcentaje maximo configurado');
GO

IF NOT EXISTS (SELECT * FROM Permiso WHERE Clave = 'pos.descuento.alto')
    INSERT INTO Permiso (Clave, Descripcion)
    VALUES ('pos.descuento.alto', 'Aplicar descuentos por encima del porcentaje maximo configurado');
GO

INSERT INTO RolPermiso (RolId, PermisoId)
SELECT r.Id, p.Id
FROM Rol r
CROSS JOIN Permiso p
WHERE (
        (r.Nombre = 'Super Administrador' AND p.Clave IN ('pos.descuento', 'pos.descuento.alto'))
     OR (r.Nombre = 'Cajera'              AND p.Clave = 'pos.descuento')
      )
  AND NOT EXISTS (
      SELECT 1 FROM RolPermiso rp WHERE rp.RolId = r.Id AND rp.PermisoId = p.Id
  );
GO
