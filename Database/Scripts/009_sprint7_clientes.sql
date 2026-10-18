/* =====================================================================
   CarniApp - Sprint 7 (Clientes y credito)
   - Agrega Cliente.FechaActualizacion (para que coincida con BaseEntity).
   Los permisos 'clientes.administrar' y 'creditos.administrar' ya existen
   desde 003_seed_data.sql.
   Idempotente.
   ===================================================================== */

USE CarniAppDb;
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Cliente') AND name = 'FechaActualizacion')
BEGIN
    ALTER TABLE Cliente ADD FechaActualizacion DATETIME2 NULL;
END
GO
