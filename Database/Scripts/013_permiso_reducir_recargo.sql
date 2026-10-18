/* =====================================================================
   Permiso 'pos.recargo.reducir': bajar (hasta 0%) el cargo por credito al
   cobrar, sin poder subirlo por encima del predeterminado.
   ('pos.recargo.ajustar' permite subir y bajar hasta el tope.)
   Super Administrador y Cajera lo reciben. Quien ya tenga sesion abierta
   debe volver a iniciarla. Idempotente.
   ===================================================================== */

USE CarniAppDb;
GO

IF NOT EXISTS (SELECT * FROM Permiso WHERE Clave = 'pos.recargo.reducir')
    INSERT INTO Permiso (Clave, Descripcion)
    VALUES ('pos.recargo.reducir', 'Bajar el porcentaje de cargo por credito al cobrar (hasta 0%)');
GO

INSERT INTO RolPermiso (RolId, PermisoId)
SELECT r.Id, p.Id
FROM Rol r
CROSS JOIN Permiso p
WHERE r.Nombre IN ('Super Administrador', 'Cajera')
  AND p.Clave = 'pos.recargo.reducir'
  AND NOT EXISTS (
      SELECT 1 FROM RolPermiso rp WHERE rp.RolId = r.Id AND rp.PermisoId = p.Id
  );
GO
