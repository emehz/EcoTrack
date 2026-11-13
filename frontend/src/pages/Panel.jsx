import { useEffect, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { clsx } from 'clsx';
import { Area, AreaChart, CartesianGrid, Dot, LabelList, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { getPanel, getRutasPanel, getViajes } from '../api/endpoints';
import { Selector } from '../components/Selector';
import { Button, DistanceLine, EmptyState, Field, Metrics, Notice, PageHeader, RouteBoard } from '../components/ui';
import { etiquetaDe, useCatalogos } from '../hooks/useCatalogos';
import { formatDateTime, formatNumber, formatOnlyDate } from '../utils/format';
import listado from '../components/Listado.module.css';
import styles from './Panel.module.css';

const MESES = ['ene', 'feb', 'mar', 'abr', 'may', 'jun', 'jul', 'ago', 'sep', 'oct', 'nov', 'dic'];

const etiquetaMes = (clave) => {
  const [anio, mes] = clave.split('-');
  return `${MESES[Number(mes) - 1]} ${anio.slice(2)}`;
};

const co2Legible = (kg) => (kg >= 1000 ? `${formatNumber(kg / 1000)} t` : `${formatNumber(kg)} kg`);

const Punto = ({ cx, cy, index, total }) => (
  <Dot
    cx={cx}
    cy={cy}
    r={index === total - 1 ? 6 : 4}
    fill={index === total - 1 ? 'var(--hoja-600)' : 'var(--bosque-700)'}
    stroke="var(--blanco)"
    strokeWidth={2}
  />
);

function Tendencia({ meses }) {
  const datos = meses.map((m) => ({ mes: etiquetaMes(m.mes), co2: m.co2_kg }));
  const maximo = Math.max(...datos.map((d) => d.co2));
  return (
    <figure className={styles.grafico}>
      <div className={styles.lienzo}>
        <ResponsiveContainer width="100%" height="100%">
          <AreaChart data={datos} margin={{ top: 24, right: 28, bottom: 0, left: 0 }}>
            <CartesianGrid vertical={false} stroke="var(--linea)" />
            <XAxis dataKey="mes" axisLine={false} tickLine={false} tick={{ fill: 'var(--tinta-media)', fontSize: 13 }} />
            <YAxis width={44} axisLine={false} tickLine={false} tick={{ fill: 'var(--tinta-media)', fontSize: 13 }} />
            <Tooltip
              isAnimationActive={false}
              formatter={(valor) => [`${formatNumber(valor)} kg`, 'CO₂']}
              contentStyle={{ border: '1px solid var(--linea)', borderRadius: 3, fontFamily: 'var(--fuente)' }}
            />
            <Area
              type="linear"
              dataKey="co2"
              stroke="var(--bosque-700)"
              strokeWidth={2}
              fill="var(--tinte)"
              fillOpacity={1}
              dot={<Punto total={datos.length} />}
              activeDot={{ r: 6 }}
              isAnimationActive={false}
            >
              <LabelList
                dataKey="co2"
                position="top"
                offset={12}
                formatter={(valor) => (valor > 0 && valor === maximo ? formatNumber(valor) : '')}
                style={{ fill: 'var(--tinta)', fontSize: 13, fontWeight: 600 }}
              />
            </Area>
          </AreaChart>
        </ResponsiveContainer>
      </div>
      <figcaption className="visually-hidden">
        CO₂ mensual en kilogramos: {datos.map(({ mes, co2 }) => `${mes} ${formatNumber(co2)}`).join(', ')}.
      </figcaption>
    </figure>
  );
}

function Barras({ titulo, filas, ancho = false }) {
  if (filas.length === 0) {
    return (
      <section className={styles.barras}>
        <h2 className={styles.seccion}>{titulo}</h2>
        <p className={styles.tenue}>Sin viajes completados en este periodo.</p>
      </section>
    );
  }
  const maximo = Math.max(...filas.map(([, valor]) => valor));
  return (
    <section className={styles.barras}>
      <h2 className={styles.seccion}>{titulo}</h2>
      <ul>
        {filas.map(([nombre, valor]) => (
          <li key={nombre} className={clsx(styles.barra, ancho && styles.barraAncha)}>
            <span>{nombre}</span>
            <span className={styles.pista}>
              <span style={{ width: `${Math.max(2, maximo > 0 ? (valor / maximo) * 100 : 0)}%` }} />
            </span>
            <span className={styles.valor}>{formatNumber(valor)}</span>
          </li>
        ))}
      </ul>
    </section>
  );
}

function variacionTexto(panel) {
  const { resumen, variacion_intensidad_pct: variacion } = panel;
  if (resumen.intensidad_kg_km == null) return 'Sin kilómetros recorridos en el periodo';
  const base = `Intensidad ${formatNumber(resumen.intensidad_kg_km, 3)} kg/km`;
  if (variacion == null) return `${base}; sin datos del periodo anterior para comparar`;
  if (variacion === 0) return `${base}, igual que en el periodo anterior`;
  return `${base}, ${formatNumber(Math.abs(variacion), 1)} % ${variacion < 0 ? 'menos' : 'más'} que en el periodo anterior`;
}

export default function Panel() {
  const { catalogos } = useCatalogos();
  const [params, setParams] = useSearchParams();
  const desde = params.get('desde') ?? '';
  const hasta = params.get('hasta') ?? '';
  const tipo = params.get('tipo_vehiculo') ?? '';
  const combustible = params.get('tipo_combustible') ?? '';
  const [respuesta, setRespuesta] = useState({ clave: null });
  const [recarga, setRecarga] = useState(0);

  const rangoInvalido = Boolean(desde && hasta && hasta < desde);
  const clave = JSON.stringify({ desde, hasta, tipo, combustible, recarga });

  useEffect(() => {
    if (rangoInvalido) return undefined;
    const controller = new AbortController();
    const { signal } = controller;
    const filtros = { desde, hasta, tipo_vehiculo: tipo, tipo_combustible: combustible };
    getPanel(filtros, signal)
      .then(async ({ data: panel }) => {
        const [rutas, recientes] = await Promise.all([
          getRutasPanel(filtros, signal),
          getViajes(
            { estado: 'completado', desde: panel.periodo.desde, hasta: panel.periodo.hasta, limit: 5 },
            signal,
          ),
        ]);
        setRespuesta({ clave, panel, rutas: rutas.data, recientes: recientes.data });
      })
      .catch((falla) => {
        if (falla.name !== 'AbortError') setRespuesta({ clave, error: falla });
      });
    return () => controller.abort();
  }, [clave, desde, hasta, tipo, combustible, rangoInvalido]);

  const cambiar = (cambios) => {
    const siguientes = new URLSearchParams(window.location.search);
    Object.entries(cambios).forEach(([nombre, valor]) => {
      if (valor) siguientes.set(nombre, valor);
      else siguientes.delete(nombre);
    });
    setParams(siguientes, { replace: true });
  };

  const { panel, rutas, recientes, error } = respuesta;
  const cargando = !rangoInvalido && respuesta.clave !== clave;
  const nombreCombustible = (valor) => etiquetaDe(catalogos?.tipos_combustible, valor);
  const nombreTipo = (valor) => etiquetaDe(catalogos?.tipos_vehiculo, valor);

  const filtros = (
    <div className={listado.filtros}>
      <Field
        label="Desde"
        type="date"
        value={desde || panel?.periodo.desde || ''}
        onChange={(event) => cambiar({ desde: event.target.value })}
      />
      <Field
        label="Hasta"
        type="date"
        value={hasta || panel?.periodo.hasta || ''}
        onChange={(event) => cambiar({ hasta: event.target.value })}
        error={rangoInvalido ? 'Debe ser igual o posterior a la fecha inicial.' : undefined}
      />
      <Selector
        label="Tipo de vehículo"
        opciones={catalogos?.tipos_vehiculo ?? []}
        opcionVacia="Todos los tipos"
        value={tipo}
        onChange={(valor) => cambiar({ tipo_vehiculo: valor })}
      />
      <Selector
        label="Combustible"
        opciones={catalogos?.tipos_combustible ?? []}
        opcionVacia="Todos los combustibles"
        value={combustible}
        onChange={(valor) => cambiar({ tipo_combustible: valor })}
      />
    </div>
  );

  if (error && !cargando) {
    return (
      <>
        <PageHeader titulo="Panel de emisiones" />
        {filtros}
        <Notice tono="peligro" titulo="No pudimos cargar el panel" role="alert">
          <p>{error.message}</p>
          <Button variant="enlacePeligro" onClick={() => setRecarga((valor) => valor + 1)}>
            Reintentar
          </Button>
        </Notice>
      </>
    );
  }

  if (!panel) {
    return (
      <>
        <PageHeader titulo="Panel de emisiones" />
        {filtros}
        <span className="skeleton" style={{ height: 320 }} aria-busy="true" />
      </>
    );
  }

  const { resumen, acumulado_anio: anio, flota } = panel;
  const periodo = `del ${formatOnlyDate(panel.periodo.desde)} al ${formatOnlyDate(panel.periodo.hasta)}`;
  const datos = [
    { etiqueta: 'Viajes completados', valor: formatNumber(resumen.viajes, 0) },
    { etiqueta: 'Kilómetros recorridos', valor: formatNumber(resumen.km) },
    { etiqueta: `Acumulado de ${panel.periodo.hasta.slice(0, 4)}`, valor: co2Legible(anio.co2_kg) },
    {
      etiqueta: 'Vehículos activos',
      valor: formatNumber(flota.vehiculos_activos, 0),
      nota: `${formatNumber(flota.electricos_hibridos_activos, 0)} eléctricos o híbridos`,
    },
  ];

  return (
    <>
      <PageHeader titulo="Panel de emisiones" />
      {filtros}

      {resumen.viajes === 0 ? (
        <EmptyState
          titulo="Sin viajes completados en este periodo"
          accion={
            <Button variant="secundario" onClick={() => cambiar({ desde: '', hasta: '', tipo_vehiculo: '', tipo_combustible: '' })}>
              Ver el año en curso
            </Button>
          }
        >
          Cambia las fechas o los filtros para ver las emisiones.
        </EmptyState>
      ) : (
        <div aria-busy={cargando}>
          <section className={styles.principal}>
            <div>
              <div className={styles.titular}>
                <p className={styles.cifra}>
                  {resumen.co2_kg >= 1000 ? formatNumber(resumen.co2_toneladas) : formatNumber(resumen.co2_kg)}
                  <small>{resumen.co2_kg >= 1000 ? ' t' : ' kg'}</small>
                </p>
                <div>
                  <p className={styles.contexto}>de CO₂ {periodo}</p>
                  <p className={styles.detalle}>{variacionTexto(panel)}</p>
                </div>
              </div>
              <Tendencia meses={panel.tendencia} />
            </div>
            <Metrics items={datos} />
          </section>

          <div className={styles.desglose}>
            <Barras
              titulo="CO₂ por combustible (kg)"
              filas={panel.por_combustible.map((g) => [nombreCombustible(g.grupo), g.co2_kg])}
            />
            <Barras
              titulo="CO₂ por tipo de vehículo (kg)"
              filas={panel.por_tipo_vehiculo.map((g) => [nombreTipo(g.grupo), g.co2_kg])}
            />
          </div>

          <div className={styles.desglose}>
            <Barras titulo="Vehículos que más emiten (kg)" filas={panel.top_vehiculos.map((v) => [v.placa, v.co2_kg])} />
            <Barras
              ancho
              titulo="Rutas con más emisiones (kg)"
              filas={(rutas ?? []).map((r) => [`${r.origen} › ${r.destino}`, r.co2_kg])}
            />
          </div>

          <section>
            <div className={styles.tituloTabla}>
              <h2 className={styles.seccion}>Viajes recientes</h2>
              <Link to="/viajes?estado=completado">Ver todos los viajes</Link>
            </div>
            <div className={styles.envoltura}>
              <table className={styles.tabla}>
                <thead>
                  <tr>
                    <th scope="col">Placa</th>
                    <th scope="col">Ruta</th>
                    <th scope="col">Salida</th>
                    <th scope="col" className={styles.derecha}>
                      Distancia (km)
                    </th>
                    <th scope="col" className={styles.derecha}>
                      CO₂ (kg)
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {(recientes ?? []).map((viaje) => (
                    <tr key={viaje.viaje_id}>
                      <th scope="row">
                        <Link to={`/viajes/${viaje.viaje_id}`}>{viaje.placa}</Link>
                      </th>
                      <td>
                        <RouteBoard origen={viaje.origen} destino={viaje.destino} />
                      </td>
                      <td className={styles.tenue}>{formatDateTime(viaje.salida_en)}</td>
                      <td className={styles.derecha}>
                        <span className={styles.distancia}>
                          <DistanceLine km={viaje.distancia_km} />
                          {formatNumber(viaje.distancia_km)}
                        </span>
                      </td>
                      <td className={styles.derecha}>
                        <strong>{formatNumber(viaje.co2_kg)}</strong>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>
        </div>
      )}
    </>
  );
}
