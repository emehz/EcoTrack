import { useEffect, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { clsx } from 'clsx';
import { toast } from 'sonner';
import { getAuditorias, getReportes, iniciarAuditoria } from '../api/endpoints';
import { AreaTexto, DialogoFormulario } from '../components/DialogoFormulario';
import { Paginacion } from '../components/Paginacion';
import { Button, EmptyState, Notice, PageHeader, StatusTag } from '../components/ui';
import { formatDateTime, formatNumber, formatOnlyDate } from '../utils/format';
import { ESTADOS_AUDITORIA, RESULTADOS } from '../utils/auditorias';
import styles from '../components/Listado.module.css';

const LIMIT = 10;

const PESTANAS = [
  { valor: 'por_auditar', texto: 'Por auditar' },
  { valor: 'en_revision', texto: 'En revisión' },
  { valor: 'cerrada', texto: 'Cerradas' },
];

function TablaPorAuditar({ reportes, onIniciar }) {
  return (
    <table className={styles.tabla}>
      <thead>
        <tr>
          <th scope="col">Periodo</th>
          <th scope="col" className={styles.derecha}>Viajes</th>
          <th scope="col" className={styles.derecha}>CO₂ (kg)</th>
          <th scope="col">Generado por</th>
          <th scope="col">Fecha</th>
          <th scope="col"><span className="visually-hidden">Acciones</span></th>
        </tr>
      </thead>
      <tbody>
        {reportes.map((reporte) => (
          <tr key={reporte.reporte_viaje_id}>
            <th scope="row">
              <Link to={`/reportes/${reporte.reporte_viaje_id}`}>
                {formatOnlyDate(reporte.periodo_desde)} – {formatOnlyDate(reporte.periodo_hasta)}
              </Link>
            </th>
            <td className={styles.derecha}>{formatNumber(reporte.total_filas, 0)}</td>
            <td className={styles.derecha}>{reporte.co2_total_kg === undefined ? '—' : formatNumber(reporte.co2_total_kg, 4)}</td>
            <td>{reporte.generado_por}</td>
            <td className={styles.tenue}>{formatDateTime(reporte.generado_en)}</td>
            <td>
              <Button variant="enlace" onClick={() => onIniciar(reporte)}>
                Iniciar auditoría
              </Button>
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

function TablaAuditorias({ auditorias }) {
  return (
    <table className={styles.tabla}>
      <thead>
        <tr>
          <th scope="col">Reporte</th>
          <th scope="col">Estado</th>
          <th scope="col" className={styles.derecha}>Hallazgos abiertos</th>
          <th scope="col">Auditor</th>
          <th scope="col">Iniciada</th>
          <th scope="col">Cerrada</th>
          <th scope="col"><span className="visually-hidden">Acciones</span></th>
        </tr>
      </thead>
      <tbody>
        {auditorias.map((auditoria) => {
          const estado = (auditoria.resultado ? RESULTADOS[auditoria.resultado] : ESTADOS_AUDITORIA[auditoria.estado])
            || { tono: 'neutro', texto: auditoria.resultado || auditoria.estado || 'Desconocido' };
          return (
            <tr key={auditoria.auditoria_reporte_id}>
              <th scope="row">
                {auditoria.nombre_archivo}
                <span className={styles.tenue}> · v{auditoria.version_auditada}</span>
              </th>
              <td>
                <StatusTag tono={estado.tono}>{estado.texto}</StatusTag>
              </td>
              <td className={styles.derecha}>
                {formatNumber(auditoria.hallazgos_abiertos, 0)}
                {auditoria.hallazgos_graves_abiertos > 0 && (
                  <>
                    {' '}
                    <StatusTag tono="peligro">{auditoria.hallazgos_graves_abiertos} graves</StatusTag>
                  </>
                )}
              </td>
              <td>{auditoria.auditor}</td>
              <td className={styles.tenue}>{formatDateTime(auditoria.iniciado_en)}</td>
              <td className={styles.tenue}>{formatDateTime(auditoria.cerrado_en)}</td>
              <td>
                <span className={styles.acciones}>
                  <Link to={`/auditorias/${auditoria.auditoria_reporte_id}`}>Ver</Link>
                </span>
              </td>
            </tr>
          );
        })}
      </tbody>
    </table>
  );
}

export default function Auditorias() {
  const navigate = useNavigate();
  const [params, setParams] = useSearchParams();
  const vista = params.get('vista') ?? 'por_auditar';
  const page = Number(params.get('page') ?? 1);
  const [respuesta, setRespuesta] = useState({ clave: null });
  const [recarga, setRecarga] = useState(0);
  const [aIniciar, setAIniciar] = useState(null);
  const [alcance, setAlcance] = useState('');
  const [ocupado, setOcupado] = useState(false);
  const [errorDialogo, setErrorDialogo] = useState('');

  const clave = JSON.stringify({ vista, page, recarga });

  useEffect(() => {
    const controller = new AbortController();
    const consulta =
      vista === 'por_auditar'
        ? getReportes({ estado: 'calculado', page, limit: LIMIT }, controller.signal)
        : getAuditorias({ estado: vista, page, limit: LIMIT }, controller.signal);
    consulta
      .then((resultado) => setRespuesta({ clave, resultado }))
      .catch((falla) => {
        if (falla.name !== 'AbortError') setRespuesta({ clave, error: falla });
      });
    return () => controller.abort();
  }, [clave, vista, page]);

  const cambiar = (cambios) =>
    setParams((actuales) => {
      const siguientes = new URLSearchParams(actuales);
      Object.entries(cambios).forEach(([nombre, valor]) => {
        if (valor) siguientes.set(nombre, valor);
        else siguientes.delete(nombre);
      });
      return siguientes;
    }, { replace: true });

  const abrirInicio = (reporte) => {
    setAIniciar(reporte);
    setAlcance('');
    setErrorDialogo('');
  };

  const iniciar = async () => {
    setOcupado(true);
    try {
      const { data } = await iniciarAuditoria(aIniciar.reporte_viaje_id, { alcance: alcance.trim() || null });
      const cantidad = data.hallazgos.length;
      toast.success('Auditoría iniciada', {
        description:
          cantidad === 0
            ? 'Los controles automáticos no encontraron diferencias.'
            : `Los controles automáticos encontraron ${cantidad} ${cantidad === 1 ? 'hallazgo' : 'hallazgos'}.`,
      });
      navigate(`/auditorias/${data.auditoria_reporte_id}`);
    } catch (falla) {
      setErrorDialogo(falla.message);
      setOcupado(false);
    }
  };

  const datos = respuesta.resultado?.data ?? [];
  const cargando = respuesta.clave !== clave;
  const error = cargando ? null : respuesta.error;
  const vacio = {
    por_auditar: ['No hay reportes por auditar', 'Aquí aparecen los reportes con la huella calculada que aún no se auditan.'],
    en_revision: ['No hay auditorías en revisión', 'Las auditorías que inicies aparecerán aquí hasta que las cierres.'],
    cerrada: ['Todavía no hay auditorías cerradas', 'Al cerrar una auditoría queda aquí con su resultado.'],
  }[vista];

  return (
    <>
      <PageHeader titulo="Auditorías" subtitulo="Revisión de los reportes de viajes y su huella de carbono." />

      <div className={styles.pestanas} role="group" aria-label="Vista">
        {PESTANAS.map(({ valor, texto }) => (
          <button
            key={valor}
            type="button"
            aria-pressed={vista === valor}
            className={clsx(styles.pestana, vista === valor && styles.pestanaActiva)}
            onClick={() => cambiar({ vista: valor === 'por_auditar' ? '' : valor, page: '' })}
          >
            {texto}
          </button>
        ))}
      </div>

      {error ? (
        <Notice tono="peligro" titulo="No pudimos cargar la información" role="alert">
          <p>{error.message}</p>
          <Button variant="enlacePeligro" onClick={() => setRecarga((valor) => valor + 1)}>
            Reintentar
          </Button>
        </Notice>
      ) : !cargando && datos.length === 0 ? (
        <EmptyState titulo={vacio[0]}>{vacio[1]}</EmptyState>
      ) : (
        <div className={styles.envoltura} aria-busy={cargando}>
          {vista === 'por_auditar' ? (
            <TablaPorAuditar reportes={datos} onIniciar={abrirInicio} />
          ) : (
            <TablaAuditorias auditorias={datos} />
          )}
        </div>
      )}

      {!error && <Paginacion meta={respuesta.resultado?.meta} onCambiar={(pagina) => cambiar({ page: String(pagina) })} />}

      <DialogoFormulario
        abierto={Boolean(aIniciar)}
        titulo="Iniciar auditoría"
        descripcion="Se revisará la última versión calculada. Al iniciar se ejecutan los controles automáticos."
        accion="Iniciar auditoría"
        ocupado={ocupado}
        error={errorDialogo}
        onEnviar={iniciar}
        onCerrar={() => setAIniciar(null)}
      >
        <AreaTexto
          label="Alcance (opcional)"
          value={alcance}
          onChange={(event) => setAlcance(event.target.value)}
          placeholder="Qué vas a revisar"
        />
      </DialogoFormulario>
    </>
  );
}
