import { useEffect, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { Plus } from 'lucide-react';
import { clsx } from 'clsx';
import { getReportes } from '../api/endpoints';
import { useAuth } from '../auth/useAuth';
import { Paginacion } from '../components/Paginacion';
import { Button, EmptyState, Notice, PageHeader, StatusTag } from '../components/ui';
import { formatDateTime, formatNumber, formatOnlyDate } from '../utils/format';
import { ESTADOS_REPORTE, puedeGenerarReportes } from '../utils/reportes';
import styles from '../components/Listado.module.css';

const LIMIT = 10;
const ESPERA_CALCULO_MS = 3000;

const PESTANAS = [
  { valor: '', texto: 'Todos' },
  { valor: 'calculado', texto: 'Calculados' },
  { valor: 'en_auditoria', texto: 'En auditoría' },
  { valor: 'auditado', texto: 'Auditados' },
  { valor: 'fallido', texto: 'Fallidos' },
];

export default function Reportes() {
  const { usuario } = useAuth();
  const [params, setParams] = useSearchParams();
  const estado = params.get('estado') ?? '';
  const page = Number(params.get('page') ?? 1);
  const [respuesta, setRespuesta] = useState({ clave: null });
  const [recarga, setRecarga] = useState(0);
  const puedeGenerar = puedeGenerarReportes(usuario.rol);

  const clave = JSON.stringify({ estado, page, recarga });

  useEffect(() => {
    const controller = new AbortController();
    getReportes({ estado, page, limit: LIMIT }, controller.signal)
      .then((resultado) => setRespuesta({ clave, resultado }))
      .catch((falla) => {
        if (falla.name !== 'AbortError') setRespuesta({ clave, error: falla });
      });
    return () => controller.abort();
  }, [clave, estado, page]);

  const reportes = respuesta.resultado?.data ?? [];
  const hayCalculando = reportes.some((reporte) => reporte.estado === 'subido');

  useEffect(() => {
    if (!hayCalculando) return undefined;
    const temporizador = setTimeout(() => setRecarga((valor) => valor + 1), ESPERA_CALCULO_MS);
    return () => clearTimeout(temporizador);
  }, [hayCalculando, respuesta]);

  const cambiar = (cambios) =>
    setParams((actuales) => {
      const siguientes = new URLSearchParams(actuales);
      Object.entries(cambios).forEach(([nombre, valor]) => {
        if (valor) siguientes.set(nombre, valor);
        else siguientes.delete(nombre);
      });
      return siguientes;
    }, { replace: true });

  const cargando = respuesta.clave !== clave && !respuesta.resultado;
  const error = respuesta.clave === clave ? respuesta.error : null;

  return (
    <>
      <PageHeader titulo="Reportes" subtitulo="Reportes CSV de viajes con su huella de carbono calculada.">
        {puedeGenerar && (
          <Button to="/reportes/nuevo" icon={Plus}>
            Generar reporte
          </Button>
        )}
      </PageHeader>

      <div className={styles.pestanas} role="group" aria-label="Filtrar por estado">
        {PESTANAS.map(({ valor, texto }) => (
          <button
            key={texto}
            type="button"
            aria-pressed={estado === valor}
            className={clsx(styles.pestana, estado === valor && styles.pestanaActiva)}
            onClick={() => cambiar({ estado: valor, page: '' })}
          >
            {texto}
          </button>
        ))}
      </div>

      {error ? (
        <Notice tono="peligro" titulo="No pudimos cargar los reportes" role="alert">
          <p>{error.message}</p>
          <Button variant="enlacePeligro" onClick={() => setRecarga((valor) => valor + 1)}>
            Reintentar
          </Button>
        </Notice>
      ) : !cargando && reportes.length === 0 ? (
        <EmptyState
          titulo={estado ? 'No hay reportes en este estado' : 'Todavía no hay reportes'}
          accion={puedeGenerar && !estado && <Button to="/reportes/nuevo" icon={Plus}>Generar el primero</Button>}
        >
          Un reporte toma los viajes de un periodo, los guarda en un CSV y calcula su huella de carbono.
        </EmptyState>
      ) : (
        <div className={styles.envoltura} aria-busy={cargando}>
          <table className={styles.tabla}>
            <thead>
              <tr>
                <th scope="col">Periodo</th>
                <th scope="col">Estado</th>
                <th scope="col" className={styles.derecha}>Viajes</th>
                <th scope="col" className={styles.derecha}>Distancia (km)</th>
                <th scope="col" className={styles.derecha}>CO₂ (kg)</th>
                <th scope="col">Generado por</th>
                <th scope="col">Fecha</th>
                <th scope="col"><span className="visually-hidden">Acciones</span></th>
              </tr>
            </thead>
            <tbody>
              {reportes.map((reporte) => {
                const { tono, texto } = ESTADOS_REPORTE[reporte.estado] ?? ESTADOS_REPORTE.generado;
                return (
                  <tr key={reporte.reporte_viaje_id}>
                    <th scope="row">
                      {formatOnlyDate(reporte.periodo_desde)} – {formatOnlyDate(reporte.periodo_hasta)}
                    </th>
                    <td>
                      <StatusTag tono={tono}>{texto}</StatusTag>
                    </td>
                    <td className={styles.derecha}>{formatNumber(reporte.total_filas, 0)}</td>
                    <td className={styles.derecha}>
                      {reporte.distancia_total_km === undefined ? '—' : formatNumber(reporte.distancia_total_km)}
                    </td>
                    <td className={styles.derecha}>
                      {reporte.co2_total_kg === undefined ? '—' : formatNumber(reporte.co2_total_kg, 4)}
                    </td>
                    <td>{reporte.generado_por}</td>
                    <td className={styles.tenue}>{formatDateTime(reporte.generado_en)}</td>
                    <td>
                      <span className={styles.acciones}>
                        <Link to={`/reportes/${reporte.reporte_viaje_id}`}>Ver</Link>
                      </span>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      {!error && <Paginacion meta={respuesta.resultado?.meta} onCambiar={(pagina) => cambiar({ page: String(pagina) })} />}
    </>
  );
}
