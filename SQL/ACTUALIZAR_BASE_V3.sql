-- HillApp v3 - actualización sobre la base v2 que ya tienes funcionando.
-- Ejecuta TODO este archivo UNA VEZ en TiDB Cloud > SQL Editor.
-- No borra usuarios, récords ni comentarios existentes.

USE hill_records;

-- Fecha persistente de última actividad. A diferencia de SessionLastSeen,
-- esta columna NO se borra al cerrar sesión.
ALTER TABLE Usuarios
    ADD COLUMN IF NOT EXISTS UltimoAcceso TIMESTAMP(6) NULL DEFAULT NULL;

-- Inicializa usuarios ya existentes para que no aparezcan como inactivos
-- desde antes de instalar esta versión.
UPDATE Usuarios
SET UltimoAcceso = COALESCE(UltimoAcceso, SessionLastSeen, FechaRegistro)
WHERE UltimoAcceso IS NULL;

-- Ayuda al login por correo y a localizar la cuenta de gestión.
CREATE INDEX IF NOT EXISTS idx_usuarios_correo
    ON Usuarios (Correo);

SHOW TABLES;
DESCRIBE Usuarios;
