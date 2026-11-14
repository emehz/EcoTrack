import { useEffect, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { Plus } from 'lucide-react';
import { clsx } from 'clsx';
import { toast } from 'sonner';
import { deleteCarga, getCargas, getConsumo, getVehiculos } from '../api/endpoints';
import { useAuth } from '../auth/useAuth';
import { CargaDialog } from '../components/CargaDialog';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { Paginacion } from '../components/Paginacion';
import { Selector } from '../components/Selector';
import { Button, EmptyState, Field, Notice, PageHeader } from '../components/ui';
import { etiquetaDe, useCatalogos } from '../hooks/useCatalogos';
import { formatDateTime, formatNumber } from '../utils/format';
import { desdeLitros, formatoDinero, kmPorGalon, precioEnUnidad, puedeRegistrarCargas } from '../utils/combustible';
import styles from '../components/Listado.module.css';

const LIMIT = 15;

const PESTANAS = [
  { valor: 'cargas', texto: 'Cargas' },
  { valor: 'consumo', texto: 'Consumo por vehículo' },
];

const galones = (litros) => formatNumber(desdeLitros(litros, 'galones'));

export default function Combustible() {
  const { usuario } = useAuth();
  const { catalogos } = useCatalogos();
  const puedeRegistrar = puedeRegistrarCargas(usuario?.rol);
  const esAdmin = usuario?.rol === 'admin';
  const [params, setParams] = useSearchParams();
  const vista = params.get('vista') === 'consumo' ? 'consumo' : 'cargas';
  const vehiculo = params.get('vehiculo_id') ?? '';
  const desde = params.get('desde') ?? '';
  const hasta = params.get('hasta') ?? '';
  const page = Number(params.get('page') ?? 1);
  const [vehiculos, setVehiculos] = useState([]);
  const [respuesta, setRespuesta] = useState({ clave: null });
  const [recarga, setRecarga] = useState(0);
  const [editando, setEditando] = useState(null);
  const [aEliminar, setAEliminar] = useState(null);
  const [eliminando, setEliminando] = useState(false);

  const rangoInvalido = Boolean(desde && hasta && hasta < desde);
  const clave = JSON.stringify({ vista, vehiculo, desde, hasta, page, recarga });

  useEffect(() => {
    const controller = new AbortController();
    getVehiculos({ estado: 'todos', limit: 100, sort: 'placa' }, controller.signal)
      .then(({ data }) => setVehiculos(data))
      .catch(() => {});
    return () => controller.abort();
  }, []);

  useEffect(() => {
    if (rangoInvalido) return undefined;
    const controller = new AbortController();
    const filtros = { vehiculo_id: vehiculo, desde, hasta };
    const peticion =
      vista === 'consumo'
        ? getConsumo(filtros, controller.signal)
        : getCargas({ ...filtros, page, limit: LIMIT }, controller.signal);
    peticion
      .then((resultado) => setRespuesta({ clave, vista, resultado }))
      .catch((falla) => {
        if (falla.name !== 'AbortError') setRespuesta({ clave, vista, error: falla });
      });
    return () => controller.abort();
  }, [clave, vista, vehiculo, desde, hasta, page, rangoInvalido]);

  const cambiar = (cambios) => {
    const siguientes = new URLSearchParams(window.location.search);
    Object.entries(cambios).forEach(([nombre, valor]) => {
      if (valor) siguientes.set(nombre, valor);
      else siguientes.delete(nombre);
    });
    if (!('page' in cambios)) siguientes.delete('page');
    setParams(siguientes, { replace: true });
  };

  const eliminar = async () => {
    setEliminando(true);
    try {
      await deleteCarga(aEliminar.carga_combustible_id);
      toast.success('Carga eliminada');
      setAEliminar(null);
      setRecarga((valor) => valor + 1);
    } catch (falla) {
      toast.error('No se pudo eliminar', { description: falla.message });
    } finally {
      setEliminando(false);
    }
  };

  const cargando = !rangoInvalido && respuesta.clave !== clave;
  const error = cargando || rangoInvalido ? null : respuesta.error;
  const mismaVista = respuesta.vista === vista;
  const filas = rangoInvalido || !mismaVista ? [] : (respuesta.resultado?.data ?? []);
  const meta = mismaVista ? respuesta.resultado?.meta : undefined;
  const opcionesVehiculo = vehiculos
    .filter((v) => v.tipo_combustible !== 'electrico')
    .map((v) => ({ valor: v.vehiculo_id, etiqueta: v.placa }));
  const filtrado = Boolean(vehiculo || desde || hasta);

  return (
    <>
      <PageHeader titulo="Combustible">
        {puedeRegistrar && (
          <Button icon={Plus} onClick={() => setEditando('nueva')}>
            Registrar carga
          </Button>
        )}
      </PageHeader>

      <div className={styles.pestanas} role="group" aria-label="Vista">
        {PESTANAS.map(({ valor, texto }) => (
          <button
            key={valor}
            type="button"
            aria-pressed={vista === valor}
            className={clsx(styles.pestana, vista === valor && styles.pestanaActiva)}
            onClick={() => cambiar({ vista: valor === 'cargas' ? '' : valor })}
          >
            {texto}
          </button>
        ))}
      </div>

      <div className={styles.filtros}>
        <Selector
          label="Vehículo"
          opciones={opcionesVehiculo}
          opcionVacia="Todos los vehículos"
          value={vehiculo}
          onChange={(valor) => cambiar({ vehiculo_id: valor })}
        />
        <Field label="Desde" type="date" value={desde} onChange={(event) => cambiar({ desde: event.target.value })} />
        <Field
          label="Hasta"
          type="date"
          value={hasta}
          onChange={(event) => cambiar({ hasta: event.target.value })}
          error={rangoInvalido ? 'Debe ser igual o posterior a la fecha inicial.' : undefined}
        />
      </div>

      {meta && !error && !rangoInvalido && (
        <p className={styles.tenue} aria-live="polite">
          {vista === 'cargas'
            ? `${formatNumber(meta.total, 0)} cargas · ${galones(meta.total_litros)} gal (${formatNumber(meta.total_litros)} L) · ${formatoDinero(meta.total_costo)}`
            : `${galones(meta.litros)} gal (${formatNumber(meta.litros)} L) · ${formatoDinero(meta.costo)} · ${formatNumber(meta.km)} km recorridos. Rendimiento estimado; no cambia el cálculo oficial de CO₂.`}
        </p>
      )}

      {error ? (
        <Notice tono="peligro" titulo="No pudimos cargar el combustible" role="alert">
          <p>{error.message}</p>
          <Button variant="enlacePeligro" onClick={() => setRecarga((valor) => valor + 1)}>
            Reintentar
          </Button>
        </Notice>
      ) : !cargando && filas.length === 0 ? (
        <EmptyState
          titulo={filtrado ? 'Nada coincide con los filtros' : 'Todavía no hay cargas'}
          accion={
            filtrado ? (
              <Button variant="secundario" onClick={() => cambiar({ vehiculo_id: '', desde: '', hasta: '' })}>
                Quitar filtros
              </Button>
            ) : (
              puedeRegistrar && <Button icon={Plus} onClick={() => setEditando('nueva')}>Registrar la primera</Button>
            )
          }
        >
          {filtrado ? 'Prueba con otro vehículo o con otro rango de fechas.' : 'Las cargas que registres aparecerán aquí.'}
        </EmptyState>
      ) : vista === 'cargas' ? (
        <div className={styles.envoltura} aria-busy={cargando}>
          <table className={styles.tabla}>
            <thead>
              <tr>
                <th scope="col">Fecha</th>
                <th scope="col">Vehículo</th>
                <th scope="col" className={styles.derecha}>Galones</th>
                <th scope="col" className={styles.derecha}>Litros</th>
                <th scope="col" className={styles.derecha}>Precio por galón</th>
                <th scope="col" className={styles.derecha}>Costo</th>
                <th scope="col">Viaje</th>
                {puedeRegistrar && <th scope="col"><span className="visually-hidden">Acciones</span></th>}
              </tr>
            </thead>
            <tbody>
              {filas.map((carga) => (
                <tr key={carga.carga_combustible_id}>
                  <th scope="row">{formatDateTime(carga.cargado_en)}</th>
                  <td className={styles.mono}>{carga.placa}</td>
                  <td className={styles.derecha}>{galones(carga.litros)}</td>
                  <td className={clsx(styles.derecha, styles.tenue)}>{formatNumber(carga.litros)}</td>
                  <td className={styles.derecha}>
                    {carga.precio_por_litro == null ? '—' : formatoDinero(precioEnUnidad(carga.precio_por_litro, 'galones'))}
                  </td>
                  <td className={styles.derecha}>{carga.costo == null ? '—' : formatoDinero(carga.costo)}</td>
                  <td className={carga.viaje ? undefined : styles.tenue}>
                    {carga.viaje_id ? <Link to={`/viajes/${carga.viaje_id}`}>{carga.viaje}</Link> : 'Sin viaje'}
                  </td>
                  {puedeRegistrar && (
                    <td>
                      <span className={styles.acciones}>
                        <Button variant="enlace" onClick={() => setEditando(carga)}>
                          Corregir
                        </Button>
                        {esAdmin && (
                          <Button variant="enlace" onClick={() => setAEliminar(carga)}>
                            Eliminar
                          </Button>
                        )}
                      </span>
                    </td>
                  )}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <div className={styles.envoltura} aria-busy={cargando}>
          <table className={styles.tabla}>
            <thead>
              <tr>
                <th scope="col">Vehículo</th>
                <th scope="col">Combustible</th>
                <th scope="col" className={styles.derecha}>Cargas</th>
                <th scope="col" className={styles.derecha}>Galones</th>
                <th scope="col" className={styles.derecha}>Costo</th>
                <th scope="col" className={styles.derecha}>Viajes</th>
                <th scope="col" className={styles.derecha}>Km</th>
                <th scope="col" className={styles.derecha}>Km por galón</th>
              </tr>
            </thead>
            <tbody>
              {filas.map((fila) => (
                <tr key={fila.vehiculo_id}>
                  <th scope="row" className={styles.mono}>
                    <Link to={`/vehiculos/${fila.vehiculo_id}`}>{fila.placa}</Link>
                  </th>
                  <td>{etiquetaDe(catalogos?.tipos_combustible, fila.tipo_combustible)}</td>
                  <td className={styles.derecha}>{formatNumber(fila.cargas, 0)}</td>
                  <td className={styles.derecha}>{galones(fila.litros)}</td>
                  <td className={styles.derecha}>
                    {formatoDinero(fila.costo)}
                    {fila.litros_sin_precio > 0 && <span className={styles.tenue}> *</span>}
                  </td>
                  <td className={styles.derecha}>{formatNumber(fila.viajes, 0)}</td>
                  <td className={styles.derecha}>{formatNumber(fila.km)}</td>
                  <td className={styles.derecha}>
                    {fila.rendimiento_km_l == null ? '—' : formatNumber(kmPorGalon(fila.rendimiento_km_l))}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {filas.some((fila) => fila.litros_sin_precio > 0) && (
            <p className={styles.tenue}>* Hay cargas sin precio registrado; su costo no se suma.</p>
          )}
        </div>
      )}

      {vista === 'cargas' && !error && !rangoInvalido && (
        <Paginacion meta={meta} onCambiar={(pagina) => cambiar({ page: String(pagina) })} />
      )}

      {editando && (
        <CargaDialog
          carga={editando === 'nueva' ? null : editando}
          vehiculos={vehiculos}
          onCerrar={() => setEditando(null)}
          onGuardada={() => {
            setEditando(null);
            setRecarga((valor) => valor + 1);
          }}
        />
      )}

      {aEliminar && (
        <ConfirmDialog
          abierto
          titulo="Eliminar carga"
          accion="Eliminar definitivamente"
          ocupado={eliminando}
          onConfirmar={eliminar}
          onCerrar={() => setAEliminar(null)}
        >
          <p>
            {aEliminar.placa} · {formatDateTime(aEliminar.cargado_en)} · {galones(aEliminar.litros)} gal. Úsalo solo para cargas
            registradas por error. La bitácora conserva sus datos.
          </p>
        </ConfirmDialog>
      )}
    </>
  );
}
