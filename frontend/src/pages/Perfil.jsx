import { useState } from "react";
import { KeyRound } from "lucide-react";
import { useAuth } from "../auth/useAuth";
import { CambiarContrasena } from "../components/CambiarContrasena";
import { Button, Metrics, PageHeader } from "../components/ui";
import { formatDate } from "../utils/format";
import styles from "./Perfil.module.css";

const PERMISOS = {
  admin:
    "Configuras factores de emisión, choferes, vehículos y usuarios, y consultas la bitácora.",
  operador:
    "Registras viajes y cargas de combustible, y generas reportes de viajes.",
  auditor:
    "Auditas los reportes de viajes: registras hallazgos, corriges versiones y cierras auditorías.",
};

const ROLES = {
  admin: "Administrador",
  operador: "Operador",
  auditor: "Auditor",
};

export default function Perfil() {
  const { usuario } = useAuth();
  const [cambiando, setCambiando] = useState(false);

  return (
    <>
      <PageHeader titulo="Mi perfil">
        <Button
          variant="secundario"
          icon={KeyRound}
          onClick={() => setCambiando(true)}
        >
          Cambiar contraseña
        </Button>
      </PageHeader>
      <div className={styles.cuerpo}>
        <Metrics
          items={[
            {
              etiqueta: "Nombre",
              valor: `${usuario.nombres} ${usuario.apellidos}`,
            },
            { etiqueta: "Correo", valor: usuario.correo },
            { etiqueta: "Rol", valor: ROLES[usuario.rol] },
            {
              etiqueta: "Estado",
              valor: usuario.activo ? "Activa" : "Inactiva",
            },
            { etiqueta: "Cuenta creada", valor: formatDate(usuario.creado_en) },
          ]}
        />
        <aside className={styles.permisos}>
          <h2 className={styles.titulo}>Permisos de tu rol</h2>
          <p>{PERMISOS[usuario.rol]}</p>
        </aside>
      </div>
      <CambiarContrasena
        abierto={cambiando}
        onCerrar={() => setCambiando(false)}
      />
    </>
  );
}
