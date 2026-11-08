import { useEffect, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { toast } from 'sonner';
import { createVehiculo, getFactorVigente, getVehiculo, updateVehiculo } from '../api/endpoints';
import { useAuth } from '../auth/useAuth';
import { FactorDialog } from '../components/FactorDialog';
import { Selector } from '../components/Selector';
import { Button, EmptyState, Field, Notice, PageHeader } from '../components/ui';
import { etiquetaDe, useCatalogos } from '../hooks/useCatalogos';
import { formatFactor, formatOnlyDate } from '../utils/format';
import { normalizarPlaca } from '../utils/placa';
import styles from './VehiculoForm.module.css';

const FORMULARIO_VACIO = { placa: '', tipo_vehiculo: '', tipo_combustible: '', estado: 'activo' };
const ESTADOS = [
  { valor: 'activo', etiqueta: 'Activo' },
  { valor: 'mantenimiento', etiqueta: 'Mantenimiento' },
];
const ESTADOS_CON_INACTIVO = [{ valor: 'inactivo', etiqueta: 'Inactivo' }, ...ESTADOS];
const ETIQUETAS = { placa: 'Placa', tipo_vehiculo: 'Tipo de vehículo', tipo_combustible: 'Combustible', estado: 'Estado' };

function PanelFactor({ tipo, combustible, factorActual, catalogos }) {
  const { usuario } = useAuth();
  const seleccion = tipo && combustible ? `${tipo}|${combustible}` : null;
  const [resultado, setResultado] = useState({ seleccion: null });
  const [creando, setCreando] = useState(false);
  const combinacion = `${etiquetaDe(catalogos?.tipos_vehiculo, tipo)} con ${etiquetaDe(catalogos?.tipos_combustible, combustible)}`;

  useEffect(() => {
    if (!seleccion) return undefined;
    const controller = new AbortController();
    getFactorVigente(tipo, combustible, controller.signal)
      .then(({ data }) => setResultado({ seleccion, factor: data }))
      .catch((falla) => {
        if (falla.name !== 'AbortError') setResultado({ seleccion, error: falla });
      });
    return () => controller.abort();
  }, [seleccion, tipo, combustible]);

  const vigente = resultado.seleccion === seleccion ? resultado : null;

  return (
    <aside className={styles.panel} aria-live="polite">
      <h2 className={styles.panelTitulo}>Factor de emisión aplicable</h2>
      {!seleccion && <p className={styles.panelTexto}>Aparece al elegir tipo y combustible. Con él se calculan los viajes.</p>}
      {seleccion && !vigente && <span className="skeleton" style={{ height: 34, width: 140 }} />}
      {vigente?.factor && (
        <>
          <p className={styles.panelCifra}>{formatFactor(vigente.factor.factor_co2_km)}</p>
          <p className={styles.panelTexto}>kg de CO₂ por km</p>
          <dl className={styles.panelDatos}>
            <div>
              <dt>Vigente desde</dt>
              <dd>{formatOnlyDate(vigente.factor.vigente_desde)}</dd>
            </div>
            {vigente.factor.fuente && (
              <div>
                <dt>Fuente</dt>
                <dd>{vigente.factor.fuente}</dd>
              </div>
            )}
            {factorActual !== undefined && factorActual !== vigente.factor.factor_co2_km && (
              <div>
                <dt>Factor actual</dt>
                <dd>{formatFactor(factorActual)}</dd>
              </div>
            )}
          </dl>
          {factorActual !== undefined && factorActual !== vigente.factor.factor_co2_km && (
            <p className={styles.panelNota}>Los viajes ya registrados conservan su factor.</p>
          )}
        </>
      )}
      {vigente?.error && (
        <>
          <p className={styles.panelAviso}>
            {vigente.error.status === 404
              ? `No hay factor vigente para ${combinacion}. Sin él no se puede guardar el vehículo.`
              : vigente.error.message}
          </p>
          {vigente.error.status === 404 && usuario?.rol === 'admin' && (
            <Button variant="secundario" className={styles.panelAccion} onClick={() => setCreando(true)}>
              Crear factor de CO₂
            </Button>
          )}
        </>
      )}
      <FactorDialog
        abierto={creando}
        tipo={tipo}
        combustible={combustible}
        combinacion={combinacion}
        onCerrar={() => setCreando(false)}
        onCreado={(factor) => {
          setCreando(false);
          setResultado({ seleccion, factor });
          toast.success('Factor creado', { description: `${combinacion}: ${formatFactor(factor.factor_co2_km)} kg/km` });
        }}
      />
    </aside>
  );
}

export default function VehiculoForm() {
  const { id } = useParams();
  const editando = Boolean(id);
  const navigate = useNavigate();
  const { catalogos, error: errorCatalogos } = useCatalogos();
  const resumenRef = useRef(null);

  const [form, setForm] = useState(FORMULARIO_VACIO);
  const [original, setOriginal] = useState(null);
  const [errorCarga, setErrorCarga] = useState(null);
  const [errores, setErrores] = useState({});
  const [errorGeneral, setErrorGeneral] = useState('');
  const [guardando, setGuardando] = useState(false);

  useEffect(() => {
    if (!editando) return undefined;
    const controller = new AbortController();
    getVehiculo(id, controller.signal)
      .then(({ data }) => {
        const { vehiculo } = data;
        setOriginal(vehiculo);
        setForm({
          placa: vehiculo.placa,
          tipo_vehiculo: vehiculo.tipo_vehiculo,
          tipo_combustible: vehiculo.tipo_combustible,
          estado: vehiculo.estado,
        });
      })
      .catch((falla) => {
        if (falla.name !== 'AbortError') setErrorCarga(falla);
      });
    return () => controller.abort();
  }, [editando, id]);

  const hayErrores = Object.keys(errores).length > 0;
  useEffect(() => {
    if (hayErrores || errorGeneral) resumenRef.current?.focus();
  }, [hayErrores, errorGeneral]);

  const cambiar = (campo) => (valor) => {
    setForm((actual) => ({ ...actual, [campo]: valor }));
    setErrores(({ [campo]: _quitado, ...resto }) => resto);
  };

  const validarLocal = () => {
    const nuevos = {};
    if (!editando && !normalizarPlaca(form.placa)) {
      nuevos.placa = 'Escribe una placa válida de El Salvador, por ejemplo C 48213 o P 123456.';
    }
    if (!form.tipo_vehiculo) nuevos.tipo_vehiculo = 'Elige un tipo de vehículo.';
    if (!form.tipo_combustible) nuevos.tipo_combustible = 'Elige un combustible.';
    return nuevos;
  };

  const enviar = async (event) => {
    event.preventDefault();
    const nuevos = validarLocal();
    setErrores(nuevos);
    setErrorGeneral('');
    if (Object.keys(nuevos).length > 0) return;

    setGuardando(true);
    try {
      const { data } = editando
        ? await updateVehiculo(id, {
            tipo_vehiculo: form.tipo_vehiculo,
            tipo_combustible: form.tipo_combustible,
            estado: form.estado === 'inactivo' ? undefined : form.estado,
          })
        : await createVehiculo(form);

      toast.success(editando ? 'Cambios guardados' : 'Vehículo creado', { description: data.placa });
      navigate(editando ? `/vehiculos/${id}` : '/vehiculos', { state: { resaltar: data.vehiculo_id } });
    } catch (falla) {
      const delServidor = Object.fromEntries((falla.details ?? []).map(({ campo, mensaje }) => [campo, mensaje]));
      setErrores(delServidor);
      if (Object.keys(delServidor).length === 0) setErrorGeneral(falla.message);
      setGuardando(false);
    }
  };

  const titulo = editando ? `Editar ${original?.placa ?? 'vehículo'}` : 'Nuevo vehículo';
  const migas = [{ texto: 'Vehículos', to: '/vehiculos' }, { texto: titulo }];

  if (errorCarga) {
    return (
      <EmptyState
        titulo={errorCarga.status === 404 ? 'Este vehículo no existe' : 'No pudimos cargar el vehículo'}
        accion={<Button variant="secundario" to="/vehiculos">Volver a vehículos</Button>}
      >
        {errorCarga.status === 404 ? 'Revisa la dirección o búscalo en la lista de vehículos.' : errorCarga.message}
      </EmptyState>
    );
  }

  const listo = catalogos && (!editando || original);
  const placaNormalizada = !editando && normalizarPlaca(form.placa);

  return (
    <>
      <PageHeader titulo={titulo} migas={migas} />

      {errorCatalogos && (
        <Notice tono="peligro" titulo="No pudimos cargar las opciones del formulario" role="alert">
          {errorCatalogos.message}
        </Notice>
      )}

      {(hayErrores || errorGeneral) && (
        <div ref={resumenRef} tabIndex={-1} role="alert" className={styles.resumen}>
          <Notice tono="peligro" titulo="No se pudo guardar">
            {errorGeneral || (
              <ul>
                {Object.entries(errores).map(([campo, mensaje]) => (
                  <li key={campo}>
                    {ETIQUETAS[campo] ?? campo}: {mensaje}
                  </li>
                ))}
              </ul>
            )}
          </Notice>
        </div>
      )}

      <div className={styles.cuerpo}>
        {!listo && !errorCatalogos ? (
          <div className={styles.formulario} aria-busy="true">
            <span className="skeleton" style={{ height: 58 }} />
            <span className="skeleton" style={{ height: 58 }} />
            <span className="skeleton" style={{ height: 58 }} />
            <span className="skeleton" style={{ height: 58 }} />
          </div>
        ) : (
          <form className={styles.formulario} onSubmit={enviar} noValidate>
            <Field
              label="Placa"
              value={form.placa}
              onChange={(event) => cambiar('placa')(event.target.value)}
              disabled={editando}
              placeholder="Ej. C 48213"
              maxLength={12}
              autoComplete="off"
              autoFocus={!editando}
              error={errores.placa}
              help={
                editando
                  ? 'La placa no se modifica.'
                  : placaNormalizada
                    ? `Se guardará como ${placaNormalizada}.`
                    : 'Prefijo de clase y número: C 48213, P 123456, AB 1234.'
              }
            />
            <Selector
              label="Tipo de vehículo"
              opciones={catalogos?.tipos_vehiculo}
              value={form.tipo_vehiculo}
              onChange={cambiar('tipo_vehiculo')}
              error={errores.tipo_vehiculo}
            />
            <Selector
              label="Combustible"
              opciones={catalogos?.tipos_combustible}
              value={form.tipo_combustible}
              onChange={cambiar('tipo_combustible')}
              error={errores.tipo_combustible}
            />
            <Selector label="Estado" opciones={original?.estado === 'inactivo' ? ESTADOS_CON_INACTIVO : ESTADOS} value={form.estado} onChange={cambiar('estado')} error={errores.estado} />
            <div className={styles.acciones}>
              <Button type="submit" disabled={guardando}>
                {guardando ? 'Guardando…' : editando ? 'Guardar cambios' : 'Guardar vehículo'}
              </Button>
              <Button variant="secundario" to={editando ? `/vehiculos/${id}` : '/vehiculos'}>
                Cancelar
              </Button>
            </div>
          </form>
        )}
        <PanelFactor tipo={form.tipo_vehiculo} combustible={form.tipo_combustible} factorActual={original?.factor_co2_km} catalogos={catalogos} />
      </div>
    </>
  );
}
