import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { toast } from 'sonner';
import { darDeBajaVehiculo, getVehiculo } from '../api/endpoints';
import { useAuth } from '../auth/useAuth';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { Button, EmptyState, EstadoVehiculo, PageHeader } from '../components/ui';
import { etiquetaDe, useCatalogos } from '../hooks/useCatalogos';
import { formatDate, formatFactor, formatNumber } from '../utils/format';
import { litrosAGalones } from '../utils/unidades';
import styles from './VehiculoDetalle.module.css';

const KM_EJEMPLO = 100;

function Dato({ etiqueta, children, nota }) {
  return (
    <div className={styles.dato}>
      <dt>{etiqueta}</dt>
      <dd>{children}</dd>
      {nota && <dd className={styles.notaDato}>{nota}</dd>}
    </div>
  );
}

export default function VehiculoDetalle() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { puedeEscribir } = useAuth();
  const { catalogos } = useCatalogos();
  const [detalle, setDetalle] = useState(null);
  const [error, setError] = useState(null);
  const [confirmando, setConfirmando] = useState(false);
  const [ocupado, setOcupado] = useState(false);

  useEffect(() => {
    const controller = new AbortController();
    getVehiculo(id, controller.signal)
      .then(({ data }) => setDetalle(data))
      .catch((falla) => {
        if (falla.name !== 'AbortError') setError(falla);
      });
    return () => controller.abort();
  }, [id]);

  const confirmarBaja = async () => {
    setOcupado(true);
    try {
      await darDeBajaVehiculo(id);
      toast.success('Vehículo dado de baja', { description: `${detalle.vehiculo.placa} pasó a Inactivos.` });
      navigate('/vehiculos');
    } catch (falla) {
      toast.error('No se pudo dar de baja', { description: falla.message });
      setOcupado(false);
    }
  };

  if (error) {
    return (
      <EmptyState
        titulo={error.status === 404 ? 'Este vehículo no existe' : 'No pudimos cargar el vehículo'}
        accion={<Button variant="secundario" to="/vehiculos">Volver a vehículos</Button>}
      >
        {error.status === 404 ? 'Revisa la dirección o búscalo en la lista de vehículos.' : error.message}
      </EmptyState>
    );
  }

  if (!detalle) {
    return (
      <div className={styles.cargando} aria-busy="true">
        <span className="skeleton" style={{ height: 42, width: 220 }} />
        <span className="skeleton" style={{ height: 180 }} />
      </div>
    );
  }

  const { vehiculo } = detalle;
  const tipo = etiquetaDe(catalogos?.tipos_vehiculo, vehiculo.tipo_vehiculo);
  const combustible = etiquetaDe(catalogos?.tipos_combustible, vehiculo.tipo_combustible);
  const factor = vehiculo.factor_co2_km;

  return (
    <>
      <PageHeader titulo={vehiculo.placa} migas={[{ texto: 'Vehículos', to: '/vehiculos' }, { texto: vehiculo.placa }]}>
        <EstadoVehiculo estado={vehiculo.estado} />
        {puedeEscribir && (
          <>
            <Button variant="secundario" to={`/vehiculos/${id}/editar`}>
              Editar
            </Button>
            {vehiculo.estado !== 'inactivo' && (
              <Button variant="secundario" onClick={() => setConfirmando(true)}>
                Dar de baja
              </Button>
            )}
          </>
        )}
      </PageHeader>

      <div className={styles.cuerpo}>
        <div className={styles.columna}>
          <section>
            <h2 className={styles.seccion}>Datos del vehículo</h2>
            <dl className={styles.ficha}>
              <Dato etiqueta="Tipo">{tipo}</Dato>
              <Dato etiqueta="Combustible">{combustible}</Dato>
              <Dato etiqueta="Registrado el">{formatDate(vehiculo.creado_en)}</Dato>
              <Dato etiqueta="Último viaje">
                {vehiculo.ultimo_viaje_en ? formatDate(vehiculo.ultimo_viaje_en) : 'Sin viajes'}
              </Dato>
            </dl>
          </section>

          <section>
            <h2 className={styles.seccion}>Lo que lleva registrado</h2>
            <dl className={styles.ficha}>
              <Dato etiqueta="Viajes completados">{formatNumber(detalle.viajes_completados, 0)}</Dato>
              <Dato etiqueta="Distancia recorrida">{formatNumber(detalle.km_recorridos)} km</Dato>
              <Dato etiqueta="CO₂ emitido" nota="Con el factor vigente en cada viaje">
                {formatNumber(detalle.co2_acumulado_kg)} kg
              </Dato>
              <Dato
                etiqueta="Última carga de combustible"
                nota={detalle.ultima_carga_en ? formatDate(detalle.ultima_carga_en) : undefined}
              >
                {detalle.ultima_carga_litros ? `${formatNumber(litrosAGalones(detalle.ultima_carga_litros))} gal` : 'Sin cargas'}
              </Dato>
            </dl>
          </section>
        </div>

        <aside className={styles.factor}>
          <h2 className={styles.tituloPanel}>Cómo se calcula su CO₂</h2>
          <p className={styles.cifra}>{formatFactor(factor)}</p>
          <p className={styles.nota}>
            kg de CO₂ por km · {tipo} con {combustible}
          </p>
          {factor !== undefined && factor !== null && (
            <p className={styles.formula}>
              Un viaje de {KM_EJEMPLO} km: {KM_EJEMPLO} × {formatFactor(factor)} = {formatNumber(KM_EJEMPLO * factor)} kg
            </p>
          )}
        </aside>
      </div>

      <ConfirmDialog
        abierto={confirmando}
        titulo={`¿Dar de baja ${vehiculo.placa}?`}
        accion="Dar de baja"
        ocupado={ocupado}
        onConfirmar={confirmarBaja}
        onCerrar={() => setConfirmando(false)}
      >
        Quedará inactivo y no podrá recibir viajes ni cargas de combustible. Su historial se conserva.
      </ConfirmDialog>
    </>
  );
}
