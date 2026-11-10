import { useEffect, useRef, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { Plus, Search } from "lucide-react";
import { clsx } from "clsx";
import { toast } from "sonner";
import {
  cambiarEstadoUsuario,
  getUsuarios,
  restablecerContrasena,
} from "../api/endpoints";
import { useAuth } from "../auth/useAuth";
import { ConfirmDialog } from "../components/ConfirmDialog";
import { DialogoFormulario } from "../components/DialogoFormulario";
import { Paginacion } from "../components/Paginacion";
import { Selector } from "../components/Selector";
import {
  Button,
  EmptyState,
  Field,
  Notice,
  PageHeader,
  StatusTag,
} from "../components/ui";
import { formatDate } from "../utils/format";
import {
  OPCIONES_ROL,
  ROLES,
  erroresDe,
  validarContrasena,
} from "../utils/usuarios";
import styles from "../components/Listado.module.css";
import buscador from "./Vehiculos.module.css";

const LIMIT = 10;
const SEARCH_DELAY_MS = 300;

const PESTANAS = [
  { valor: "activo", texto: "Activos" },
  { valor: "inactivo", texto: "Inactivos" },
  { valor: "todos", texto: "Todos" },
];

const nombreDe = (usuario) => `${usuario.nombres} ${usuario.apellidos}`;

export default function Usuarios() {
  const { usuario: actual } = useAuth();
  const [params, setParams] = useSearchParams();
  const estado = params.get("estado") ?? "activo";
  const rol = params.get("rol") ?? "";
  const q = params.get("q") ?? "";
  const page = Number(params.get("page") ?? 1);
  const [busqueda, setBusqueda] = useState(q);
  const [respuesta, setRespuesta] = useState({ clave: null });
  const [recarga, setRecarga] = useState(0);
  const [aDesactivar, setADesactivar] = useState(null);
  const [aRestablecer, setARestablecer] = useState(null);
  const [contrasenas, setContrasenas] = useState({
    nueva: "",
    confirmacion: "",
  });
  const [errores, setErrores] = useState({});
  const [errorGeneral, setErrorGeneral] = useState("");
  const [ocupado, setOcupado] = useState(false);
  const temporizador = useRef(null);

  const clave = JSON.stringify({ estado, rol, q, page, recarga });

  useEffect(() => {
    const controller = new AbortController();
    getUsuarios({ estado, rol, q, page, limit: LIMIT }, controller.signal)
      .then((resultado) => setRespuesta({ clave, resultado }))
      .catch((falla) => {
        if (falla.name !== "AbortError") setRespuesta({ clave, error: falla });
      });
    return () => controller.abort();
  }, [clave, estado, rol, q, page]);

  useEffect(() => () => clearTimeout(temporizador.current), []);

  const cambiar = (cambios) =>
    setParams(
      (anteriores) => {
        const siguientes = new URLSearchParams(anteriores);
        Object.entries(cambios).forEach(([nombre, valor]) => {
          if (valor) siguientes.set(nombre, valor);
          else siguientes.delete(nombre);
        });
        if (!("page" in cambios)) siguientes.delete("page");
        return siguientes;
      },
      { replace: true },
    );

  const buscar = (valor) => {
    setBusqueda(valor);
    clearTimeout(temporizador.current);
    temporizador.current = setTimeout(
      () => cambiar({ q: valor.trim() }),
      SEARCH_DELAY_MS,
    );
  };

  const cambiarEstado = async (usuario, activo) => {
    setOcupado(true);
    try {
      await cambiarEstadoUsuario(usuario.usuario_id, activo);
      toast.success(activo ? "Cuenta activada" : "Cuenta desactivada", {
        description: nombreDe(usuario),
      });
      setADesactivar(null);
      setRecarga((valor) => valor + 1);
    } catch (falla) {
      toast.error("No se pudo cambiar el estado", {
        description: falla.message,
      });
    } finally {
      setOcupado(false);
    }
  };

  const abrirRestablecer = (usuario) => {
    setARestablecer(usuario);
    setContrasenas({ nueva: "", confirmacion: "" });
    setErrores({});
    setErrorGeneral("");
  };

  const guardarContrasena = async () => {
    const problema = validarContrasena(
      contrasenas.nueva,
      contrasenas.confirmacion,
    );
    if (problema) {
      setErrores({ contrasena_nueva: problema });
      return;
    }
    setOcupado(true);
    try {
      await restablecerContrasena(aRestablecer.usuario_id, contrasenas.nueva);
      toast.success("Contraseña restablecida", {
        description: `Comunícasela a ${nombreDe(aRestablecer)} por un medio seguro.`,
      });
      setARestablecer(null);
    } catch (falla) {
      const porCampo = erroresDe(falla);
      setErrores(porCampo);
      if (Object.keys(porCampo).length === 0) setErrorGeneral(falla.message);
    } finally {
      setOcupado(false);
    }
  };

  const cargando = respuesta.clave !== clave;
  const error = cargando ? null : respuesta.error;
  const usuarios = respuesta.resultado?.data ?? [];

  return (
    <>
      <PageHeader titulo="Usuarios">
        <Button to="/usuarios/nuevo" icon={Plus}>
          Nuevo usuario
        </Button>
      </PageHeader>

      <div
        className={styles.pestanas}
        role="group"
        aria-label="Filtrar por estado"
      >
        {PESTANAS.map(({ valor, texto }) => (
          <button
            key={valor}
            type="button"
            aria-pressed={estado === valor}
            className={clsx(
              styles.pestana,
              estado === valor && styles.pestanaActiva,
            )}
            onClick={() => cambiar({ estado: valor === "activo" ? "" : valor })}
          >
            {texto}
          </button>
        ))}
      </div>

      <div className={styles.filtros}>
        <label className={buscador.buscador}>
          <span className="visually-hidden">Buscar por nombre o correo</span>
          <Search size={16} strokeWidth={1.75} aria-hidden="true" />
          <input
            type="search"
            placeholder="Buscar por nombre o correo"
            value={busqueda}
            onChange={(event) => buscar(event.target.value)}
          />
        </label>
        <Selector
          compacto
          label="Rol"
          opcionVacia="Todos los roles"
          opciones={OPCIONES_ROL}
          value={rol}
          onChange={(valor) => cambiar({ rol: valor })}
        />
      </div>

      {error ? (
        <Notice
          tono="peligro"
          titulo="No pudimos cargar los usuarios"
          role="alert"
        >
          <p>{error.message}</p>
          <Button
            variant="enlacePeligro"
            onClick={() => setRecarga((valor) => valor + 1)}
          >
            Reintentar
          </Button>
        </Notice>
      ) : !cargando && usuarios.length === 0 ? (
        <EmptyState titulo="Ningún usuario coincide">
          Revisa la búsqueda o cambia los filtros.
        </EmptyState>
      ) : (
        <div className={styles.envoltura} aria-busy={cargando}>
          <table className={styles.tabla}>
            <thead>
              <tr>
                <th scope="col">Nombre</th>
                <th scope="col">Correo</th>
                <th scope="col">Rol</th>
                <th scope="col">Estado</th>
                <th scope="col">Creado</th>
                <th scope="col">
                  <span className="visually-hidden">Acciones</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {usuarios.map((usuario) => {
                const esActual = usuario.usuario_id === actual.usuario_id;
                return (
                  <tr key={usuario.usuario_id}>
                    <th scope="row">
                      {nombreDe(usuario)}
                      {esActual && <span className={styles.tenue}> · tú</span>}
                    </th>
                    <td>{usuario.correo}</td>
                    <td>{ROLES[usuario.rol]}</td>
                    <td>
                      <StatusTag tono={usuario.activo ? "exito" : "neutro"}>
                        {usuario.activo ? "Activo" : "Inactivo"}
                      </StatusTag>
                    </td>
                    <td className={styles.tenue}>
                      {formatDate(usuario.creado_en)}
                    </td>
                    <td>
                      <span className={styles.acciones}>
                        <Link to={`/usuarios/${usuario.usuario_id}/editar`}>
                          Editar
                        </Link>
                        <Button
                          variant="enlace"
                          onClick={() => abrirRestablecer(usuario)}
                        >
                          Restablecer contraseña
                        </Button>
                        {usuario.activo ? (
                          !esActual && (
                            <Button
                              variant="enlacePeligro"
                              onClick={() => setADesactivar(usuario)}
                            >
                              Desactivar
                            </Button>
                          )
                        ) : (
                          <Button
                            variant="enlace"
                            disabled={ocupado}
                            onClick={() => cambiarEstado(usuario, true)}
                          >
                            Activar
                          </Button>
                        )}
                      </span>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      {!error && (
        <Paginacion
          meta={respuesta.resultado?.meta}
          onCambiar={(pagina) => cambiar({ page: String(pagina) })}
        />
      )}

      <ConfirmDialog
        abierto={Boolean(aDesactivar)}
        titulo={`¿Desactivar a ${aDesactivar ? nombreDe(aDesactivar) : ""}?`}
        accion="Desactivar"
        ocupado={ocupado}
        onConfirmar={() => cambiarEstado(aDesactivar, false)}
        onCerrar={() => setADesactivar(null)}
      >
        No podrá iniciar sesión. Su historial se conserva y puedes activarla de
        nuevo cuando quieras.
      </ConfirmDialog>

      <DialogoFormulario
        abierto={Boolean(aRestablecer)}
        titulo="Restablecer contraseña"
        descripcion={
          aRestablecer
            ? `${nombreDe(aRestablecer)} · ${aRestablecer.correo}`
            : undefined
        }
        accion="Restablecer"
        ocupado={ocupado}
        error={errorGeneral}
        onEnviar={guardarContrasena}
        onCerrar={() => setARestablecer(null)}
      >
        <Field
          label="Nueva contraseña"
          type="password"
          autoComplete="new-password"
          value={contrasenas.nueva}
          onChange={(event) =>
            setContrasenas((anterior) => ({
              ...anterior,
              nueva: event.target.value,
            }))
          }
          error={errores.contrasena_nueva}
          help="Al menos 8 caracteres, con letras y números."
        />
        <Field
          label="Confirmar contraseña"
          type="password"
          autoComplete="new-password"
          value={contrasenas.confirmacion}
          onChange={(event) =>
            setContrasenas((anterior) => ({
              ...anterior,
              confirmacion: event.target.value,
            }))
          }
        />
      </DialogoFormulario>
    </>
  );
}
