import { useEffect, useRef, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { Download, Plus, Search } from 'lucide-react';
import { clsx } from 'clsx';
import { toast } from 'sonner';
import { exportarViajes, getVehiculos, getViajes } from '../api/endpoints';
import { useAuth } from '../auth/useAuth';
import { Paginacion } from '../components/Paginacion';
import { Selector } from '../components/Selector';
import { Button, EmptyState, Field, Notice, PageHeader, RouteBoard, StatusTag } from '../components/ui';
import { formatDateTime, formatNumber } from '../utils/format';
import { ESTADOS_VIAJE, PESTANAS_VIAJE, puedeRegistrarViajes } from '../utils/viajes';
import styles from '../components/Listado.module.css';
import buscador from './Vehiculos.module.css';

const LIMIT = 15;
const SEARCH_DELAY_MS = 300;

export default function Viajes() {
  const { usuario } = useAuth();
  const puedeRegistrar = puedeRegistrarViajes(usuario?.rol);
  const [params, setParams] = useSearchParams();
  const estado = params.get('estado') ?? 'todos';
  const desde = params.get('desde') ?? '';
  const hasta = params.get('hasta') ?? '';
  const vehiculo = params.get('vehiculo_id') ?? '';
  const q = params.get('q') ?? '';
  const page = Number(params.get('page') ?? 1);
  const [busqueda, setBusqueda] = useState(q);
  const [respuesta, setRespuesta] = useState({ clave: null });
  const [recarga, setRecarga] = useState(0);
  const [vehiculos, setVehiculos] = useState([]);
  const [exportando, setExportando] = useState(false);
  const temporizador = useRef(null);

  const filtros = { estado, desde, hasta, vehiculo_id: vehiculo, q };
  const clave = JSON.stringify({ ...filtros, page, recarga });
  const rangoInvalido = Boolean(desde && hasta && hasta < desde);

  useEffect(() => {
    const controller = new AbortController();
    getVehiculos({ estado: 'todos', limit: 100, sort: 'placa' }, controller.signal)
      .then(({ data }) => setVehiculos(data.map((v) => ({ valor: v.vehiculo_id, etiqueta: v.placa }))))
      .catch(() => {});
    return () => controller.abort();
  }, []);

  useEffect(() => {
    if (rangoInvalido) return undefined;
    const controller = new AbortController();
    const { estado: e, desde: d, hasta: h, vehiculo_id: v, q: texto } = JSON.parse(clave);
    getViajes({ estado: e, desde: d, hasta: h, vehiculo_id: v, q: texto, page, limit: LIMIT }, controller.signal)
      .then((resultado) => setRespuesta({ clave, resultado }))
      .catch((falla) => {
        if (falla.name !== 'AbortError') setRespuesta({ clave, error: falla });
      });
    return () => controller.abort();
  }, [clave, page, rangoInvalido]);

  useEffect(() => () => clearTimeout(temporizador.current), []);

  const cambiar = (cambios) => {
    const siguientes = new URLSearchParams(window.location.search);
    Object.entries(cambios).forEach(([nombre, valor]) => {
      if (valor) siguientes.set(nombre, valor);
      else siguientes.delete(nombre);
    });
    if (!('page' in cambios)) siguientes.delete('page');
    setParams(siguientes, { replace: true });
  };

  const buscar = (valor) => {
    setBusqueda(valor);
    clearTimeout(temporizador.current);
    temporizador.current = setTimeout(() => cambiar({ q: valor.trim() }), SEARCH_DELAY_MS);
  };

  const exportar = async () => {
    setExportando(true);
    try {
      await exportarViajes(filtros, 'viajes.csv');
    } catch (falla) {
      toast.error('No se pudo exportar', { description: falla.message });
    } finally {
      setExportando(false);
    }
  };

  const limpiar = () => {
    setBusqueda('');
    cambiar({ desde: '', hasta: '', vehiculo_id: '', q: '' });
  };

  const cargando = !rangoInvalido && respuesta.clave !== clave;
  const error = cargando || rangoInvalido ? null : respuesta.error;
  const viajes = rangoInvalido ? [] : (respuesta.resultado?.data ?? []);
  const meta = respuesta.resultado?.meta;
  const filtrado = Boolean(desde || hasta || vehiculo || q);

  return (
    <>
      <PageHeader titulo="Viajes">
        <Button variant="secundario" icon={Download} onClick={exportar} disabled={exportando || rangoInvalido}>
          {exportando ? 'Exportando…' : 'Exportar CSV'}
        </Button>
        {puedeRegistrar && (
          <Button to="/viajes/nuevo" icon={Plus}>
            Registrar salida
          </Button>
        )}
      </PageHeader>

      <div className={styles.pestanas} role="group" aria-label="Filtrar por estado">
        {PESTANAS_VIAJE.map(({ valor, texto }) => (
          <button
            key={valor}
            type="button"
            aria-pressed={estado === valor}
            className={clsx(styles.pestana, estado === valor && styles.pestanaActiva)}
            onClick={() => cambiar({ estado: valor === 'todos' ? '' : valor })}
          >
            {texto}
            <span className={buscador.conteo}>{meta ? formatNumber(meta.conteos?.[valor], 0) : '–'}</span>
          </button>
        ))}
      </div>

      <div className={styles.filtros}>
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
          error={rangoInvalido ? 'Debe ser igual o posterior a la fecha inicial.' : undefined}
        />
        <Selector
          label="Vehículo"
          opciones={vehiculos}
          opcionVacia="Todos los vehículos"
          value={vehiculo}
          onChange={(valor) => cambiar({ vehiculo_id: valor })}
        />
        <label className={buscador.buscador}>
          <span className="visually-hidden">Buscar por origen, destino o placa</span>
          <Search size={16} strokeWidth={1.75} aria-hidden="true" />
          <input
            type="search"
            placeholder="Origen, destino o placa"
            value={busqueda}
            onChange={(event) => buscar(event.target.value)}
          />
        </label>
      </div>

      {meta && !error && !rangoInvalido && (
        <p className={styles.tenue} aria-live="polite">
          {formatNumber(meta.total, 0)} viajes · {formatNumber(meta.total_km)} km · {formatNumber(meta.total_co2_kg)} kg de CO₂
          {estado !== 'cancelado' ? ' (sin contar cancelados)' : ''}
        </p>
      )}

      {error ? (
        <Notice tono="peligro" titulo="No pudimos cargar los viajes" role="alert">
          <p>{error.message}</p>
          <Button variant="enlacePeligro" onClick={() => setRecarga((valor) => valor + 1)}>
            Reintentar
          </Button>
        </Notice>
      ) : !cargando && viajes.length === 0 ? (
        <EmptyState
          titulo={filtrado ? 'Ningún viaje coincide con los filtros' : 'No hay viajes en este estado'}
          accion={
            filtrado ? (
              <Button variant="secundario" onClick={limpiar}>
                Quitar filtros
              </Button>
            ) : (
              puedeRegistrar && <Button to="/viajes/nuevo" icon={Plus}>Registrar una salida</Button>
            )
          }
        >
          {filtrado ? 'Prueba con otro rango de fechas o con otro vehículo.' : 'Los viajes que registres aparecerán aquí.'}
        </EmptyState>
      ) : (
        <div className={styles.envoltura} aria-busy={cargando}>
          <table className={styles.tabla}>
            <thead>
              <tr>
                <th scope="col">Salida</th>
                <th scope="col">Vehículo</th>
                <th scope="col">Ruta</th>
                <th scope="col">Chofer</th>
                <th scope="col" className={styles.derecha}>Km</th>
                <th scope="col" className={styles.derecha}>kg CO₂</th>
                <th scope="col">Estado</th>
                <th scope="col"><span className="visually-hidden">Acciones</span></th>
              </tr>
            </thead>
            <tbody>
              {viajes.map((viaje) => {
                const { tono, texto } = ESTADOS_VIAJE[viaje.estado];
                return (
                  <tr key={viaje.viaje_id}>
                    <th scope="row">{formatDateTime(viaje.salida_en)}</th>
                    <td className={styles.mono}>{viaje.placa}</td>
                    <td>
                      <RouteBoard origen={viaje.origen} destino={viaje.destino} />
                    </td>
                    <td className={viaje.chofer ? undefined : styles.tenue}>{viaje.chofer ?? 'Sin chofer'}</td>
                    <td className={styles.derecha}>{formatNumber(viaje.distancia_km)}</td>
                    <td className={styles.derecha}>{formatNumber(viaje.co2_kg)}</td>
                    <td>
                      <StatusTag tono={tono}>{texto}</StatusTag>
                    </td>
                    <td>
                      <Link to={`/viajes/${viaje.viaje_id}`}>Ver</Link>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      {!error && !rangoInvalido && <Paginacion meta={meta} onCambiar={(pagina) => cambiar({ page: String(pagina) })} />}
    </>
  );
}
