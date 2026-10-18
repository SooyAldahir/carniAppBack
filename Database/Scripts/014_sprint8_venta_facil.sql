/* =====================================================================
   Sprint 8 - Venta Facil (RF-VF-*)
   - Corte.RendimientoPorcentaje: % del peso del animal que da cada corte
     (solo cortes por peso). La suma por tipo de animal no puede pasar de 100;
     el resto es hueso/merma.
   - Permiso 'ventafacil.registrar': registrar lotes de animal (Super Admin).
   La configuracion de rendimientos usa 'catalogo.administrar'.
   Idempotente. El usuario debe volver a iniciar sesion para el permiso nuevo.
   ===================================================================== */

USE CarniAppDb;
GO

IF COL_LENGTH('Corte', 'RendimientoPorcentaje') IS NULL
    ALTER TABLE Corte ADD RendimientoPorcentaje DECIMAL(5,2) NULL;
GO

IF NOT EXISTS (SELECT * FROM Permiso WHERE Clave = 'ventafacil.registrar')
    INSERT INTO Permiso (Clave, Descripcion)
    VALUES ('ventafacil.registrar', 'Registrar lotes de animal y repartirlos en cortes (Venta Facil)');
GO

INSERT INTO RolPermiso (RolId, PermisoId)
SELECT r.Id, p.Id
FROM Rol r
CROSS JOIN Permiso p
WHERE r.Nombre = 'Super Administrador'
  AND p.Clave = 'ventafacil.registrar'
  AND NOT EXISTS (
      SELECT 1 FROM RolPermiso rp WHERE rp.RolId = r.Id AND rp.PermisoId = p.Id
  );
GO
