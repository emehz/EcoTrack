import { useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { toast } from 'sonner';
import { cambiarEstadoHallazgo, cerrarAuditoria, getAuditoria, iniciarAuditoria, registrarHallazgo } from '../api/endpoints';
import { useAuth } from '../auth/useAuth';
import { AreaTexto, DialogoFormulario } from '../components/DialogoFormulario';
import { Selector } from '../components/Selector';
import { Button, EmptyState, Field, Notice, PageHeader, StatusTag } from '../components/ui';
import { formatDateTime, formatOnlyDate } from '../utils/format';
import {
  CONTROLES,
  ESTADOS_AUDITORIA,
  ESTADOS_HALLAZGO,
  RESULTADOS,
  SEVERIDADES,
  esGrave,
  opcionesDe,
} from '../utils/auditorias';
import listado from '../components/Listado.module.css';
import styles from '../components/Detalle.module.css';

const HALLAZGO_VACIO = { severidad: '', fila_numero: '', campo: '', descripcion: '', valor_encontrado: '', valor_esperado: '' };

const erroresDe = (falla) => Object.fromEntries((falla.details ?? []).map(({ campo, mensaje }) => [campo, mensaje]));

export default function AuditoriaDetalle() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { usuario } = useAuth();
  const esAdmin = usuario.rol === 'admin';
  const [respuesta, setRespuesta] = useState({ clave: null });
  const [recarga, setRecarga] = useState(0);
  const [dialogo, setDialogo] = useState(null);
  const [form, setForm] = useState({});
  const [errores, setErrores] = useState({});
  const [errorGeneral, setErrorGeneral] = useState('');
  const [ocupado, setOcupado] = useState(false);
  const clave = `${id}-${recarga}`;

  useEffect(() => {
    const controller = new AbortController();
    getAuditoria(id, controller.signal)
      .then(({ data }) => setRespuesta({ clave, data }))
      .catch((falla) => {
        if (falla.name !== 'AbortError') setRespuesta({ clave, error: falla });
      });
    return () => controller.abort();
  }, [clave, id]);

  const abrir = (tipo, valores, extra = {}) => {
    setDialogo({ tipo, ...extra });
    setForm(valores);
    setErrores({});
    setErrorGeneral('');
  };

  const actualizar = (campo, valor) => setForm((actual) => ({ ...actual, [campo]: valor }));

  const guardar = async (accion, exito) => {
    setOcupado(true);
    try {
      const resultado = await accion();
      setDialogo(null);
      toast.success(exito);
      setRecarga((valor) => valor + 1);
      return resultado;
    } catch (falla) {
      const porCampo = erroresDe(falla);
      setErrores(porCampo);
      if (Object.keys(porCampo).length === 0) setErrorGeneral(falla.message);
      return null;
    } finally {
      setOcupado(false);
    }
  };

  const texto = (valor) => (valor?.trim() ? valor.trim() : null);

  const enviar = async () => {
    if (dialogo.tipo === 'hallazgo') {
      await guardar(
        () =>
          registrarHallazgo(id, {
            severidad: form.severidad || null,
            fila_numero: form.fila_numero ? Number(form.fila_numero) : null,
            campo: texto(form.campo),
            descripcion: texto(form.descripcion),
            valor_encontrado: texto(form.valor_encontrado),
            valor_esperado: texto(form.valor_esperado),
          }),
        'Hallazgo registrado',
      );
    } else if (dialogo.tipo === 'estado') {
      await guardar(
        () =>
          cambiarEstadoHallazgo(id, dialogo.hallazgo.hallazgo_auditoria_id, {
            estado: form.estado || null,
            justificacion: texto(form.justificacion),
            version_correccion: form.version_correccion ? Number(form.version_correccion) : null,
          }),
        'Estado del hallazgo actualizado',
      );
    } else if (dialogo.tipo === 'cerrar') {
      await guardar(
        () => cerrarAuditoria(id, { resultado: form.resultado || null, conclusion: texto(form.conclusion) }),
        'Auditoría cerrada',
      );
    } else if (dialogo.tipo === 'ciclo') {
      const resultado = await guardar(
        () => iniciarAuditoria(respuesta.data.reporte_viaje_id, { motivo_ciclo: texto(form.motivo_ciclo) }),
        'Nuevo ciclo de auditoría abierto',
      );
      if (resultado) navigate(`/auditorias/${resultado.data.auditoria_reporte_id}`);
    }
  };

  const auditoria = respuesta.data;

  if (respuesta.error && !auditoria) {
    const noExiste = respuesta.error.status === 404;
    return (
      <EmptyState
        titulo={noExiste ? 'Esta auditoría no existe' : 'No pudimos cargar la auditoría'}
        accion={<Button variant="secundario" to="/auditorias">Volver a auditorías</Button>}
      >
        {noExiste ? 'Revisa la dirección o búscala en la lista de auditorías.' : respuesta.error.message}
      </EmptyState>
    );
  }

  if (!auditoria) {
    return (
      <div className={styles.cargando} aria-busy="true">
        <span className="skeleton" style={{ height: 42, width: 260 }} />
        <span className="skeleton" style={{ height: 180 }} />
      </div>
    );
  }

  const abierta = auditoria.estado === 'en_revision';
  const estado = auditoria.resultado ? RESULTADOS[auditoria.resultado] : ESTADOS_AUDITORIA[auditoria.estado];
  const abiertos = auditoria.hallazgos.filter((hallazgo) => hallazgo.estado === 'abierto');
  const gravesAbiertos = abiertos.filter(esGrave).length;
  const periodo = `${formatOnlyDate(auditoria.periodo_desde)} – ${formatOnlyDate(auditoria.periodo_hasta)}`;
  const estadosPermitidos = opcionesDe(ESTADOS_HALLAZGO).filter(({ valor }) => esAdmin || valor !== 'riesgo_aceptado');

  return (
    <>
      <PageHeader
        titulo="Auditoría"
        subtitulo={`Reporte ${periodo} · versión ${auditoria.version_auditada.numero_version}`}
        migas={[{ texto: 'Auditorías', to: '/auditorias' }, { texto: periodo }]}
      >
        <StatusTag tono={estado.tono}>{estado.texto}</StatusTag>
        {abierta && (
          <>
            <Button variant="secundario" onClick={() => abrir('hallazgo', HALLAZGO_VACIO)}>
              Registrar hallazgo
            </Button>
            <Button onClick={() => abrir('cerrar', { resultado: '', conclusion: '' })}>Cerrar auditoría</Button>
          </>
        )}
        {!abierta && esAdmin && auditoria.estado_reporte === 'auditado' && (
          <Button variant="secundario" onClick={() => abrir('ciclo', { motivo_ciclo: '' })}>
            Abrir nuevo ciclo
          </Button>
        )}
      </PageHeader>

      <div className={styles.cuerpo}>
        <div className={styles.columna}>
          {abierta && gravesAbiertos > 0 && (
            <Notice
              tono="aviso"
              titulo={
                gravesAbiertos === 1
                  ? '1 hallazgo crítico o alto abierto'
                  : `${gravesAbiertos} hallazgos críticos o altos abiertos`
              }
            >
              Mientras sigan abiertos, la auditoría solo puede cerrarse como rechazada.
            </Notice>
          )}

          <section>
            <h2 className={styles.seccion}>
              Hallazgos
              <span className={styles.secundario}>
                {abiertos.length} abiertos de {auditoria.hallazgos.length}
              </span>
            </h2>
            {auditoria.hallazgos.length === 0 ? (
              <EmptyState titulo="Sin hallazgos">
                Los controles automáticos no encontraron diferencias. Puedes registrar hallazgos de tu revisión manual.
              </EmptyState>
            ) : (
              <div className={listado.envoltura}>
                <table className={listado.tabla}>
                  <thead>
                    <tr>
                      <th scope="col">Severidad</th>
                      <th scope="col">Hallazgo</th>
                      <th scope="col">Fila</th>
                      <th scope="col">Encontrado / esperado</th>
                      <th scope="col">Estado</th>
                      {abierta && (
                        <th scope="col">
                          <span className="visually-hidden">Acciones</span>
                        </th>
                      )}
                    </tr>
                  </thead>
                  <tbody>
                    {auditoria.hallazgos.map((hallazgo) => {
                      const severidad = SEVERIDADES[hallazgo.severidad];
                      const estadoHallazgo = ESTADOS_HALLAZGO[hallazgo.estado];
                      return (
                        <tr key={hallazgo.hallazgo_auditoria_id}>
                          <td>
                            <StatusTag tono={severidad.tono}>{severidad.texto}</StatusTag>
                          </td>
                          <td className={listado.ajustar}>
                            {hallazgo.descripcion}
                            <span className={styles.secundario}>
                              {hallazgo.origen === 'manual'
                                ? `Manual · ${hallazgo.detectado_por ?? ''}`
                                : CONTROLES[hallazgo.control] ?? hallazgo.control}
                              {hallazgo.campo ? ` · ${hallazgo.campo}` : ''}
                            </span>
                          </td>
                          <td className={listado.derecha}>{hallazgo.fila_numero ?? '—'}</td>
                          <td className={listado.ajustar}>
                            {hallazgo.valor_encontrado || hallazgo.valor_esperado ? (
                              <>
                                {hallazgo.valor_encontrado ?? '—'}
                                <span className={styles.secundario}>Esperado: {hallazgo.valor_esperado ?? '—'}</span>
                              </>
                            ) : (
                              '—'
                            )}
                          </td>
                          <td className={listado.ajustar}>
                            <StatusTag tono={estadoHallazgo.tono}>{estadoHallazgo.texto}</StatusTag>
                            {hallazgo.justificacion && (
                              <span className={styles.secundario}>
                                {hallazgo.justificacion} · {hallazgo.resuelto_por}
                              </span>
                            )}
                          </td>
                          {abierta && (
                            <td>
                              <Button
                                variant="enlace"
                                onClick={() =>
                                  abrir('estado', { estado: hallazgo.estado, justificacion: '', version_correccion: '' }, { hallazgo })
                                }
                              >
                                Cambiar estado
                              </Button>
                            </td>
                          )}
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </section>

          {auditoria.conclusion && (
            <section>
              <h2 className={styles.seccion}>Conclusión</h2>
              <p className={styles.texto}>{auditoria.conclusion}</p>
            </section>
          )}
        </div>

        <aside className={styles.panel}>
          <h2 className={styles.tituloPanel}>Datos de la auditoría</h2>
          <dl className={styles.lista}>
            <div>
              <dt>Reporte</dt>
              <dd>
                <Link to={`/reportes/${auditoria.reporte_viaje_id}`}>{periodo}</Link>
              </dd>
            </div>
            <div>
              <dt>Versión auditada</dt>
              <dd title={auditoria.version_auditada.hash_sha256}>
                v{auditoria.version_auditada.numero_version} · {auditoria.version_auditada.hash_sha256.slice(0, 12)}…
              </dd>
            </div>
            {auditoria.version_final && (
              <div>
                <dt>Versión oficial</dt>
                <dd>v{auditoria.version_final.numero_version}</dd>
              </div>
            )}
            <div>
              <dt>Auditor</dt>
              <dd>{auditoria.auditor}</dd>
            </div>
            <div>
              <dt>Iniciada</dt>
              <dd>{formatDateTime(auditoria.iniciado_en)}</dd>
            </div>
            <div>
              <dt>Cerrada</dt>
              <dd>{formatDateTime(auditoria.cerrado_en)}</dd>
            </div>
          </dl>
          {auditoria.alcance && (
            <p className={styles.texto}>
              <strong>Alcance:</strong> {auditoria.alcance}
            </p>
          )}
          {auditoria.motivo_ciclo && (
            <p className={styles.texto}>
              <strong>Motivo del nuevo ciclo:</strong> {auditoria.motivo_ciclo}
            </p>
          )}
        </aside>
      </div>

      <DialogoFormulario
        abierto={dialogo?.tipo === 'hallazgo'}
        titulo="Registrar hallazgo"
        descripcion="Anota lo que encontraste al revisar el reporte."
        accion="Registrar"
        ocupado={ocupado}
        error={errorGeneral}
        onEnviar={enviar}
        onCerrar={() => setDialogo(null)}
      >
        <Selector
          label="Severidad"
          opciones={opcionesDe(SEVERIDADES)}
          value={form.severidad}
          onChange={(valor) => actualizar('severidad', valor)}
          error={errores.severidad}
        />
        <Field
          label="Fila del CSV (opcional)"
          type="number"
          min="2"
          value={form.fila_numero ?? ''}
          onChange={(event) => actualizar('fila_numero', event.target.value)}
          error={errores.fila_numero}
        />
        <Field label="Campo (opcional)" value={form.campo ?? ''} onChange={(event) => actualizar('campo', event.target.value)} />
        <AreaTexto
          label="Descripción"
          value={form.descripcion ?? ''}
          onChange={(event) => actualizar('descripcion', event.target.value)}
          error={errores.descripcion}
        />
        <Field
          label="Valor encontrado (opcional)"
          value={form.valor_encontrado ?? ''}
          onChange={(event) => actualizar('valor_encontrado', event.target.value)}
        />
        <Field
          label="Valor esperado (opcional)"
          value={form.valor_esperado ?? ''}
          onChange={(event) => actualizar('valor_esperado', event.target.value)}
        />
      </DialogoFormulario>

      <DialogoFormulario
        abierto={dialogo?.tipo === 'estado'}
        titulo="Cambiar estado del hallazgo"
        descripcion={dialogo?.hallazgo?.descripcion}
        accion="Guardar"
        ocupado={ocupado}
        error={errorGeneral}
        onEnviar={enviar}
        onCerrar={() => setDialogo(null)}
      >
        <Selector
          label="Estado"
          opciones={estadosPermitidos}
          value={form.estado}
          onChange={(valor) => actualizar('estado', valor)}
          error={errores.estado}
          help={esAdmin ? undefined : 'Solo un administrador puede aceptar el riesgo de un hallazgo.'}
        />
        {form.estado === 'corregido' && (
          <Field
            label="Versión que contiene la corrección"
            type="number"
            min="1"
            value={form.version_correccion ?? ''}
            onChange={(event) => actualizar('version_correccion', event.target.value)}
            error={errores.version_correccion}
          />
        )}
        {form.estado !== 'abierto' && (
          <AreaTexto
            label="Justificación"
            value={form.justificacion ?? ''}
            onChange={(event) => actualizar('justificacion', event.target.value)}
            error={errores.justificacion}
          />
        )}
      </DialogoFormulario>

      <DialogoFormulario
        abierto={dialogo?.tipo === 'cerrar'}
        titulo="Cerrar auditoría"
        descripcion="Una auditoría cerrada no se puede modificar. Al aprobar, la versión auditada queda como oficial."
        accion="Cerrar auditoría"
        ocupado={ocupado}
        error={errorGeneral}
        onEnviar={enviar}
        onCerrar={() => setDialogo(null)}
      >
        <Selector
          label="Resultado"
          opciones={opcionesDe(RESULTADOS)}
          value={form.resultado}
          onChange={(valor) => actualizar('resultado', valor)}
          error={errores.resultado}
        />
        <AreaTexto
          label="Conclusión"
          value={form.conclusion ?? ''}
          onChange={(event) => actualizar('conclusion', event.target.value)}
          error={errores.conclusion}
        />
      </DialogoFormulario>

      <DialogoFormulario
        abierto={dialogo?.tipo === 'ciclo'}
        titulo="Abrir nuevo ciclo de auditoría"
        descripcion="La auditoría anterior se conserva. El nuevo ciclo revisa la última versión calculada."
        accion="Abrir ciclo"
        ocupado={ocupado}
        error={errorGeneral}
        onEnviar={enviar}
        onCerrar={() => setDialogo(null)}
      >
        <AreaTexto
          label="Motivo"
          value={form.motivo_ciclo ?? ''}
          onChange={(event) => actualizar('motivo_ciclo', event.target.value)}
          error={errores.motivo_ciclo}
        />
      </DialogoFormulario>
    </>
  );
}
