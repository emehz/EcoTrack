import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { getChofer } from '../api/endpoints';
import { useAuth } from '../auth/useAuth';
import { CambiarEstadoChofer } from '../components/CambiarEstadoChofer';
import { Button, EmptyState, PageHeader, StatusTag } from '../components/ui';
import { formatDate, formatNumber } from '../utils/format';
import { ESTADOS_CHOFER, nombreCompleto } from '../utils/choferes';
import styles from '../components/Detalle.module.css';

export default function ChoferDetalle() {
  const { id } = useParams();
  const { puedeEscribir } = useAuth();
  const [respuesta, setRespuesta] = useState({ clave: null });
  const [recarga, setRecarga] = useState(0);
  const [cambiando, setCambiando] = useState(false);
  const clave = `${id}-${recarga}`;

  useEffect(() => {
    const controller = new AbortController();
    getChofer(id, controller.signal)
      .then(({ data }) => setRespuesta({ clave, data }))
      .catch((falla) => {
        if (falla.name !== 'AbortError') setRespuesta({ clave, error: falla });
      });
    return () => controller.abort();
  }, [clave, id]);

  const detalle = respuesta.data;

  if (respuesta.error && !detalle) {
    const noExiste = respuesta.error.status === 404;
    return (
      <EmptyState
        titulo={noExiste ? 'Este chofer no existe' : 'No pudimos cargar el chofer'}
        accion={<Button variant="secundario" to="/choferes">Volver a choferes</Button>}
      >
        {noExiste ? 'Revisa la dirección o búscalo en la lista de choferes.' : respuesta.error.message}
      </EmptyState>
    );
  }

  if (!detalle) {
    return (
      <div className={styles.cargando} aria-busy="true">
        <span className="skeleton" style={{ height: 42, width: 260 }} />
        <span className="skeleton" style={{ height: 180 }} />
      </div>
    );
  }

  const { chofer } = detalle;
  const estado = ESTADOS_CHOFER[chofer.estado];

  return (
    <>
      <PageHeader titulo={nombreCompleto(chofer)} migas={[{ texto: 'Choferes', to: '/choferes' }, { texto: nombreCompleto(chofer) }]}>
        <StatusTag tono={estado.tono}>{estado.texto}</StatusTag>
        {puedeEscribir && (
          <>
            <Button variant="secundario" to={`/choferes/${id}/editar`}>
              Editar
            </Button>
            <Button variant="secundario" onClick={() => setCambiando(true)}>
              Cambiar estado
            </Button>
          </>
        )}
      </PageHeader>

      <div className={styles.cuerpo}>
        <div className={styles.columna}>
          <section>
            <h2 className={styles.seccion}>Viajes realizados</h2>
            <div className={styles.cifras}>
              <div>
                <p className={styles.cifra}>{formatNumber(detalle.viajes_completados, 0)}</p>
                <p className={styles.nota}>viajes completados</p>
              </div>
              <div>
                <p className={styles.cifra}>{formatNumber(detalle.km_recorridos)}</p>
                <p className={styles.nota}>km recorridos</p>
              </div>
              <div>
                <p className={styles.cifra}>{detalle.ultimo_viaje_en ? formatDate(detalle.ultimo_viaje_en) : '—'}</p>
                <p className={styles.nota}>último viaje</p>
              </div>
            </div>
          </section>
          {chofer.estado !== 'activo' && (
            <p className={styles.nota}>Este chofer no puede asignarse a viajes nuevos mientras no esté activo.</p>
          )}
        </div>

        <aside className={styles.panel}>
          <h2 className={styles.tituloPanel}>Datos del chofer</h2>
          <dl className={styles.lista}>
            <div>
              <dt>Nombres</dt>
              <dd>{chofer.nombres}</dd>
            </div>
            <div>
              <dt>Apellidos</dt>
              <dd>{chofer.apellidos}</dd>
            </div>
            <div>
              <dt>Licencia</dt>
              <dd>{chofer.numero_licencia}</dd>
            </div>
            <div>
              <dt>Teléfono</dt>
              <dd>{chofer.telefono ?? '—'}</dd>
            </div>
          </dl>
        </aside>
      </div>

      {cambiando && (
        <CambiarEstadoChofer
          chofer={chofer}
          onCerrar={() => setCambiando(false)}
          onCambiado={() => {
            setCambiando(false);
            setRecarga((valor) => valor + 1);
          }}
        />
      )}
    </>
  );
}
