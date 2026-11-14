import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { clsx } from 'clsx';
import { getFactores } from '../api/endpoints';
import { useAuth } from '../auth/useAuth';
import { Paginacion } from '../components/Paginacion';
import { Selector } from '../components/Selector';
import { SustituirFactor } from '../components/SustituirFactor';
import { Button, EmptyState, Notice, PageHeader, StatusTag } from '../components/ui';
import { etiquetaDe, useCatalogos } from '../hooks/useCatalogos';
import { formatFactor, formatNumber, formatOnlyDate } from '../utils/format';
import { VIGENCIAS, estadoFactor, hoyElSalvador } from '../utils/factores';
import styles from '../components/Listado.module.css';

const LIMIT = 20;

export default function Factores() {
  const { puedeEscribir } = useAuth();
  const { catalogos } = useCatalogos();
  const [params, setParams] = useSearchParams();
  const vigencia = params.get('vigencia') ?? 'vigentes';
  const combustible = params.get('tipo_combustible') ?? '';
  const tipo = params.get('tipo_vehiculo') ?? '';
  const page = Number(params.get('page') ?? 1);
  const [respuesta, setRespuesta] = useState({ clave: null });
  const [recarga, setRecarga] = useState(0);
  const [aSustituir, setASustituir] = useState(null);

  const clave = JSON.stringify({ vigencia, combustible, tipo, page, recarga });

  useEffect(() => {
    const controller = new AbortController();
    getFactores({ vigencia, tipo_combustible: combustible, tipo_vehiculo: tipo, page, limit: LIMIT }, controller.signal)
      .then((resultado) => setRespuesta({ clave, resultado }))
      .catch((falla) => {
        if (falla.name !== 'AbortError') setRespuesta({ clave, error: falla });
      });
    return () => controller.abort();
  }, [clave, vigencia, combustible, tipo, page]);

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

  const combinacion = (factor) =>
    `${etiquetaDe(catalogos?.tipos_combustible, factor.tipo_combustible)} · ${etiquetaDe(catalogos?.tipos_vehiculo, factor.tipo_vehiculo)}`;

  const cargando = respuesta.clave !== clave;
  const error = cargando ? null : respuesta.error;
  const factores = respuesta.resultado?.data ?? [];
  const meta = respuesta.resultado?.meta;
  const hoy = hoyElSalvador();
  const filtrado = Boolean(combustible || tipo);

  return (
    <>
      <PageHeader
        titulo="Factores de emisión"
        subtitulo="Kilogramos de CO₂ por kilómetro según el combustible y el tipo de vehículo. Cada viaje guarda el factor con el que se calculó."
      />

      <div className={styles.pestanas} role="group" aria-label="Filtrar por vigencia">
        {VIGENCIAS.map(({ valor, texto }) => (
          <button
            key={valor}
            type="button"
            aria-pressed={vigencia === valor}
            className={clsx(styles.pestana, vigencia === valor && styles.pestanaActiva)}
            onClick={() => cambiar({ vigencia: valor === 'vigentes' ? '' : valor })}
          >
            {texto}
          </button>
        ))}
      </div>

      <div className={styles.filtros}>
        <Selector
          label="Combustible"
          compacto
          opciones={catalogos?.tipos_combustible ?? []}
          opcionVacia="Todos los combustibles"
          value={combustible}
          onChange={(valor) => cambiar({ tipo_combustible: valor })}
        />
        <Selector
          label="Tipo de vehículo"
          compacto
          opciones={catalogos?.tipos_vehiculo ?? []}
          opcionVacia="Todos los tipos"
          value={tipo}
          onChange={(valor) => cambiar({ tipo_vehiculo: valor })}
        />
      </div>

      {error ? (
        <Notice tono="peligro" titulo="No pudimos cargar los factores" role="alert">
          <p>{error.message}</p>
          <Button variant="enlacePeligro" onClick={() => setRecarga((valor) => valor + 1)}>
            Reintentar
          </Button>
        </Notice>
      ) : !cargando && factores.length === 0 ? (
        <EmptyState
          titulo={filtrado ? 'Ningún factor coincide con los filtros' : 'No hay factores en esta vista'}
          accion={
            filtrado && (
              <Button variant="secundario" onClick={() => cambiar({ tipo_combustible: '', tipo_vehiculo: '' })}>
                Quitar filtros
              </Button>
            )
          }
        >
          {vigencia === 'historicos'
            ? 'Aquí aparecen los factores que ya fueron sustituidos.'
            : 'Prueba con otra vigencia o con otra combinación.'}
        </EmptyState>
      ) : (
        <div className={styles.envoltura} aria-busy={cargando}>
          <table className={styles.tabla}>
            <thead>
              <tr>
                <th scope="col">Combustible</th>
                <th scope="col">Tipo de vehículo</th>
                <th scope="col" className={styles.derecha}>kg CO₂/km</th>
                <th scope="col">Vigencia</th>
                <th scope="col">Fuente</th>
                <th scope="col" className={styles.derecha}>Viajes</th>
                <th scope="col">Estado</th>
                {puedeEscribir && <th scope="col"><span className="visually-hidden">Acciones</span></th>}
              </tr>
            </thead>
            <tbody>
              {factores.map((factor) => {
                const { tono, texto } = estadoFactor(factor, hoy);
                return (
                  <tr key={factor.factor_emision_id}>
                    <th scope="row">{etiquetaDe(catalogos?.tipos_combustible, factor.tipo_combustible)}</th>
                    <td>{etiquetaDe(catalogos?.tipos_vehiculo, factor.tipo_vehiculo)}</td>
                    <td className={clsx(styles.derecha, styles.mono)}>{formatFactor(factor.factor_co2_km)}</td>
                    <td>
                      {formatOnlyDate(factor.vigente_desde)} –{' '}
                      {factor.vigente_hasta ? formatOnlyDate(factor.vigente_hasta) : <span className={styles.tenue}>sin fin</span>}
                    </td>
                    <td className={styles.tenue}>{factor.fuente ?? '—'}</td>
                    <td className={styles.derecha}>{formatNumber(factor.viajes, 0)}</td>
                    <td>
                      <StatusTag tono={tono}>{texto}</StatusTag>
                    </td>
                    {puedeEscribir && (
                      <td>
                        {!factor.vigente_hasta && (
                          <Button variant="enlace" onClick={() => setASustituir(factor)}>
                            Sustituir
                          </Button>
                        )}
                      </td>
                    )}
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      {!error && <Paginacion meta={meta} onCambiar={(pagina) => cambiar({ page: String(pagina) })} />}

      {aSustituir && (
        <SustituirFactor
          factor={aSustituir}
          combinacion={combinacion(aSustituir)}
          onCerrar={() => setASustituir(null)}
          onSustituido={() => {
            setASustituir(null);
            setRecarga((valor) => valor + 1);
          }}
        />
      )}
    </>
  );
}
