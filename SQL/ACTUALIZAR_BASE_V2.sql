-- HillApp / Récords HCR2 - actualización desde la primera versión.
-- TU CASO: si ya tenías Usuarios y Records funcionando, ejecuta este archivo UNA VEZ.

USE hill_records;

ALTER TABLE Usuarios
    ADD COLUMN IF NOT EXISTS ActiveSessionToken VARCHAR(128) NULL;

ALTER TABLE Usuarios
    ADD COLUMN IF NOT EXISTS SessionLastSeen TIMESTAMP(6) NULL DEFAULT NULL;

CREATE INDEX IF NOT EXISTS idx_usuarios_session
    ON Usuarios (ActiveSessionToken);

CREATE TABLE IF NOT EXISTS Comentarios (
    ComentarioId BIGINT NOT NULL AUTO_INCREMENT,
    UsuarioId INT NOT NULL,
    CorreoUsuario VARCHAR(255) NOT NULL,
    Tipo VARCHAR(30) NOT NULL,
    Mensaje TEXT NOT NULL,
    EmailEnviado TINYINT(1) NOT NULL DEFAULT 0,
    FechaCreacion TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (ComentarioId),
    KEY idx_comentarios_usuario_fecha (UsuarioId, FechaCreacion),
    CONSTRAINT fk_comentarios_usuario
        FOREIGN KEY (UsuarioId)
        REFERENCES Usuarios(UsuarioId)
        ON DELETE CASCADE
        ON UPDATE CASCADE
);

SHOW TABLES;
DESCRIBE Usuarios;
DESCRIBE Comentarios;
