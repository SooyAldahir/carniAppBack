/* =====================================================================
   CarniApp - Refresh Tokens (Sprint 1)
   RF-AUTH-002 (JWT + refresh token) y RF-AUTH-007 (revocacion remota).
   Se guarda el HASH del token (SHA-256), nunca el token en texto plano.
   ===================================================================== */

USE CarniAppDb;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RefreshToken')
BEGIN
    CREATE TABLE RefreshToken (
        Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        UsuarioId           UNIQUEIDENTIFIER NOT NULL,
        TokenHash           NVARCHAR(200)    NOT NULL,
        ExpiraUtc           DATETIME2        NOT NULL,
        RevocadoUtc         DATETIME2        NULL,
        FechaCreacion       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        FechaActualizacion  DATETIME2        NULL,
        Activo              BIT              NOT NULL DEFAULT 1,
        CONSTRAINT FK_RefreshToken_Usuario FOREIGN KEY (UsuarioId) REFERENCES Usuario(Id)
    );

    CREATE UNIQUE INDEX IX_RefreshToken_TokenHash ON RefreshToken(TokenHash);
    CREATE INDEX IX_RefreshToken_UsuarioId ON RefreshToken(UsuarioId);
END
GO
