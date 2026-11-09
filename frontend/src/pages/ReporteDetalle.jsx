import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { Download } from 'lucide-react';
import { clsx } from 'clsx';
import { toast } from 'sonner';
import { descargarCsvReporte, getFilasReporte, getReporte, reprocesarVersion } from '../api/endpoints';
import { useAuth } from '../auth/useAuth';
import { Paginacion } from '../components/Paginacion';
import { Button, EmptyState, Notice, PageHeader, StatusTag } from '../components/ui';
import { etiquetaDe, useCatalogos } from '../hooks/useCatalogos';
import { formatDateTime, formatFactor, formatNumber, formatOnlyDate } from '../utils/format';
import {
  ESTADOS_CALCULO,
  ESTADOS_REPORTE,
  calculoEnCurso,
  nombreDescarga,
  puedeGenerarReportes,
  versionVigente,
} from '../utils/reportes';
import listado from '../components/Listado.module.css';
import styles from '../components/Detalle.module.css';

const ESPERA_CALCULO_MS = 2000;
const LIMITE_FILAS = 20;

function FilasCalculo({ reporteId, version, catalogos }) {
  const [filtro, setFiltro] = useState('');
  const [page, setPage] = useState(1);
  const [respuesta, setRespuesta] = useState({ clave: null });
  const clave = JSON.stringify({ reporteId, version, filtro, page });

  useEffect(() => {
    const controller = new AbortController();
    getFilasReporte(reporteId, version, { estado: filtro, page, limit: LIMITE_FILAS }, controller.signal)
      .then((resultado) => setRespuesta({ clave, resultado }))
      .catch((falla) => {
        if (falla.name !== 'AbortError') setRespuesta({ clave, error: falla });
      });
    return () => controller.abort();
  }, [clave, reporteId, version, filtro, page]);

  const filas = respuesta.resultado?.data ?? [];
  const elegir = (valor) => {
    setFiltro(valor);
    setPage(1);
  };

  return (
    <section>
      <h2 className={styles.seccion}>Filas del cálculo · versión {version}</h2>
      <div className={listado.pestanas} role="group" aria-label="Filtrar filas">
        {[
          { valor: '', texto: 'Todas' },
          { valor: 'error', texto: 'Con error' },
        ].map(({ valor, texto }) => (
          <button
            key={texto}
            type="button"
            aria-pressed={filtro === valor}
            className={clsx(listado.pestana, filtro === valor && listado.pestanaActiva)}
            onClick={() => elegir(valor)}
          >
            {texto}
          </button>
        ))}
      </div>
      {respuesta.error ? (
        <Notice tono="peligro" role="alert">
          {respuesta.error.message}
        </Notice>
      ) : respuesta.resultado && filas.length === 0 ? (
        <EmptyState titulo={filtro ? 'Ninguna fila tiene error' : 'Sin filas'}>
          {filtro ? 'Todas las filas del CSV se calcularon correctamente.' : 'Esta versión no tiene filas calculadas.'}
        </EmptyState>
      ) : (
        <div className={listado.envoltura} aria-busy={respuesta.clave !== clave}>
          <table className={listado.tabla}>
            <thead>
              <tr>
                <th scope="col">Fila</th>
                <th scope="col">Placa</th>
                <th scope="col">Vehículo</th>
                <th scope="col">Salida</th>
                <th scope="col" className={listado.derecha}>Km</th>
                <th scope="col" className={listado.derecha}>Factor</th>
                <th scope="col" className={listado.derecha}>CO₂ (kg)</th>
                <th scope="col">Estado</th>
              </tr>
            </thead>
            <tbody>
              {filas.map((fila) => (
                <tr key={fila.fila_numero}>
                  <th scope="row">{fila.fila_numero}</th>
                  <td>{fila.vehiculo_placa ?? '—'}</td>
                  <td className={listado.tenue}>
                    {fila.tipo_vehiculo ? etiquetaDe(catalogos?.tipos_vehiculo, fila.tipo_vehiculo) : '—'} ·{' '}
                    {fila.tipo_combustible ? etiquetaDe(catalogos?.tipos_combustible, fila.tipo_combustible) : '—'}
                  </td>
                  <td className={listado.tenue}>{formatDateTime(fila.salida_en)}</td>
                  <td className={listado.derecha}>{fila.distancia_km === undefined ? '—' : formatNumber(fila.distancia_km)}</td>
                  <td className={listado.derecha}>{formatFactor(fila.factor_co2_km)}</td>
                  <td className={listado.derecha}>{fila.co2_kg === undefined ? '—' : formatNumber(fila.co2_kg, 4)}</td>
                  <td className={listado.ajustar}>
                    {fila.estado === 'ok' ? (
                      <StatusTag tono="exito">Correcta</StatusTag>
                    ) : (
                      <>
                        <StatusTag tono="peligro">Error</StatusTag>
                        <span className={styles.secundario}>{fila.mensaje_error}</span>
                      </>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      <Paginacion meta={respuesta.resultado?.meta} onCambiar={setPage} />
    </section>
  );
}

export default function ReporteDetalle() {
  const { id } = useParams();
  const { usuario } = useAuth();
  const { catalogos } = useCatalogos();
  const [respuesta, setRespuesta] = useState({ clave: null });
  const [recarga, setRecarga] = useState(0);
  const [ocupado, setOcupado] = useState(false);
  const puedeGenerar = puedeGenerarReportes(usuario.rol);
  const clave = `${id}-${recarga}`;

  useEffect(() => {
    const controller = new AbortController();
    getReporte(id, controller.signal)
      .then(({ data }) => setRespuesta({ clave, data }))
      .catch((falla) => {
        if (falla.name !== 'AbortError') setRespuesta({ clave, error: falla });
      });
    return () => controller.abort();
  }, [clave, id]);

  const reporte = respuesta.data;
  const enCurso = reporte?.versiones.some((version) => calculoEnCurso(version.estado_calculo));

  useEffect(() => {
    if (!enCurso) return undefined;
    const temporizador = setTimeout(() => setRecarga((valor) => valor + 1), ESPERA_CALCULO_MS);
    return () => clearTimeout(temporizador);
  }, [enCurso, respuesta]);

  const descargar = async (numero) => {
    try {
      await descargarCsvReporte(id, numero, nombreDescarga(reporte.nombre_archivo, numero));
    } catch (falla) {
      toast.error('No se pudo descargar el CSV', { description: falla.message });
    }
  };

  const reprocesar = async (numero) => {
    setOcupado(true);
    try {
      await reprocesarVersion(id, numero);
      toast.success('Cálculo reiniciado', { description: `La versión ${numero} se volverá a calcular.` });
      setRecarga((valor) => valor + 1);
    } catch (falla) {
      toast.error('No se pudo reprocesar', { description: falla.message });
    } finally {
      setOcupado(false);
    }
  };

  if (respuesta.error && !reporte) {
    const noExiste = respuesta.error.status === 404;
    return (
      <EmptyState
        titulo={noExiste ? 'Este reporte no existe' : 'No pudimos cargar el reporte'}
        accion={<Button variant="secundario" to="/reportes">Volver a reportes</Button>}
      >
        {noExiste ? 'Revisa la dirección o búscalo en la lista de reportes.' : respuesta.error.message}
      </EmptyState>
    );
  }

  if (!reporte) {
    return (
      <div className={styles.cargando} aria-busy="true">
        <span className="skeleton" style={{ height: 42, width: 260 }} />
        <span className="skeleton" style={{ height: 180 }} />
      </div>
    );
  }

  const vigente = reporte.versiones.length > 0 ? versionVigente(reporte.versiones) : null;
  const estado = ESTADOS_REPORTE[reporte.estado] ?? ESTADOS_REPORTE.generado;
  const periodo = `${formatOnlyDate(reporte.periodo_desde)} – ${formatOnlyDate(reporte.periodo_hasta)}`;
  const { filtros } = reporte;

  return (
    <>
      <PageHeader
        titulo="Reporte de viajes"
        subtitulo={`Periodo ${periodo}`}
        migas={[{ texto: 'Reportes', to: '/reportes' }, { texto: periodo }]}
      >
        <StatusTag tono={estado.tono}>{estado.texto}</StatusTag>
        {vigente && (
          <Button variant="secundario" icon={Download} onClick={() => descargar(vigente.numero_version)}>
            Descargar CSV
          </Button>
        )}
      </PageHeader>

      {reporte.mensaje_error && (
        <Notice tono="peligro" titulo="El reporte no se pudo guardar" role="alert">
          {reporte.mensaje_error}
        </Notice>
      )}

      <div className={styles.cuerpo}>
        <div className={styles.columna}>
          <section>
            <h2 className={styles.seccion}>Huella de carbono</h2>
            {!vigente ? (
              <p className={styles.nota}>Este reporte no tiene versiones.</p>
            ) : calculoEnCurso(vigente.estado_calculo) ? (
              <Notice tono="info" titulo="Calculando la huella" role="status">
                La página se actualiza sola cuando termine.
              </Notice>
            ) : vigente.estado_calculo === 'fallido' ? (
              <Notice tono="peligro" titulo="El cálculo falló" role="alert">
                <p>{vigente.mensaje_error}</p>
                {puedeGenerar && (
                  <Button variant="enlacePeligro" disabled={ocupado} onClick={() => reprocesar(vigente.numero_version)}>
                    Reprocesar
                  </Button>
                )}
              </Notice>
            ) : (
              <div className={styles.cifras}>
                <div>
                  <p className={styles.cifra}>{formatNumber(vigente.huella.co2_total_kg, 4)}</p>
                  <p className={styles.nota}>kg de CO₂ ({formatNumber(vigente.huella.co2_total_toneladas, 4)} t)</p>
                </div>
                <div>
                  <p className={styles.cifra}>{formatNumber(vigente.huella.distancia_total_km)}</p>
                  <p className={styles.nota}>km recorridos</p>
                </div>
                <div>
                  <p className={styles.cifra}>
                    {formatNumber(vigente.huella.filas_validas, 0)} / {formatNumber(vigente.huella.filas_procesadas, 0)}
                  </p>
                  <p className={styles.nota}>filas calculadas, {formatNumber(vigente.huella.filas_con_error, 0)} con error</p>
                </div>
              </div>
            )}
          </section>

          <section>
            <h2 className={styles.seccion}>Versiones</h2>
            <div className={listado.envoltura}>
              <table className={listado.tabla}>
                <thead>
                  <tr>
                    <th scope="col">Versión</th>
                    <th scope="col">Cálculo</th>
                    <th scope="col" className={listado.derecha}>Filas</th>
                    <th scope="col" className={listado.derecha}>CO₂ (kg)</th>
                    <th scope="col">Hash SHA-256</th>
                    <th scope="col"><span className="visually-hidden">Acciones</span></th>
                  </tr>
                </thead>
                <tbody>
                  {reporte.versiones.map((version) => {
                    const calculo = ESTADOS_CALCULO[version.estado_calculo] ?? ESTADOS_CALCULO.pendiente;
                    return (
                      <tr key={version.numero_version}>
                        <th scope="row">
                          v{version.numero_version}{' '}
                          {version.es_oficial && <StatusTag tono="exito">Oficial</StatusTag>}
                          <span className={styles.secundario}>
                            {version.origen === 'original' ? 'Original' : 'Corregida'} · {formatDateTime(version.creado_en)}
                          </span>
                        </th>
                        <td>
                          <StatusTag tono={calculo.tono}>{calculo.texto}</StatusTag>
                        </td>
                        <td className={listado.derecha}>{formatNumber(version.total_filas, 0)}</td>
                        <td className={listado.derecha}>{version.huella ? formatNumber(version.huella.co2_total_kg, 4) : '—'}</td>
                        <td className={listado.mono} title={version.hash_sha256}>
                          {version.hash_sha256.slice(0, 16)}…
                        </td>
                        <td>
                          <span className={listado.acciones}>
                            <Button variant="enlace" onClick={() => descargar(version.numero_version)}>
                              Descargar
                            </Button>
                            {puedeGenerar && version.estado_calculo === 'fallido' && (
                              <Button variant="enlace" disabled={ocupado} onClick={() => reprocesar(version.numero_version)}>
                                Reprocesar
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
          </section>

          {vigente?.estado_calculo === 'completado' && (
            <FilasCalculo reporteId={id} version={vigente.numero_version} catalogos={catalogos} />
          )}
        </div>

        <aside className={styles.panel}>
          <h2 className={styles.tituloPanel}>Datos del reporte</h2>
          <dl className={styles.lista}>
            <div>
              <dt>Periodo</dt>
              <dd>{periodo}</dd>
            </div>
            <div>
              <dt>Viajes</dt>
              <dd>{formatNumber(reporte.total_filas, 0)}</dd>
            </div>
            <div>
              <dt>Tipo de vehículo</dt>
              <dd>{filtros.tipo_vehiculo ? etiquetaDe(catalogos?.tipos_vehiculo, filtros.tipo_vehiculo) : 'Todos'}</dd>
            </div>
            <div>
              <dt>Combustible</dt>
              <dd>{filtros.tipo_combustible ? etiquetaDe(catalogos?.tipos_combustible, filtros.tipo_combustible) : 'Todos'}</dd>
            </div>
            <div>
              <dt>Estados de viaje</dt>
              <dd>{filtros.solo_completados ? 'Solo completados' : 'Todos menos cancelados'}</dd>
            </div>
            <div>
              <dt>Generado por</dt>
              <dd>{reporte.generado_por}</dd>
            </div>
            <div>
              <dt>Fecha</dt>
              <dd>{formatDateTime(reporte.generado_en)}</dd>
            </div>
            <div>
              <dt>Archivo</dt>
              <dd>{reporte.nombre_archivo}</dd>
            </div>
          </dl>
        </aside>
      </div>
    </>
  );
}
