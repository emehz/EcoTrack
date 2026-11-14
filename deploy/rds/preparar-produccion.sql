-- Preparar la base de produccion despues de ecotrack_unificado.sql y database/sesiones.sql.
-- Uso desde la EC2:
--   psql "host=ENDPOINT port=5432 dbname=ecotrack_db user=USUARIO_MAESTRO sslmode=require" \
--        -v clave_app='CLAVE_PARA_LA_API' -v clave_admin='CLAVE_DEL_ADMINISTRADOR' \
--        -f deploy/rds/preparar-produccion.sql
-- No se usa ajustes_desarrollo_local.sql en RDS.

\set ON_ERROR_STOP on

CREATE EXTENSION IF NOT EXISTS pgcrypto;

ALTER ROLE ecotrack_app LOGIN PASSWORD :'clave_app';

UPDATE ecotrack.usuario
   SET hash_contrasena = crypt(:'clave_admin', gen_salt('bf', 12)),
       activo = true
 WHERE correo = 'admin@ecotrack.sv';

UPDATE ecotrack.usuario
   SET activo = false
 WHERE correo IN ('operador@ecotrack.sv', 'auditor@ecotrack.sv');

SELECT correo, rol, activo FROM ecotrack.usuario ORDER BY rol;
