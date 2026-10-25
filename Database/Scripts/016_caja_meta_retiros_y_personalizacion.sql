/* =====================================================================
   Sprint 12 - Ajustes para produccion
   - Venta.Nota: de 200 a 500 caracteres (lleva direccion y referencia del cliente).
   - CorteCaja.MetaEfectivo: proyeccion de efectivo en caja; al alcanzarla el POS
     avisa que hay que hacer un retiro.
   - GastoCaja.Tipo: 'Gasto' o 'Retiro' (el retiro baja el efectivo esperado, pero
     no es un gasto del negocio).
   - Negocio.ColorPrimario / PieTicket: personalizacion de la app y del ticket.
   Idempotente.
   ===================================================================== */

USE CarniAppDb;
GO

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Venta') AND name = 'Nota' AND max_length < 1000)
    ALTER TABLE Venta ALTER COLUMN Nota NVARCHAR(500) NULL;
GO

IF COL_LENGTH('CorteCaja', 'MetaEfectivo') IS NULL
    ALTER TABLE CorteCaja ADD MetaEfectivo DECIMAL(12,2) NULL;
GO

IF COL_LENGTH('GastoCaja', 'Tipo') IS NULL
    ALTER TABLE GastoCaja ADD Tipo NVARCHAR(10) NOT NULL CONSTRAINT DF_GastoCaja_Tipo DEFAULT 'Gasto';
GO

IF COL_LENGTH('Negocio', 'ColorPrimario') IS NULL
    ALTER TABLE Negocio ADD ColorPrimario NVARCHAR(9) NULL;
GO

IF COL_LENGTH('Negocio', 'PieTicket') IS NULL
    ALTER TABLE Negocio ADD PieTicket NVARCHAR(120) NULL;
GO
