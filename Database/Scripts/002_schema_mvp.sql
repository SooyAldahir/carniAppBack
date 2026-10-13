/* =====================================================================
   CarniApp - Esquema MVP (Sprints 1-9)
   Catalogos, Inventario, Clientes, Punto de Venta y Caja.
   Diseno completo del MVP; el codigo C# lo implementa de forma
   incremental conforme avanzan los sprints del plan de desarrollo.
   ===================================================================== */

USE CarniAppDb;
GO

-- RF-INV-001: catalogo de tipos de animal (configurable, no fijo en codigo)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TipoAnimal')
BEGIN
    CREATE TABLE TipoAnimal (
        Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        Nombre          NVARCHAR(100)    NOT NULL,   -- Res, Cerdo, Pollo...
        FechaCreacion   DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        Activo          BIT              NOT NULL DEFAULT 1
    );
END
GO

-- RF-INV-007: proveedores de animales
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Proveedor')
BEGIN
    CREATE TABLE Proveedor (
        Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        Nombre          NVARCHAR(150)    NOT NULL,
        Telefono        NVARCHAR(30)     NULL,
        FechaCreacion   DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        Activo          BIT              NOT NULL DEFAULT 1
    );
END
GO

-- RF-INV-002/CONFIG-002: catalogo de cortes (= "producto" vendible)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Corte')
BEGIN
    CREATE TABLE Corte (
        Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        TipoAnimalId    UNIQUEIDENTIFIER NOT NULL,
        Nombre          NVARCHAR(100)    NOT NULL,            -- Bistec, Costilla, Molida...
        UnidadVenta     NVARCHAR(20)     NOT NULL,            -- 'Peso' o 'Pieza' (RF-POS-001/002)
        PrecioBase      DECIMAL(10,2)    NOT NULL,
        StockMinimo     DECIMAL(10,3)    NOT NULL DEFAULT 0,  -- RF-INV-003
        FechaCreacion   DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        Activo          BIT              NOT NULL DEFAULT 1,
        CONSTRAINT FK_Corte_TipoAnimal FOREIGN KEY (TipoAnimalId) REFERENCES TipoAnimal(Id)
    );
END
GO

-- RF-INV-001: lotes de animal comprado (origen de Venta Facil / Produccion)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LoteAnimal')
BEGIN
    CREATE TABLE LoteAnimal (
        Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        TipoAnimalId    UNIQUEIDENTIFIER NOT NULL,
        ProveedorId     UNIQUEIDENTIFIER NULL,
        PesoKg          DECIMAL(10,3)    NOT NULL,
        PrecioCompra    DECIMAL(10,2)    NOT NULL,
        FechaCompra     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        EsEstimado      BIT              NOT NULL DEFAULT 1,  -- RF-VF-005: estimado (Venta Facil) vs medido (Produccion)
        FechaCreacion   DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        Activo          BIT              NOT NULL DEFAULT 1,
        CONSTRAINT FK_LoteAnimal_TipoAnimal FOREIGN KEY (TipoAnimalId) REFERENCES TipoAnimal(Id),
        CONSTRAINT FK_LoteAnimal_Proveedor FOREIGN KEY (ProveedorId) REFERENCES Proveedor(Id)
    );
END
GO

-- RF-INV-004: stock actual por corte (saldo vivo, separado del kardex de movimientos)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Inventario')
BEGIN
    CREATE TABLE Inventario (
        Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        CorteId         UNIQUEIDENTIFIER NOT NULL,
        SucursalId      UNIQUEIDENTIFIER NOT NULL,
        StockActual     DECIMAL(10,3)    NOT NULL DEFAULT 0,
        FechaActualizacion DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_Inventario_Corte FOREIGN KEY (CorteId) REFERENCES Corte(Id),
        CONSTRAINT FK_Inventario_Sucursal FOREIGN KEY (SucursalId) REFERENCES Sucursal(Id),
        CONSTRAINT UQ_Inventario_Corte_Sucursal UNIQUE (CorteId, SucursalId)
    );
END
GO

-- RF-INV-008: kardex de movimientos (entrada por produccion/venta facil, salida por venta/merma)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MovimientoInventario')
BEGIN
    CREATE TABLE MovimientoInventario (
        Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        CorteId         UNIQUEIDENTIFIER NOT NULL,
        SucursalId      UNIQUEIDENTIFIER NOT NULL,
        LoteAnimalId    UNIQUEIDENTIFIER NULL,               -- trazabilidad al origen (RF-INV-004)
        TipoMovimiento  NVARCHAR(30)     NOT NULL,           -- 'Entrada', 'Venta', 'Merma', 'Ajuste'
        Cantidad        DECIMAL(10,3)    NOT NULL,           -- positivo entrada, negativo salida
        Motivo          NVARCHAR(300)    NULL,               -- RF-INV-005: motivo obligatorio en ajustes
        UsuarioId       UNIQUEIDENTIFIER NULL,
        Fecha           DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_MovInv_Corte FOREIGN KEY (CorteId) REFERENCES Corte(Id),
        CONSTRAINT FK_MovInv_Sucursal FOREIGN KEY (SucursalId) REFERENCES Sucursal(Id),
        CONSTRAINT FK_MovInv_LoteAnimal FOREIGN KEY (LoteAnimalId) REFERENCES LoteAnimal(Id),
        CONSTRAINT FK_MovInv_Usuario FOREIGN KEY (UsuarioId) REFERENCES Usuario(Id)
    );
END
GO

-- RF-CLI-001: clientes
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Cliente')
BEGIN
    CREATE TABLE Cliente (
        Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        Nombre          NVARCHAR(150)    NOT NULL,
        Telefono        NVARCHAR(30)     NULL,
        Direccion       NVARCHAR(300)    NULL,
        Latitud         DECIMAL(10,7)    NULL,               -- RF-MAP-001
        Longitud        DECIMAL(10,7)    NULL,
        FotoFachadaUrl  NVARCHAR(500)    NULL,                -- RF-CLI-001 / RF-REP-006
        Referencias     NVARCHAR(500)    NULL,
        LimiteCredito   DECIMAL(10,2)    NOT NULL DEFAULT 0,  -- RF-CRED-001
        SaldoCredito    DECIMAL(10,2)    NOT NULL DEFAULT 0,
        PuntosAcumulados INT             NOT NULL DEFAULT 0,  -- RF-REC-001
        ConsentimientoUbicacionFoto BIT  NOT NULL DEFAULT 0,  -- RNF-PRIV-001
        FechaCreacion   DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        Activo          BIT              NOT NULL DEFAULT 1
    );
END
GO

-- RF-POS-008: catalogo de metodos de pago (configurable)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MetodoPago')
BEGIN
    CREATE TABLE MetodoPago (
        Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        Nombre          NVARCHAR(50)     NOT NULL,   -- Efectivo, Tarjeta, Transferencia, Credito
        Activo          BIT              NOT NULL DEFAULT 1
    );
END
GO

-- RF-POS-001..011: venta (encabezado del ticket)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Venta')
BEGIN
    CREATE TABLE Venta (
        Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        Folio           INT IDENTITY(1000,1) NOT NULL,
        SucursalId      UNIQUEIDENTIFIER NOT NULL,
        ClienteId       UNIQUEIDENTIFIER NULL,
        UsuarioId       UNIQUEIDENTIFIER NOT NULL,           -- cajera que atendio
        Subtotal        DECIMAL(10,2)    NOT NULL,
        Descuento       DECIMAL(10,2)    NOT NULL DEFAULT 0,
        Total           DECIMAL(10,2)    NOT NULL,
        Estado          NVARCHAR(20)     NOT NULL DEFAULT 'Completada', -- Completada, Anulada
        MotivoAnulacion NVARCHAR(300)    NULL,                -- RN-003
        PendienteSincronizar BIT         NOT NULL DEFAULT 0,  -- RF-POS-012
        Fecha           DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_Venta_Sucursal FOREIGN KEY (SucursalId) REFERENCES Sucursal(Id),
        CONSTRAINT FK_Venta_Cliente FOREIGN KEY (ClienteId) REFERENCES Cliente(Id),
        CONSTRAINT FK_Venta_Usuario FOREIGN KEY (UsuarioId) REFERENCES Usuario(Id)
    );
END
GO

-- Detalle de venta (lineas del ticket)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'VentaDetalle')
BEGIN
    CREATE TABLE VentaDetalle (
        Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        VentaId         UNIQUEIDENTIFIER NOT NULL,
        CorteId         UNIQUEIDENTIFIER NOT NULL,
        Cantidad        DECIMAL(10,3)    NOT NULL,           -- kg o piezas segun UnidadVenta
        PrecioUnitario  DECIMAL(10,2)    NOT NULL,
        Importe         DECIMAL(10,2)    NOT NULL,
        CONSTRAINT FK_VentaDetalle_Venta FOREIGN KEY (VentaId) REFERENCES Venta(Id) ON DELETE CASCADE,
        CONSTRAINT FK_VentaDetalle_Corte FOREIGN KEY (CorteId) REFERENCES Corte(Id)
    );
END
GO

-- Pagos de una venta (soporta pago mixto, RF-POS-008)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'VentaPago')
BEGIN
    CREATE TABLE VentaPago (
        Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        VentaId         UNIQUEIDENTIFIER NOT NULL,
        MetodoPagoId    UNIQUEIDENTIFIER NOT NULL,
        Importe         DECIMAL(10,2)    NOT NULL,
        CONSTRAINT FK_VentaPago_Venta FOREIGN KEY (VentaId) REFERENCES Venta(Id) ON DELETE CASCADE,
        CONSTRAINT FK_VentaPago_MetodoPago FOREIGN KEY (MetodoPagoId) REFERENCES MetodoPago(Id)
    );
END
GO

-- RF-CRED-002/003: abonos a credito
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AbonoCredito')
BEGIN
    CREATE TABLE AbonoCredito (
        Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        ClienteId       UNIQUEIDENTIFIER NOT NULL,
        Importe         DECIMAL(10,2)    NOT NULL,
        UsuarioId       UNIQUEIDENTIFIER NOT NULL,
        Fecha           DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_AbonoCredito_Cliente FOREIGN KEY (ClienteId) REFERENCES Cliente(Id),
        CONSTRAINT FK_AbonoCredito_Usuario FOREIGN KEY (UsuarioId) REFERENCES Usuario(Id)
    );
END
GO

-- RF-CAJA-001..005: corte de caja
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CorteCaja')
BEGIN
    CREATE TABLE CorteCaja (
        Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        SucursalId          UNIQUEIDENTIFIER NOT NULL,
        UsuarioId           UNIQUEIDENTIFIER NOT NULL,
        FechaInicio         DATETIME2        NOT NULL,
        FechaCierre         DATETIME2        NULL,
        TotalVentas         DECIMAL(10,2)    NOT NULL DEFAULT 0,
        TotalGastos         DECIMAL(10,2)    NOT NULL DEFAULT 0,
        EfectivoEsperado    DECIMAL(10,2)    NOT NULL DEFAULT 0,
        EfectivoContado     DECIMAL(10,2)    NULL,
        Diferencia          DECIMAL(10,2)    NULL,
        Estado              NVARCHAR(20)     NOT NULL DEFAULT 'Abierto', -- Abierto, Cerrado
        CONSTRAINT FK_CorteCaja_Sucursal FOREIGN KEY (SucursalId) REFERENCES Sucursal(Id),
        CONSTRAINT FK_CorteCaja_Usuario FOREIGN KEY (UsuarioId) REFERENCES Usuario(Id)
    );
END
GO
