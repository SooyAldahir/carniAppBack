/* =====================================================================
   CarniApp - Ajustes para Sprint 2 (Catalogos: TipoAnimal y Corte)
   - Agrega FechaActualizacion a TipoAnimal y Corte (faltaba en el
     diseno original del esquema MVP, para que coincidan con BaseEntity).
   - Agrega el permiso 'catalogo.administrar' y se lo asigna al
     Super Administrador.
   Idempotente: puede ejecutarse varias veces sin error.
   ===================================================================== */

USE CarniAppDb;
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('TipoAnimal') AND name = 'FechaActualizacion')
BEGIN
    ALTER TABLE TipoAnimal ADD FechaActualizacion DATETIME2 NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Corte') AND name = 'FechaActualizacion')
BEGIN
    ALTER TABLE Corte ADD FechaActualizacion DATETIME2 NULL;
END
GO

-- RF-CONFIG-002/RF-INV-002: administracion del catalogo de cortes y tipos de animal
IF NOT EXISTS (SELECT * FROM Permiso WHERE Clave = 'catalogo.administrar')
BEGIN
    INSERT INTO Permiso (Clave, Descripcion)
    VALUES ('catalogo.administrar', 'Administrar catalogo de cortes y tipos de animal');
END
GO

INSERT INTO RolPermiso (RolId, PermisoId)
SELECT r.Id, p.Id
FROM Rol r
CROSS JOIN Permiso p
WHERE r.Nombre = 'Super Administrador'
  AND p.Clave = 'catalogo.administrar'
  AND NOT EXISTS (
      SELECT 1 FROM RolPermiso rp WHERE rp.RolId = r.Id AND rp.PermisoId = p.Id
  );
GO
