import { useCallback, useEffect, useRef, useState } from 'react';
import { Link, useLocation, useSearchParams } from 'react-router-dom';
import { ArrowDown, ArrowUp, Plus, Search } from 'lucide-react';
import { clsx } from 'clsx';
import { toast } from 'sonner';
import { darDeBajaVehiculo, getVehiculos } from '../api/endpoints';
import { useAuth } from '../auth/useAuth';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { Selector } from '../components/Selector';
import { Button, EmptyState, EstadoVehiculo, Notice, PageHeader } from '../components/ui';
import { etiquetaDe, useCatalogos } from '../hooks/useCatalogos';
import { formatDate, formatFactor, formatNumber } from '../utils/format';
import styles from './Vehiculos.module.css';

const LIMIT = 10;
const SEARCH_DELAY_MS = 300;

const PESTANAS = [
  { valor: 'activo', texto: 'Activos', conteo: 'activo' },
  { valor: 'mantenimiento', texto: 'Mantenimiento', conteo: 'mantenimiento' },
  { valor: 'inactivo', texto: 'Inactivos', conteo: 'inactivo' },
  { valor: 'todos', texto: 'Todos', conteo: 'todos' },
];

function Encabezado({ campo, orden, onOrdenar, children, derecha }) {
  const activo = orden.campo === campo;
  const Flecha = orden.sentido === 'desc' ? ArrowDown : ArrowUp;
  return (
    <th
      scope="col"
      className={derecha ? styles.derecha : undefined}
      aria-sort={!campo ? undefined : activo ? (orden.sentido === 'desc' ? 'descending' : 'ascending') : 'none'}
    >
      {campo ? (
        <button type="button" className={styles.ordenar} onClick={() => onOrdenar(campo)}>
          {children}
          <Flecha size={13} className={activo ? undefined : styles.flechaOculta} aria-hidden="true" />
        </button>
      ) : (
        children
      )}
    </th>
  );
}

function FilasEsqueleto() {
  return Array.from({ length: 6 }, (_, fila) => (
    <tr key={fila}>
      {Array.from({ length: 7 }, (_, columna) => (
        <td key={columna}>
          <span className="skeleton" style={{ height: 16, width: columna === 6 ? 120 : 80 }} />
        </td>
      ))}
    </tr>
  ));
}

export default function Vehiculos() {
  const { puedeEscribir } = useAuth();
  const location = useLocation();
  const [params, setParams] = useSearchParams();
  const { catalogos } = useCatalogos();

  const estado = params.get('estado') ?? 'activo';
  const q = params.get('q') ?? '';
  const tipo = params.get('tipo') ?? '';
  const combustible = params.get('combustible') ?? '';
  const sort = params.get('sort') ?? 'placa';
  const order = params.get('order') ?? 'asc';
  const page = Number(params.get('page') ?? 1);

  const [busqueda, setBusqueda] = useState(q);
  const [respuesta, setRespuesta] = useState({ clave: null });
  const [recarga, setRecarga] = useState(0);
  const [aDarDeBaja, setADarDeBaja] = useState(null);
  const [ocupado, setOcupado] = useState(false);
  const resaltado = location.state?.resaltar;
  const busquedaTimer = useRef(null);

  const cambiar = useCallback(
    (cambios) => {
      setParams(
        (actuales) => {
          const siguientes = new URLSearchParams(actuales);
          Object.entries(cambios).forEach(([clave, valor]) => {
            if (valor) siguientes.set(clave, valor);
            else siguientes.delete(clave);
          });
          if (!('page' in cambios)) siguientes.delete('page');
          return siguientes;
        },
        { replace: true },
      );
    },
    [setParams],
  );

  useEffect(() => () => clearTimeout(busquedaTimer.current), []);

  const buscar = (valor) => {
    setBusqueda(valor);
    clearTimeout(busquedaTimer.current);
    busquedaTimer.current = setTimeout(() => cambiar({ q: valor.trim() }), SEARCH_DELAY_MS);
  };

  const clave = JSON.stringify({ estado, q, tipo, combustible, sort, order, page, recarga });

  useEffect(() => {
    const controller = new AbortController();
    getVehiculos(
      { estado, q, tipo_vehiculo: tipo, tipo_combustible: combustible, sort, order, page, limit: LIMIT },
      controller.signal,
    )
      .then((resultado) => setRespuesta({ clave, resultado }))
      .catch((falla) => {
        if (falla.name !== 'AbortError') setRespuesta((anterior) => ({ ...anterior, clave, error: falla }));
      });
    return () => controller.abort();
  }, [clave, estado, q, tipo, combustible, sort, order, page]);

  const ultimaPagina = respuesta.resultado?.meta.total_pages;

  useEffect(() => {
    if (ultimaPagina > 0 && page > ultimaPagina) cambiar({ page: String(ultimaPagina) });
  }, [page, ultimaPagina, cambiar]);

  const cargando = respuesta.clave !== clave;
  const error = cargando ? null : respuesta.error;
  const resultado = respuesta.resultado;

  const ordenar = (campo) =>
    cambiar({ sort: campo, order: sort === campo && order === 'asc' ? 'desc' : 'asc' });

  const limpiarFiltros = () => {
    clearTimeout(busquedaTimer.current);
    setBusqueda('');
    cambiar({ q: '', tipo: '', combustible: '' });
  };

  const confirmarBaja = async () => {
    setOcupado(true);
    try {
      await darDeBajaVehiculo(aDarDeBaja.vehiculo_id);
      toast.success('Vehículo dado de baja', { description: `${aDarDeBaja.placa} pasó a Inactivos.` });
      setADarDeBaja(null);
      setRecarga((valor) => valor + 1);
    } catch (falla) {
      toast.error('No se pudo dar de baja', { description: falla.message });
    } finally {
      setOcupado(false);
    }
  };

  const vehiculos = resultado?.data ?? [];
  const meta = resultado?.meta;
  const orden = { campo: sort, sentido: order };
  const hayFiltros = Boolean(q || tipo || combustible);

  return (
    <>
      <PageHeader titulo="Vehículos">
        {puedeEscribir && (
          <Button to="/vehiculos/nuevo" icon={Plus}>
            Nuevo vehículo
          </Button>
        )}
      </PageHeader>

      <div className={styles.pestanas} role="group" aria-label="Filtrar por estado">
        {PESTANAS.map(({ valor, texto, conteo }) => (
          <button
            key={valor}
            type="button"
            aria-pressed={estado === valor}
            className={clsx(styles.pestana, estado === valor && styles.pestanaActiva)}
            onClick={() => cambiar({ estado: valor === 'activo' ? '' : valor })}
          >
            {texto}
            <span className={styles.conteo}>{meta ? formatNumber(meta.conteos?.[conteo], 0) : '–'}</span>
          </button>
        ))}
      </div>

      <div className={styles.filtros}>
        <label className={styles.buscador}>
          <span className="visually-hidden">Buscar por placa</span>
          <Search size={16} strokeWidth={1.75} aria-hidden="true" />
          <input
            type="search"
            placeholder="Buscar por placa"
            value={busqueda}
            onChange={(event) => buscar(event.target.value)}
          />
        </label>
        <Selector
          compacto
          label="Tipo de vehículo"
          opcionVacia="Todos los tipos"
          opciones={catalogos?.tipos_vehiculo}
          value={tipo}
          onChange={(valor) => cambiar({ tipo: valor })}
        />
        <Selector
          compacto
          label="Combustible"
          opcionVacia="Todos los combustibles"
          opciones={catalogos?.tipos_combustible}
          value={combustible}
          onChange={(valor) => cambiar({ combustible: valor })}
        />
      </div>

      {error ? (
        <Notice tono="peligro" titulo="No pudimos cargar los vehículos" role="alert">
          <p>{error.message}</p>
          <Button variant="enlacePeligro" onClick={() => setRecarga((valor) => valor + 1)}>
            Reintentar
          </Button>
        </Notice>
      ) : !cargando && vehiculos.length === 0 ? (
        <EmptyState
          titulo={hayFiltros ? 'Ningún vehículo coincide con la búsqueda' : 'No hay vehículos en este estado'}
          accion={
            hayFiltros ? (
              <Button variant="secundario" onClick={limpiarFiltros}>
                Limpiar filtros
              </Button>
            ) : (
              puedeEscribir && estado === 'activo' && <Button to="/vehiculos/nuevo" icon={Plus}>Registrar el primero</Button>
            )
          }
        >
          {hayFiltros
            ? 'Revisa la placa o quita algún filtro.'
            : 'Cuando registres vehículos aparecerán aquí, con el factor de emisión que les corresponde.'}
        </EmptyState>
      ) : (
        <div className={styles.envoltura} aria-busy={cargando}>
          <table className={styles.tabla}>
            <thead>
              <tr>
                <Encabezado campo="placa" orden={orden} onOrdenar={ordenar}>
                  Placa
                </Encabezado>
                <Encabezado campo="tipo_vehiculo" orden={orden} onOrdenar={ordenar}>
                  Tipo
                </Encabezado>
                <Encabezado campo="tipo_combustible" orden={orden} onOrdenar={ordenar}>
                  Combustible
                </Encabezado>
                <Encabezado orden={orden} derecha>
                  Factor (kg CO₂/km)
                </Encabezado>
                <Encabezado campo="estado" orden={orden} onOrdenar={ordenar}>
                  Estado
                </Encabezado>
                <Encabezado orden={orden}>Último viaje</Encabezado>
                <th scope="col" className={styles.celdaAcciones}>
                  <span className="visually-hidden">Acciones</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {!resultado ? (
                <FilasEsqueleto />
              ) : (
                vehiculos.map((vehiculo) => (
                  <tr key={vehiculo.vehiculo_id} className={vehiculo.vehiculo_id === resaltado ? styles.resaltada : undefined}>
                    <th scope="row">{vehiculo.placa}</th>
                    <td>{etiquetaDe(catalogos?.tipos_vehiculo, vehiculo.tipo_vehiculo)}</td>
                    <td>{etiquetaDe(catalogos?.tipos_combustible, vehiculo.tipo_combustible)}</td>
                    <td className={styles.derecha}>{formatFactor(vehiculo.factor_co2_km)}</td>
                    <td>
                      <EstadoVehiculo estado={vehiculo.estado} />
                    </td>
                    <td className={styles.tenue}>
                      {vehiculo.ultimo_viaje_en ? formatDate(vehiculo.ultimo_viaje_en) : 'Sin viajes'}
                    </td>
                    <td className={styles.celdaAcciones}>
                      <span className={styles.acciones}>
                        <Link to={`/vehiculos/${vehiculo.vehiculo_id}`} aria-label={`Ver ${vehiculo.placa}`}>
                          Ver
                        </Link>
                        {puedeEscribir && (
                          <Link to={`/vehiculos/${vehiculo.vehiculo_id}/editar`} aria-label={`Editar ${vehiculo.placa}`}>
                            Editar
                          </Link>
                        )}
                        {puedeEscribir && vehiculo.estado !== 'inactivo' && (
                          <Button
                            variant="enlacePeligro"
                            aria-label={`Dar de baja ${vehiculo.placa}`}
                            onClick={() => setADarDeBaja(vehiculo)}
                          >
                            Dar de baja
                          </Button>
                        )}
                      </span>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      )}

      {vehiculos.length > 0 && !error && (
        <div className={styles.paginacion}>
          <span>
            {formatNumber((meta.page - 1) * meta.limit + 1, 0)}–{formatNumber(Math.min(meta.page * meta.limit, meta.total), 0)} de{' '}
            {formatNumber(meta.total, 0)}
          </span>
          {meta.total_pages > 1 && (
            <span className={styles.paginas}>
              <Button variant="enlace" disabled={meta.page <= 1} onClick={() => cambiar({ page: String(meta.page - 1) })}>
                Anterior
              </Button>
              <Button
                variant="enlace"
                disabled={meta.page >= meta.total_pages}
                onClick={() => cambiar({ page: String(meta.page + 1) })}
              >
                Siguiente
              </Button>
            </span>
          )}
        </div>
      )}

      <ConfirmDialog
        abierto={Boolean(aDarDeBaja)}
        titulo={`¿Dar de baja ${aDarDeBaja?.placa ?? ''}?`}
        accion="Dar de baja"
        ocupado={ocupado}
        onConfirmar={confirmarBaja}
        onCerrar={() => setADarDeBaja(null)}
      >
        Quedará inactivo y no podrá recibir viajes ni cargas de combustible. Su historial se conserva.
      </ConfirmDialog>
    </>
  );
}
