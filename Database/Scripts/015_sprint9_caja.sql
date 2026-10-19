/* =====================================================================
   Sprint 9 - Caja y corte de caja (RF-CAJA-*)
   - CorteCaja.FondoInicial: efectivo con el que se abre el turno.
   - CorteCaja.Notas: observaciones al cerrar.
   - GastoCaja: gastos / retiros pagados con efectivo de la caja (reducen lo esperado).
   - Indice unico filtrado: solo puede haber UN corte abierto por sucursal.
   Los permisos 'caja.cortar' y 'caja.reabrir' ya existen desde el seed inicial.
   Idempotente.
   ===================================================================== */

USE CarniAppDb;
GO

IF COL_LENGTH('CorteCaja', 'FondoInicial') IS NULL
    ALTER TABLE CorteCaja ADD FondoInicial DECIMAL(10,2) NOT NULL CONSTRAINT DF_CorteCaja_FondoInicial DEFAULT 0;
GO

IF COL_LENGTH('CorteCaja', 'Notas') IS NULL
    ALTER TABLE CorteCaja ADD Notas NVARCHAR(300) NULL;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'GastoCaja')
BEGIN
    CREATE TABLE GastoCaja (
        Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        CorteCajaId     UNIQUEIDENTIFIER NOT NULL,
        Concepto        NVARCHAR(100)    NOT NULL,
        Importe         DECIMAL(10,2)    NOT NULL,
        UsuarioId       UNIQUEIDENTIFIER NOT NULL,
        Fecha           DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_GastoCaja_CorteCaja FOREIGN KEY (CorteCajaId) REFERENCES CorteCaja(Id),
        CONSTRAINT FK_GastoCaja_Usuario FOREIGN KEY (UsuarioId) REFERENCES Usuario(Id)
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UX_CorteCaja_Abierto')
    CREATE UNIQUE INDEX UX_CorteCaja_Abierto ON CorteCaja (SucursalId) WHERE Estado = 'Abierto';
GO
