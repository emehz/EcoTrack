import { forwardRef, useId } from 'react';
import { clsx } from 'clsx';
import { Link } from 'react-router-dom';
import styles from './ui.module.css';

export function Button({ variant = 'primario', icon: Icon, children, to, className, ...props }) {
  const classes = clsx(styles.button, styles[variant], className);
  const content = (
    <>
      {Icon && <Icon size={16} strokeWidth={1.75} aria-hidden="true" />}
      {children}
    </>
  );
  return to ? (
    <Link to={to} className={classes} {...props}>
      {content}
    </Link>
  ) : (
    <button type="button" className={classes} {...props}>
      {content}
    </button>
  );
}

export function StatusTag({ tono = 'neutro', children }) {
  return <span className={clsx(styles.tag, styles[`tag_${tono}`])}>{children}</span>;
}

const ESTADOS = {
  activo: { tono: 'exito', texto: 'Activo' },
  mantenimiento: { tono: 'aviso', texto: 'Mantenimiento' },
  inactivo: { tono: 'neutro', texto: 'Inactivo' },
};

export function EstadoVehiculo({ estado }) {
  const { tono, texto } = ESTADOS[estado] ?? ESTADOS.inactivo;
  return <StatusTag tono={tono}>{texto}</StatusTag>;
}

export const Field = forwardRef(function Field({ label, help, error, ...props }, ref) {
  const id = useId();
  const messageId = `${id}-mensaje`;
  const control = {
    id,
    ref,
    'aria-invalid': error ? true : undefined,
    'aria-describedby': error || help ? messageId : undefined,
    className: clsx(styles.control, error && styles.controlError),
    ...props,
  };

  return (
    <div className={styles.field}>
      <label htmlFor={id} className={styles.label}>
        {label}
      </label>
      <input {...control} />
      {(error || help) && (
        <p id={messageId} className={error ? styles.errorText : styles.helpText}>
          {error || help}
        </p>
      )}
    </div>
  );
});

export function Notice({ tono = 'info', titulo, children, ...props }) {
  return (
    <div className={clsx(styles.notice, styles[`tag_${tono}`])} {...props}>
      {titulo && <p className={styles.noticeTitle}>{titulo}</p>}
      <div>{children}</div>
    </div>
  );
}

export function PageHeader({ titulo, subtitulo, migas, children }) {
  return (
    <header className={styles.header}>
      <title>{`${titulo} · EcoTrack`}</title>
      {migas && (
        <nav aria-label="Ruta de navegación" className={styles.crumbs}>
          {migas.map(({ texto, to }) => (
            <span key={texto}>
              {to ? <Link to={to}>{texto}</Link> : <span aria-current="page">{texto}</span>}
            </span>
          ))}
        </nav>
      )}
      <div className={styles.headerRow}>
        <div>
          <h1 className={styles.title}>{titulo}</h1>
          {subtitulo && <p className={styles.subtitle}>{subtitulo}</p>}
        </div>
        {children && <div className={styles.headerActions}>{children}</div>}
      </div>
    </header>
  );
}

export function EmptyState({ titulo, children, accion }) {
  return (
    <div className={styles.empty}>
      <h2 className={styles.emptyTitle}>{titulo}</h2>
      <p>{children}</p>
      {accion}
    </div>
  );
}

export function RouteBoard({ origen, destino }) {
  return (
    <span className={styles.board}>
      {origen}
      <span className={styles.boardArrow} aria-label="a">
        ›
      </span>
      {destino}
    </span>
  );
}

const ESCALA_KM = 186;
const LARGO_LINEA = 70;

export function DistanceLine({ km }) {
  const largo = Math.max(10, (km / ESCALA_KM) * LARGO_LINEA);
  return (
    <svg className={styles.line} width={LARGO_LINEA + 6} height="10" aria-hidden="true">
      <line x1="3" y1="5" x2={3 + largo} y2="5" stroke="var(--bosque-700)" strokeWidth="2" strokeDasharray="4 3" />
      <circle cx="3" cy="5" r="3" fill="var(--bosque-700)" />
      <circle cx={3 + largo} cy="5" r="3" fill="var(--hoja-600)" />
    </svg>
  );
}

export function Metrics({ items }) {
  return (
    <dl className={styles.metrics}>
      {items.map(({ etiqueta, valor, nota }) => (
        <div key={etiqueta} className={styles.metric}>
          <dt>
            {etiqueta}
            {nota && <span className={styles.metricNote}>{nota}</span>}
          </dt>
          <dd>{valor}</dd>
        </div>
      ))}
    </dl>
  );
}
