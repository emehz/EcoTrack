import { useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { getBitacora, getUsuariosBitacora } from "../api/endpoints";
import { DetalleBitacora } from "../components/DetalleBitacora";
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
import { formatDateTime } from "../utils/format";
import {
  ACCIONES,
  ENTIDADES,
  OPCIONES_ACCION,
  OPCIONES_ENTIDAD,
  enlaceDe,
  idCorto,
} from "../utils/bitacora";
import styles from "../components/Listado.module.css";

const LIMIT = 20;
const CAMPOS_VISIBLES = 2;

export default function Bitacora() {
  const [params, setParams] = useSearchParams();
  const entidad = params.get("entidad") ?? "";
  const entidadId = params.get("entidad_id") ?? "";
  const accion = params.get("accion") ?? "";
  const usuarioId = params.get("usuario_id") ?? "";
  const desde = params.get("desde") ?? "";
  const hasta = params.get("hasta") ?? "";
  const page = Number(params.get("page") ?? 1);
  const [usuarios, setUsuarios] = useState([]);
  const [respuesta, setRespuesta] = useState({ clave: null });
  const [recarga, setRecarga] = useState(0);
  const [abierto, setAbierto] = useState(null);

  const rangoInvalido = Boolean(desde && hasta && hasta < desde);
  const clave = JSON.stringify({
    entidad,
    entidadId,
    accion,
    usuarioId,
    desde,
    hasta,
    page,
    recarga,
  });

  useEffect(() => {
    const controller = new AbortController();
    getUsuariosBitacora(controller.signal)
      .then(({ data }) =>
        setUsuarios(
          data.map((u) => ({ valor: u.usuario_id, etiqueta: u.nombre })),
        ),
      )
      .catch(() => {});
    return () => controller.abort();
  }, []);

  useEffect(() => {
    if (rangoInvalido) return undefined;
    const controller = new AbortController();
    getBitacora(
      {
        entidad,
        entidad_id: entidadId,
        accion,
        usuario_id: usuarioId,
        desde,
        hasta,
        page,
        limit: LIMIT,
      },
      controller.signal,
    )
      .then((resultado) => setRespuesta({ clave, resultado }))
      .catch((falla) => {
        if (falla.name !== "AbortError") setRespuesta({ clave, error: falla });
      });
    return () => controller.abort();
  }, [
    clave,
    entidad,
    entidadId,
    accion,
    usuarioId,
    desde,
    hasta,
    page,
    rangoInvalido,
  ]);

  const cambiar = (cambios) => {
    const siguientes = new URLSearchParams(window.location.search);
    Object.entries(cambios).forEach(([nombre, valor]) => {
      if (valor) siguientes.set(nombre, valor);
      else siguientes.delete(nombre);
    });
    if (!("page" in cambios)) siguientes.delete("page");
    setParams(siguientes, { replace: true });
  };

  const cargando = !rangoInvalido && respuesta.clave !== clave;
  const error = cargando || rangoInvalido ? null : respuesta.error;
  const registros = rangoInvalido ? [] : (respuesta.resultado?.data ?? []);
  const meta = respuesta.resultado?.meta;
  const filtrado = Boolean(
    entidad || entidadId || accion || usuarioId || desde || hasta,
  );

  return (
    <>
      <PageHeader
        titulo="Bitácora"
        subtitulo="Cada creación, edición y eliminación queda registrada con su usuario y los datos de antes y después."
      />

      <div className={styles.filtros}>
        <Selector
          label="Entidad"
          opciones={OPCIONES_ENTIDAD}
          opcionVacia="Todas las entidades"
          value={entidad}
          onChange={(valor) => cambiar({ entidad: valor, entidad_id: "" })}
        />
        <Selector
          label="Acción"
          opciones={OPCIONES_ACCION}
          opcionVacia="Todas las acciones"
          value={accion}
          onChange={(valor) => cambiar({ accion: valor })}
        />
        <Selector
          label="Usuario"
          opciones={usuarios}
          opcionVacia="Todos los usuarios"
          value={usuarioId}
          onChange={(valor) => cambiar({ usuario_id: valor })}
        />
        <Field
          label="Desde"
          type="date"
          value={desde}
          onChange={(event) => cambiar({ desde: event.target.value })}
        />
        <Field
          label="Hasta"
          type="date"
          value={hasta}
          onChange={(event) => cambiar({ hasta: event.target.value })}
          error={
            rangoInvalido
              ? "Debe ser igual o posterior a la fecha inicial."
              : undefined
          }
        />
      </div>

      {entidadId && (
        <Notice tono="info">
          Mostrando solo el historial del registro {idCorto(entidadId)}.{" "}
          <Button variant="enlace" onClick={() => cambiar({ entidad_id: "" })}>
            Ver todos
          </Button>
        </Notice>
      )}

      {error ? (
        <Notice
          tono="peligro"
          titulo="No pudimos cargar la bitácora"
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
      ) : !cargando && registros.length === 0 ? (
        <EmptyState
          titulo={
            filtrado
              ? "Ningún registro coincide con los filtros"
              : "La bitácora está vacía"
          }
          accion={
            filtrado && (
              <Button
                variant="secundario"
                onClick={() =>
                  cambiar({
                    entidad: "",
                    entidad_id: "",
                    accion: "",
                    usuario_id: "",
                    desde: "",
                    hasta: "",
                  })
                }
              >
                Quitar filtros
              </Button>
            )
          }
        >
          {filtrado
            ? "Prueba con otras fechas o con otra entidad."
            : "Aquí aparecerán los cambios hechos en el sistema."}
        </EmptyState>
      ) : (
        <div className={styles.envoltura} aria-busy={cargando}>
          <table className={styles.tabla}>
            <thead>
              <tr>
                <th scope="col">Fecha</th>
                <th scope="col">Usuario</th>
                <th scope="col">Acción</th>
                {!entidad && <th scope="col">Entidad</th>}
                <th scope="col">Registro</th>
                <th scope="col">Campos</th>
                <th scope="col">
                  <span className="visually-hidden">Acciones</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {registros.map((registro) => {
                const { tono, texto } = ACCIONES[registro.accion];
                const enlace =
                  registro.accion !== "eliminar"
                    ? enlaceDe(registro.entidad, registro.entidad_id)
                    : null;
                const extra = registro.campos.length - CAMPOS_VISIBLES;
                return (
                  <tr key={registro.auditoria_id}>
                    <th scope="row">{formatDateTime(registro.creado_en)}</th>
                    <td className={registro.usuario ? undefined : styles.tenue}>
                      {registro.usuario ?? "Sistema"}
                    </td>
                    <td>
                      <StatusTag tono={tono}>{texto}</StatusTag>
                    </td>
                    {!entidad && (
                      <td>{ENTIDADES[registro.entidad] ?? registro.entidad}</td>
                    )}
                    <td className={styles.mono}>
                      {enlace ? (
                        <Link to={enlace}>{idCorto(registro.entidad_id)}</Link>
                      ) : (
                        idCorto(registro.entidad_id)
                      )}{" "}
                      {registro.entidad_id && (
                        <Button
                          variant="enlace"
                          onClick={() =>
                            cambiar({
                              entidad: registro.entidad,
                              entidad_id: registro.entidad_id,
                            })
                          }
                        >
                          Historial
                        </Button>
                      )}
                    </td>
                    <td className={styles.tenue}>
                      {registro.accion === "crear"
                        ? "Registro nuevo"
                        : registro.accion === "eliminar"
                          ? "Registro eliminado"
                          : `${registro.campos.slice(0, CAMPOS_VISIBLES).join(", ")}${extra > 0 ? ` y ${extra} más` : ""}`}
                    </td>
                    <td>
                      <Button
                        variant="enlace"
                        onClick={() => setAbierto(registro)}
                      >
                        Ver cambios
                      </Button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      {!error && !rangoInvalido && (
        <Paginacion
          meta={meta}
          onCambiar={(pagina) => cambiar({ page: String(pagina) })}
        />
      )}

      {abierto && (
        <DetalleBitacora registro={abierto} onCerrar={() => setAbierto(null)} />
      )}
    </>
  );
}
