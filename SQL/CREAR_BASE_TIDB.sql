-- HillApp / Récords HCR2 - TiDB Cloud (versión 3)
-- Para una base NUEVA. Ejecuta TODO este archivo en el SQL Editor de TiDB Cloud.

CREATE DATABASE IF NOT EXISTS hill_records
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_unicode_ci;

USE hill_records;

CREATE TABLE IF NOT EXISTS Usuarios (
    UsuarioId INT NOT NULL AUTO_INCREMENT,
    NombreUsuario VARCHAR(100) NOT NULL,
    Correo VARCHAR(255) NOT NULL,
    Contrasena VARCHAR(500) NOT NULL,
    FechaRegistro TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UltimoAcceso TIMESTAMP(6) NULL DEFAULT NULL,
    ActiveSessionToken VARCHAR(128) NULL,
    SessionLastSeen TIMESTAMP(6) NULL DEFAULT NULL,
    PRIMARY KEY (UsuarioId),
    UNIQUE KEY uq_usuarios_nombre (NombreUsuario),
    UNIQUE KEY uq_usuarios_correo (Correo),
    KEY idx_usuarios_session (ActiveSessionToken)
);

CREATE TABLE IF NOT EXISTS Records (
    RecordId BIGINT NOT NULL AUTO_INCREMENT,
    Mapa VARCHAR(150) NOT NULL,
    Vehiculo VARCHAR(150) NOT NULL,
    Distancia INT NOT NULL,
    UsuarioId INT NOT NULL,
    FechaActualizacion TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (RecordId),
    UNIQUE KEY uq_records_usuario_mapa_vehiculo (UsuarioId, Mapa, Vehiculo),
    KEY idx_records_usuario (UsuarioId),
    CONSTRAINT fk_records_usuario
        FOREIGN KEY (UsuarioId)
        REFERENCES Usuarios(UsuarioId)
        ON DELETE CASCADE
        ON UPDATE CASCADE
);

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
DESCRIBE Records;
DESCRIBE Comentarios;
