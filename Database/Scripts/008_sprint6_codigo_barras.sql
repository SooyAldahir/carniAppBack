/* =====================================================================
   CarniApp - Sprint 6 (POS + hardware)
   - Agrega Corte.CodigoBarras (opcional) para el lector de codigo de
     barras del POS (RF-POS-006). Unico entre cortes que lo tengan.
   Idempotente.
   ===================================================================== */

USE CarniAppDb;
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Corte') AND name = 'CodigoBarras')
BEGIN
    ALTER TABLE Corte ADD CodigoBarras NVARCHAR(50) NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UX_Corte_CodigoBarras' AND object_id = OBJECT_ID('Corte'))
BEGIN
    CREATE UNIQUE INDEX UX_Corte_CodigoBarras ON Corte (CodigoBarras) WHERE CodigoBarras IS NOT NULL;
END
GO
