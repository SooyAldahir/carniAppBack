/* =====================================================================
   CarniApp - Cargo por venta a credito (visible en el ticket).
   Venta.RecargoCredito guarda el importe del cargo; Venta.Total ya lo incluye.
   Idempotente.
   ===================================================================== */

USE CarniAppDb;
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Venta') AND name = 'RecargoCredito')
BEGIN
    ALTER TABLE Venta ADD RecargoCredito DECIMAL(10,2) NOT NULL CONSTRAINT DF_Venta_RecargoCredito DEFAULT 0;
END
GO
