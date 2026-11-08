import { Link } from 'react-router-dom';
import { Area, AreaChart, CartesianGrid, Dot, LabelList, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { DistanceLine, Metrics, PageHeader, RouteBoard, StatusTag } from '../components/ui';
import { formatNumber } from '../utils/format';
import styles from './Panel.module.css';

const TENDENCIA = [
  { mes: 'may', co2: 16.9 },
  { mes: 'jun', co2: 17.6 },
  { mes: 'jul', co2: 18.2 },
  { mes: 'ago', co2: 19.4 },
  { mes: 'sep', co2: 19.2 },
  { mes: 'oct', co2: 18.43 },
];

const DATOS = [
  { etiqueta: 'Viajes completados', valor: '312' },
  { etiqueta: 'Kilómetros recorridos', valor: '36,540.00' },
  { etiqueta: 'Acumulado de 2026', valor: '168.40 t' },
  { etiqueta: 'Vehículos activos', valor: '7', nota: '2 eléctricos o híbridos' },
  { etiqueta: 'Huella auditada de septiembre', valor: '19,204.30 kg', nota: 'Versión oficial. No se suma a la operación.' },
];

const POR_COMBUSTIBLE = [
  ['Diésel', 15210.4],
  ['Gasolina', 1905.2],
  ['Híbrido', 1012.6],
  ['Eléctrico', 298.5],
];

const POR_TIPO = [
  ['Camión', 13402.1],
  ['Furgoneta', 3119.4],
  ['Camioneta', 1905.2],
];

const VIAJES = [
  { placa: 'C 48213', origen: 'San Salvador', destino: 'Santa Ana', salida: '05/11, 07:30', km: 65, co2: 55.25 },
  { placa: 'C 30952', origen: 'San Salvador', destino: 'Sonsonate', salida: '04/11, 14:10', km: 68, co2: 14.96 },
  { placa: 'C 51877', origen: 'Acajutla', destino: 'San Miguel', salida: '04/11, 06:00', km: 186, co2: 158.1 },
  { placa: 'P 627104', origen: 'Santa Ana', destino: 'Metapán', salida: '03/11, 09:15', km: 47, co2: 15.04 },
];

const Punto = ({ cx, cy, index }) => (
  <Dot
    cx={cx}
    cy={cy}
    r={index === TENDENCIA.length - 1 ? 6 : 4}
    fill={index === TENDENCIA.length - 1 ? 'var(--hoja-600)' : 'var(--bosque-700)'}
    stroke="var(--blanco)"
    strokeWidth={2}
  />
);

function Tendencia() {
  return (
    <figure className={styles.grafico}>
      <div className={styles.lienzo}>
        <ResponsiveContainer width="100%" height="100%">
          <AreaChart data={TENDENCIA} margin={{ top: 24, right: 28, bottom: 0, left: 0 }}>
            <CartesianGrid vertical={false} stroke="var(--linea)" />
            <XAxis dataKey="mes" axisLine={false} tickLine={false} tick={{ fill: 'var(--tinta-media)', fontSize: 13 }} />
            <YAxis domain={[16, 20]} ticks={[16, 18, 20]} width={28} axisLine={false} tickLine={false} tick={{ fill: 'var(--tinta-media)', fontSize: 13 }} />
            <Tooltip
              isAnimationActive={false}
              formatter={(valor) => [`${formatNumber(valor)} t`, 'CO₂']}
              contentStyle={{ border: '1px solid var(--linea)', borderRadius: 3, fontFamily: 'var(--fuente)' }}
            />
            <Area
              type="linear"
              dataKey="co2"
              stroke="var(--bosque-700)"
              strokeWidth={2}
              fill="var(--tinte)"
              fillOpacity={1}
              dot={<Punto />}
              activeDot={{ r: 6 }}
              isAnimationActive={false}
            >
              <LabelList
                dataKey="co2"
                position="top"
                offset={12}
                formatter={(valor) => (valor === 19.4 || valor === 18.43 ? `${formatNumber(valor)} t` : '')}
                style={{ fill: 'var(--tinta)', fontSize: 13, fontWeight: 600 }}
              />
            </Area>
          </AreaChart>
        </ResponsiveContainer>
      </div>
      <figcaption className="visually-hidden">
        CO₂ mensual en toneladas: {TENDENCIA.map(({ mes, co2 }) => `${mes} ${formatNumber(co2)}`).join(', ')}.
      </figcaption>
    </figure>
  );
}

function Barras({ titulo, filas }) {
  const maximo = Math.max(...filas.map(([, valor]) => valor));
  return (
    <section className={styles.barras}>
      <h2 className={styles.seccion}>{titulo}</h2>
      <ul>
        {filas.map(([nombre, valor]) => (
          <li key={nombre} className={styles.barra}>
            <span>{nombre}</span>
            <span className={styles.pista}>
              <span style={{ width: `${Math.max(2, (valor / maximo) * 100)}%` }} />
            </span>
            <span className={styles.valor}>{formatNumber(valor)}</span>
          </li>
        ))}
      </ul>
    </section>
  );
}

export default function Panel() {
  return (
    <>
      <PageHeader titulo="Panel de emisiones">
        <StatusTag tono="aviso">Datos de ejemplo</StatusTag>
      </PageHeader>

      <section className={styles.principal}>
        <div>
          <div className={styles.titular}>
            <p className={styles.cifra}>
              18.43<small> t</small>
            </p>
            <div>
              <p className={styles.contexto}>de CO₂ en octubre de 2026</p>
              <p className={styles.detalle}>Intensidad 0.504 kg/km, 4.2 % menos que en septiembre</p>
            </div>
          </div>
          <Tendencia />
        </div>
        <Metrics items={DATOS} />
      </section>

      <div className={styles.desglose}>
        <Barras titulo="CO₂ por combustible (kg)" filas={POR_COMBUSTIBLE} />
        <Barras titulo="CO₂ por tipo de vehículo (kg)" filas={POR_TIPO} />
      </div>

      <section>
        <div className={styles.tituloTabla}>
          <h2 className={styles.seccion}>Viajes recientes</h2>
          <Link to="/vehiculos">Ver flota</Link>
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
              {VIAJES.map((viaje) => (
                <tr key={viaje.salida}>
                  <th scope="row">{viaje.placa}</th>
                  <td>
                    <RouteBoard origen={viaje.origen} destino={viaje.destino} />
                  </td>
                  <td className={styles.tenue}>{viaje.salida}</td>
                  <td className={styles.derecha}>
                    <span className={styles.distancia}>
                      <DistanceLine km={viaje.km} />
                      {formatNumber(viaje.km)}
                    </span>
                  </td>
                  <td className={styles.derecha}>
                    <strong>{formatNumber(viaje.co2)}</strong>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </>
  );
}
