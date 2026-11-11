import { useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { toast } from 'sonner';
import { cambiarEstadoViaje, deleteViaje, getViaje } from '../api/endpoints';
import { useAuth } from '../auth/useAuth';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { RegistrarLlegada } from '../components/RegistrarLlegada';
import { Button, EmptyState, PageHeader, StatusTag } from '../components/ui';
import { etiquetaDe, useCatalogos } from '../hooks/useCatalogos';
import { formatDateTime, formatFactor, formatNumber, formatOnlyDate } from '../utils/format';
import { nombreCompleto } from '../utils/choferes';
import { ESTADOS_VIAJE, puedeRegistrarViajes } from '../utils/viajes';
import styles from '../components/Detalle.module.css';

const CONFIRMACIONES = {
  cancelar: {
    titulo: 'Cancelar viaje',
    accion: 'Cancelar viaje',
    texto: 'El viaje deja de contar en los cálculos de emisiones y su hora de salida queda libre para otro registro.',
  },
  reabrir: {
    titulo: 'Reabrir viaje',
    accion: 'Reabrir',
    texto: 'El viaje vuelve a quedar en curso y se borra la hora de llegada registrada.',
  },
  eliminar: {
    titulo: 'Eliminar viaje',
    accion: 'Eliminar definitivamente',
    texto: 'Úsalo solo para viajes duplicados. El viaje se borra y no se puede recuperar; la bitácora conserva sus datos.',
  },
};

export default function ViajeDetalle() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { usuario } = useAuth();
  const { catalogos } = useCatalogos();
  const esAdmin = usuario?.rol === 'admin';
  const puedeRegistrar = puedeRegistrarViajes(usuario?.rol);
  const [respuesta, setRespuesta] = useState({ clave: null });
  const [recarga, setRecarga] = useState(0);
  const [llegando, setLlegando] = useState(false);
  const [confirmar, setConfirmar] = useState(null);
  const [ocupado, setOcupado] = useState(false);
  const clave = `${id}-${recarga}`;

  useEffect(() => {
    const controller = new AbortController();
    getViaje(id, controller.signal)
      .then(({ data }) => setRespuesta({ clave, data }))
      .catch((falla) => {
        if (falla.name !== 'AbortError') setRespuesta({ clave, error: falla });
      });
    return () => controller.abort();
  }, [clave, id]);

  const detalle = respuesta.data;

  if (respuesta.error && !detalle) {
    const noExiste = respuesta.error.status === 404;
    return (
      <EmptyState
        titulo={noExiste ? 'Este viaje no existe' : 'No pudimos cargar el viaje'}
        accion={<Button variant="secundario" to="/viajes">Volver a viajes</Button>}
      >
        {noExiste ? 'Puede que lo hayan eliminado. Búscalo en la lista de viajes.' : respuesta.error.message}
      </EmptyState>
    );
  }

  if (!detalle) {
    return (
      <div className={styles.cargando} aria-busy="true">
        <span className="skeleton" style={{ height: 42, width: 260 }} />
        <span className="skeleton" style={{ height: 180 }} />
      </div>
    );
  }

  const { viaje, vehiculo, chofer, factor, carga } = detalle;
  const estado = ESTADOS_VIAJE[viaje.estado];
  const recargar = () => setRecarga((valor) => valor + 1);
  const abierto = viaje.estado === 'planificado' || viaje.estado === 'en_curso';

  const cambiarEstado = async (nuevo, mensaje) => {
    setOcupado(true);
    try {
      await cambiarEstadoViaje(id, { estado: nuevo });
      toast.success(mensaje);
      setConfirmar(null);
      recargar();
    } catch (falla) {
      toast.error('No se pudo cambiar el estado', { description: falla.message });
    } finally {
      setOcupado(false);
    }
  };

  const eliminar = async () => {
    setOcupado(true);
    try {
      await deleteViaje(id);
      toast.success('Viaje eliminado');
      navigate('/viajes', { replace: true });
    } catch (falla) {
      toast.error('No se pudo eliminar', { description: falla.message });
      setOcupado(false);
    }
  };

  const confirmarAccion = () => {
    if (confirmar === 'cancelar') return cambiarEstado('cancelado', 'Viaje cancelado');
    if (confirmar === 'reabrir') return cambiarEstado('en_curso', 'Viaje reabierto');
    return eliminar();
  };

  const titulo = `${viaje.origen} › ${viaje.destino}`;

  return (
    <>
      <PageHeader titulo={titulo} migas={[{ texto: 'Viajes', to: '/viajes' }, { texto: titulo }]}>
        <StatusTag tono={estado.tono}>{estado.texto}</StatusTag>
        {puedeRegistrar && viaje.estado === 'planificado' && (
          <Button onClick={() => cambiarEstado('en_curso', 'Viaje iniciado')} disabled={ocupado}>
            Iniciar viaje
          </Button>
        )}
        {puedeRegistrar && viaje.estado === 'en_curso' && <Button onClick={() => setLlegando(true)}>Registrar llegada</Button>}
        {puedeRegistrar && viaje.estado !== 'cancelado' && (
          <Button variant="secundario" to={`/viajes/${id}/editar`}>
            Editar
          </Button>
        )}
        {puedeRegistrar && abierto && (
          <Button variant="secundario" onClick={() => setConfirmar('cancelar')}>
            Cancelar viaje
          </Button>
        )}
        {esAdmin && viaje.estado === 'completado' && (
          <Button variant="secundario" onClick={() => setConfirmar('reabrir')}>
            Reabrir
          </Button>
        )}
        {esAdmin && (
          <Button variant="secundario" onClick={() => setConfirmar('eliminar')}>
            Eliminar
          </Button>
        )}
      </PageHeader>

      <div className={styles.cuerpo}>
        <div className={styles.columna}>
          <section>
            <h2 className={styles.seccion}>Emisiones del viaje</h2>
            <div className={styles.cifras}>
              <div>
                <p className={styles.cifra}>{formatNumber(viaje.distancia_km)}</p>
                <p className={styles.nota}>km recorridos</p>
              </div>
              <div>
                <p className={styles.cifra}>{formatNumber(viaje.co2_kg)}</p>
                <p className={styles.nota}>kg de CO₂</p>
              </div>
              <div>
                <p className={styles.cifra}>{formatFactor(factor.factor_co2_km)}</p>
                <p className={styles.nota}>kg de CO₂ por km</p>
              </div>
            </div>
            <p className={styles.nota}>
              CO₂ = {formatNumber(viaje.distancia_km)} km × {formatFactor(factor.factor_co2_km)} kg/km. Factor vigente del{' '}
              {formatOnlyDate(factor.vigente_desde)}
              {factor.vigente_hasta ? ` al ${formatOnlyDate(factor.vigente_hasta)}` : ' en adelante'}
              {factor.fuente ? ` · Fuente: ${factor.fuente}` : ''}.
            </p>
            {viaje.estado === 'cancelado' && (
              <p className={styles.nota}>Este viaje está cancelado y no cuenta en los cálculos de emisiones.</p>
            )}
          </section>
        </div>

        <aside className={styles.panel}>
          <h2 className={styles.tituloPanel}>Datos del viaje</h2>
          <dl className={styles.lista}>
            <div>
              <dt>Vehículo</dt>
              <dd>
                <Link to={`/vehiculos/${vehiculo.vehiculo_id}`}>{vehiculo.placa}</Link>
                <span className={styles.secundario}>
                  {etiquetaDe(catalogos?.tipos_vehiculo, vehiculo.tipo_vehiculo)} ·{' '}
                  {etiquetaDe(catalogos?.tipos_combustible, vehiculo.tipo_combustible)}
                </span>
              </dd>
            </div>
            <div>
              <dt>Chofer</dt>
              <dd>
                {chofer ? (
                  <Link to={`/choferes/${chofer.chofer_id}`}>{nombreCompleto(chofer)}</Link>
                ) : (
                  'Sin chofer'
                )}
              </dd>
            </div>
            <div>
              <dt>Salida</dt>
              <dd>{formatDateTime(viaje.salida_en)}</dd>
            </div>
            <div>
              <dt>Llegada</dt>
              <dd>{viaje.llegada_en ? formatDateTime(viaje.llegada_en) : '—'}</dd>
            </div>
            <div>
              <dt>Registrado por</dt>
              <dd>
                {detalle.creado_por ?? '—'}
                <span className={styles.secundario}>{formatDateTime(detalle.creado_en)}</span>
              </dd>
            </div>
            {carga && (
              <div>
                <dt>Carga de origen</dt>
                <dd>{carga.nombre_archivo ?? carga.carga_archivo_id}</dd>
              </div>
            )}
          </dl>
        </aside>
      </div>

      {llegando && (
        <RegistrarLlegada
          viaje={viaje}
          onCerrar={() => setLlegando(false)}
          onRegistrada={() => {
            setLlegando(false);
            recargar();
          }}
        />
      )}

      {confirmar && (
        <ConfirmDialog
          abierto
          titulo={CONFIRMACIONES[confirmar].titulo}
          accion={CONFIRMACIONES[confirmar].accion}
          ocupado={ocupado}
          onConfirmar={confirmarAccion}
          onCerrar={() => setConfirmar(null)}
        >
          <p>{CONFIRMACIONES[confirmar].texto}</p>
        </ConfirmDialog>
      )}
    </>
  );
}
