/* =====================================================================
   Permiso 'usuarios.administrar': dar de alta cajeros y demas usuarios,
   cambiarles el rol, desactivarlos y restablecer su contrasena.
   Solo el Super Administrador lo recibe. Quien ya tenga sesion abierta
   debe volver a iniciarla (los permisos viajan dentro del token). Idempotente.
   ===================================================================== */

USE CarniAppDb;
GO

IF NOT EXISTS (SELECT * FROM Permiso WHERE Clave = 'usuarios.administrar')
    INSERT INTO Permiso (Clave, Descripcion)
    VALUES ('usuarios.administrar', 'Administrar usuarios: altas, bajas, roles y contrasenas');
GO

INSERT INTO RolPermiso (RolId, PermisoId)
SELECT r.Id, p.Id
FROM Rol r
CROSS JOIN Permiso p
WHERE r.Nombre = 'Super Administrador'
  AND p.Clave = 'usuarios.administrar'
  AND NOT EXISTS (
      SELECT 1 FROM RolPermiso rp WHERE rp.RolId = r.Id AND rp.PermisoId = p.Id
  );
GO
