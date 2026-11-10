import { Button } from './ui';
import { formatNumber } from '../utils/format';
import styles from './Paginacion.module.css';

export function Paginacion({ meta, onCambiar }) {
  if (!meta || meta.total === 0) return null;
  const desde = (meta.page - 1) * meta.limit + 1;
  const hasta = Math.min(meta.page * meta.limit, meta.total);

  return (
    <div className={styles.paginacion}>
      <span>
        {formatNumber(desde, 0)}–{formatNumber(hasta, 0)} de {formatNumber(meta.total, 0)}
      </span>
      {meta.total_pages > 1 && (
        <span className={styles.paginas}>
          <Button variant="enlace" disabled={meta.page <= 1} onClick={() => onCambiar(meta.page - 1)}>
            Anterior
          </Button>
          <Button variant="enlace" disabled={meta.page >= meta.total_pages} onClick={() => onCambiar(meta.page + 1)}>
            Siguiente
          </Button>
        </span>
      )}
    </div>
  );
}
