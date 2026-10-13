/* =====================================================================
   CarniApp - Esquema CORE (Sprint 0)
   Tablas: Negocio, Sucursal, Rol, Permiso, RolPermiso, Usuario.
   Corresponde exactamente al modelo EF Core de Sprint 0
   (CarniApp.Domain.Entities + CarniAppDbContext).
   ===================================================================== */

IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'CarniAppDb')
BEGIN
    CREATE DATABASE CarniAppDb;
END
GO

USE CarniAppDb;
GO

-- RF-CONFIG-003: configuracion de marca por instalacion (D-01: single-tenant)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Negocio')
BEGIN
    CREATE TABLE Negocio (
        Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        NombreComercial     NVARCHAR(150)    NOT NULL,
        LogoUrl             NVARCHAR(500)    NULL,
        Telefono            NVARCHAR(30)     NULL,
        RedSocialPrincipal  NVARCHAR(200)    NULL,
        Direccion           NVARCHAR(300)    NULL,
        Moneda              NVARCHAR(10)     NOT NULL DEFAULT 'MXN',
        ZonaHoraria         NVARCHAR(60)     NOT NULL DEFAULT 'America/Mexico_City',
        FechaCreacion       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        FechaActualizacion  DATETIME2        NULL,
        Activo              BIT              NOT NULL DEFAULT 1
    );
END
GO

-- SUP-01: Sucursal desde el dia uno, aunque hoy se opere una sola ubicacion
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Sucursal')
BEGIN
    CREATE TABLE Sucursal (
        Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        Nombre              NVARCHAR(150)    NOT NULL,
        Direccion           NVARCHAR(300)    NULL,
        EsPrincipal         BIT              NOT NULL DEFAULT 1,
        FechaCreacion       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        FechaActualizacion  DATETIME2        NULL,
        Activo              BIT              NOT NULL DEFAULT 1
    );
END
GO

-- SUP-03: catalogo de roles configurable (no fijo a 3 roles en codigo)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Rol')
BEGIN
    CREATE TABLE Rol (
        Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        Nombre              NVARCHAR(100)    NOT NULL,
        Descripcion         NVARCHAR(300)    NULL,
        FechaCreacion       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        FechaActualizacion  DATETIME2        NULL,
        Activo              BIT              NOT NULL DEFAULT 1
    );
END
GO

-- SUP-03: catalogo de permisos atomicos (ej. "pos.vender")
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Permiso')
BEGIN
    CREATE TABLE Permiso (
        Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        Clave               NVARCHAR(100)    NOT NULL,
        Descripcion         NVARCHAR(300)    NULL,
        FechaCreacion       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        FechaActualizacion  DATETIME2        NULL,
        Activo              BIT              NOT NULL DEFAULT 1
    );
END
GO

-- RF-AUTH-004: RBAC configurable via tabla de union Rol-Permiso
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RolPermiso')
BEGIN
    CREATE TABLE RolPermiso (
        RolId       UNIQUEIDENTIFIER NOT NULL,
        PermisoId   UNIQUEIDENTIFIER NOT NULL,
        CONSTRAINT PK_RolPermiso PRIMARY KEY (RolId, PermisoId),
        CONSTRAINT FK_RolPermiso_Rol FOREIGN KEY (RolId) REFERENCES Rol(Id),
        CONSTRAINT FK_RolPermiso_Permiso FOREIGN KEY (PermisoId) REFERENCES Permiso(Id)
    );
END
GO

-- RF-AUTH-001/005: usuarios del sistema. PasswordHash nunca texto plano (RNF-SEC-002)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Usuario')
BEGIN
    CREATE TABLE Usuario (
        Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        NombreCompleto      NVARCHAR(150)    NOT NULL,
        NombreUsuario       NVARCHAR(100)    NOT NULL,
        Correo              NVARCHAR(150)    NULL,
        PasswordHash        NVARCHAR(500)    NOT NULL,
        RolId               UNIQUEIDENTIFIER NOT NULL,
        IntentosFallidos    INT              NOT NULL DEFAULT 0,
        BloqueadoHasta      DATETIME2        NULL,
        FechaCreacion       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        FechaActualizacion  DATETIME2        NULL,
        Activo              BIT              NOT NULL DEFAULT 1,
        CONSTRAINT FK_Usuario_Rol FOREIGN KEY (RolId) REFERENCES Rol(Id)
    );

    CREATE UNIQUE INDEX IX_Usuario_NombreUsuario ON Usuario(NombreUsuario);
END
GO
