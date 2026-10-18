/* =====================================================================
   CarniApp - Nota opcional en la venta (se imprime en el ticket).
   Idempotente.
   ===================================================================== */

USE CarniAppDb;
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Venta') AND name = 'Nota')
BEGIN
    ALTER TABLE Venta ADD Nota NVARCHAR(200) NULL;
END
GO
