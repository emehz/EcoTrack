import { useEffect, useRef, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { Plus, Search } from 'lucide-react';
import { clsx } from 'clsx';
import { getChoferes } from '../api/endpoints';
import { useAuth } from '../auth/useAuth';
import { CambiarEstadoChofer } from '../components/CambiarEstadoChofer';
import { Paginacion } from '../components/Paginacion';
import { Button, EmptyState, Notice, PageHeader, StatusTag } from '../components/ui';
import { formatNumber } from '../utils/format';
import { ESTADOS_CHOFER } from '../utils/choferes';
import styles from '../components/Listado.module.css';
import buscador from './Vehiculos.module.css';

const LIMIT = 10;
const SEARCH_DELAY_MS = 300;

const PESTANAS = [
  { valor: 'activo', texto: 'Activos' },
  { valor: 'suspendido', texto: 'Suspendidos' },
  { valor: 'inactivo', texto: 'Inactivos' },
  { valor: 'todos', texto: 'Todos' },
];

export default function Choferes() {
  const { puedeEscribir } = useAuth();
  const [params, setParams] = useSearchParams();
  const estado = params.get('estado') ?? 'activo';
  const q = params.get('q') ?? '';
  const page = Number(params.get('page') ?? 1);
  const [busqueda, setBusqueda] = useState(q);
  const [respuesta, setRespuesta] = useState({ clave: null });
  const [recarga, setRecarga] = useState(0);
  const [aCambiar, setACambiar] = useState(null);
  const temporizador = useRef(null);

  const clave = JSON.stringify({ estado, q, page, recarga });

  useEffect(() => {
    const controller = new AbortController();
    getChoferes({ estado, q, page, limit: LIMIT }, controller.signal)
      .then((resultado) => setRespuesta({ clave, resultado }))
      .catch((falla) => {
        if (falla.name !== 'AbortError') setRespuesta({ clave, error: falla });
      });
    return () => controller.abort();
  }, [clave, estado, q, page]);

  useEffect(() => () => clearTimeout(temporizador.current), []);

  const cambiar = (cambios) =>
    setParams((actuales) => {
      const siguientes = new URLSearchParams(actuales);
      Object.entries(cambios).forEach(([nombre, valor]) => {
        if (valor) siguientes.set(nombre, valor);
        else siguientes.delete(nombre);
      });
      if (!('page' in cambios)) siguientes.delete('page');
      return siguientes;
    }, { replace: true });

  const buscar = (valor) => {
    setBusqueda(valor);
    clearTimeout(temporizador.current);
    temporizador.current = setTimeout(() => cambiar({ q: valor.trim() }), SEARCH_DELAY_MS);
  };

  const cargando = respuesta.clave !== clave;
  const error = cargando ? null : respuesta.error;
  const choferes = respuesta.resultado?.data ?? [];
  const meta = respuesta.resultado?.meta;

  return (
    <>
      <PageHeader titulo="Choferes">
        {puedeEscribir && (
          <Button to="/choferes/nuevo" icon={Plus}>
            Nuevo chofer
          </Button>
        )}
      </PageHeader>

      <div className={styles.pestanas} role="group" aria-label="Filtrar por estado">
        {PESTANAS.map(({ valor, texto }) => (
          <button
            key={valor}
            type="button"
            aria-pressed={estado === valor}
            className={clsx(styles.pestana, estado === valor && styles.pestanaActiva)}
            onClick={() => cambiar({ estado: valor === 'activo' ? '' : valor })}
          >
            {texto}
            <span className={buscador.conteo}>{meta ? formatNumber(meta.conteos?.[valor], 0) : '–'}</span>
          </button>
        ))}
      </div>

      <div className={styles.filtros}>
        <label className={buscador.buscador}>
          <span className="visually-hidden">Buscar por nombre o licencia</span>
          <Search size={16} strokeWidth={1.75} aria-hidden="true" />
          <input
            type="search"
            placeholder="Buscar por nombre o licencia"
            value={busqueda}
            onChange={(event) => buscar(event.target.value)}
          />
        </label>
      </div>

      {error ? (
        <Notice tono="peligro" titulo="No pudimos cargar los choferes" role="alert">
          <p>{error.message}</p>
          <Button variant="enlacePeligro" onClick={() => setRecarga((valor) => valor + 1)}>
            Reintentar
          </Button>
        </Notice>
      ) : !cargando && choferes.length === 0 ? (
        <EmptyState
          titulo={q ? 'Ningún chofer coincide con la búsqueda' : 'No hay choferes en este estado'}
          accion={
            q ? (
              <Button variant="secundario" onClick={() => { setBusqueda(''); cambiar({ q: '' }); }}>
                Limpiar búsqueda
              </Button>
            ) : (
              puedeEscribir && estado === 'activo' && <Button to="/choferes/nuevo" icon={Plus}>Registrar el primero</Button>
            )
          }
        >
          {q ? 'Revisa el nombre o el número de licencia.' : 'Los choferes que registres aparecerán aquí.'}
        </EmptyState>
      ) : (
        <div className={styles.envoltura} aria-busy={cargando}>
          <table className={styles.tabla}>
            <thead>
              <tr>
                <th scope="col">Nombre</th>
                <th scope="col">Licencia</th>
                <th scope="col">Teléfono</th>
                <th scope="col">Estado</th>
                <th scope="col"><span className="visually-hidden">Acciones</span></th>
              </tr>
            </thead>
            <tbody>
              {choferes.map((chofer) => {
                const { tono, texto } = ESTADOS_CHOFER[chofer.estado];
                return (
                  <tr key={chofer.chofer_id}>
                    <th scope="row">
                      {chofer.apellidos}, {chofer.nombres}
                    </th>
                    <td>{chofer.numero_licencia}</td>
                    <td className={styles.tenue}>{chofer.telefono ?? '—'}</td>
                    <td>
                      <StatusTag tono={tono}>{texto}</StatusTag>
                    </td>
                    <td>
                      <span className={styles.acciones}>
                        <Link to={`/choferes/${chofer.chofer_id}`}>Ver</Link>
                        {puedeEscribir && <Link to={`/choferes/${chofer.chofer_id}/editar`}>Editar</Link>}
                        {puedeEscribir && (
                          <Button variant="enlace" onClick={() => setACambiar(chofer)}>
                            Cambiar estado
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

      {!error && <Paginacion meta={meta} onCambiar={(pagina) => cambiar({ page: String(pagina) })} />}

      {aCambiar && (
        <CambiarEstadoChofer
          chofer={aCambiar}
          onCerrar={() => setACambiar(null)}
          onCambiado={() => {
            setACambiar(null);
            setRecarga((valor) => valor + 1);
          }}
        />
      )}
    </>
  );
}
