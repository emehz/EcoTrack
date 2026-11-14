-- Sesiones de EcoTrack: refresh tokens con rotacion.
-- Se ejecuta despues de ecotrack_unificado.sql, en local y en RDS. Se puede ejecutar mas de una vez.

CREATE TABLE IF NOT EXISTS ecotrack.sesion (
    sesion_id   uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    usuario_id  uuid NOT NULL REFERENCES ecotrack.usuario(usuario_id) ON DELETE CASCADE,
    familia_id  uuid NOT NULL,
    hash_token  text NOT NULL UNIQUE,
    creado_en   timestamptz NOT NULL DEFAULT now(),
    expira_en   timestamptz NOT NULL,
    usado_en    timestamptz,
    revocado_en timestamptz,
    ip          text,
    agente      text,
    CHECK (expira_en > creado_en)
);

CREATE INDEX IF NOT EXISTS idx_sesion_usuario ON ecotrack.sesion (usuario_id);
CREATE INDEX IF NOT EXISTS idx_sesion_familia ON ecotrack.sesion (familia_id);

COMMENT ON TABLE ecotrack.sesion IS 'Refresh tokens: solo se guarda su hash SHA-256. Cada uso crea uno nuevo de la misma familia.';

GRANT SELECT, INSERT, UPDATE, DELETE ON ecotrack.sesion TO ecotrack_app;
