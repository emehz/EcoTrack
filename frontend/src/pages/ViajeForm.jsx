import { useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { toast } from 'sonner';
import { createViaje, getChoferes, getVehiculos, getViaje, updateViaje } from '../api/endpoints';
import { Selector } from '../components/Selector';
import { Button, EmptyState, Field, Notice, PageHeader } from '../components/ui';
import { nombreCompleto } from '../utils/choferes';
import { formatNumber } from '../utils/format';
import { OPCIONES_ESTADO_NUEVO, aEntradaLocal, deEntradaLocal, leerDistancia } from '../utils/viajes';
import styles from '../components/Formulario.module.css';

const vacio = () => ({
  vehiculo_id: '',
  chofer_id: '',
  origen: '',
  destino: '',
  salida: aEntradaLocal(),
  llegada: '',
  distancia: '',
  estado: 'en_curso',
});

export default function ViajeForm() {
  const { id } = useParams();
  const editando = Boolean(id);
  const navigate = useNavigate();
  const [form, setForm] = useState(vacio);
  const [viaje, setViaje] = useState(null);
  const [vehiculos, setVehiculos] = useState(null);
  const [choferes, setChoferes] = useState(null);
  const [errorCarga, setErrorCarga] = useState(null);
  const [errores, setErrores] = useState({});
  const [errorGeneral, setErrorGeneral] = useState('');
  const [existente, setExistente] = useState(null);
  const [enviando, setEnviando] = useState(false);

  useEffect(() => {
    const controller = new AbortController();
    const { signal } = controller;
    Promise.all([
      getVehiculos({ estado: 'todos', limit: 100, sort: 'placa' }, signal),
      getChoferes({ estado: 'todos', limit: 100 }, signal),
      editando ? getViaje(id, signal) : Promise.resolve(null),
    ])
      .then(([listaVehiculos, listaChoferes, detalle]) => {
        setVehiculos(listaVehiculos.data);
        setChoferes(listaChoferes.data);
        if (detalle) {
          const { viaje: v } = detalle.data;
          setViaje(v);
          setForm({
            vehiculo_id: v.vehiculo_id,
            chofer_id: v.chofer_id ?? '',
            origen: v.origen,
            destino: v.destino,
            salida: aEntradaLocal(v.salida_en),
            llegada: v.llegada_en ? aEntradaLocal(v.llegada_en) : '',
            distancia: String(v.distancia_km),
            estado: v.estado,
          });
        }
      })
      .catch((falla) => {
        if (falla.name !== 'AbortError') setErrorCarga(falla);
      });
    return () => controller.abort();
  }, [editando, id]);

  const actualizar = (campo, valor) => setForm((actual) => ({ ...actual, [campo]: valor }));

  const conLlegada = form.estado === 'completado';

  const enviar = async (event) => {
    event.preventDefault();
    const distancia = leerDistancia(form.distancia);
    const nuevos = {};
    if (!form.vehiculo_id) nuevos.vehiculo_id = 'Elige el vehículo.';
    if (!form.origen.trim()) nuevos.origen = 'Escribe el origen.';
    if (!form.destino.trim()) nuevos.destino = 'Escribe el destino.';
    if (!form.salida) nuevos.salida_en = 'Indica la fecha y hora de salida.';
    if (distancia === null || distancia < 0) nuevos.distancia_km = 'Escribe la distancia en kilómetros.';
    if (conLlegada && !form.llegada) nuevos.llegada_en = 'Indica la fecha y hora de llegada.';
    if (conLlegada && form.llegada && form.salida && form.llegada < form.salida) {
      nuevos.llegada_en = 'La llegada no puede ser anterior a la salida.';
    }
    setErrores(nuevos);
    setErrorGeneral('');
    setExistente(null);
    if (Object.keys(nuevos).length > 0) return;

    setEnviando(true);
    const cuerpo = {
      vehiculo_id: form.vehiculo_id,
      chofer_id: form.chofer_id || null,
      origen: form.origen.trim(),
      destino: form.destino.trim(),
      salida_en: deEntradaLocal(form.salida),
      llegada_en: conLlegada ? deEntradaLocal(form.llegada) : null,
      distancia_km: distancia,
      ...(editando ? {} : { estado: form.estado }),
    };
    try {
      const { data } = editando ? await updateViaje(id, cuerpo) : await createViaje(cuerpo);
      toast.success(editando ? 'Viaje actualizado' : 'Salida registrada', {
        description: `${data.viaje.origen} › ${data.viaje.destino} · ${formatNumber(data.viaje.co2_kg)} kg de CO₂`,
      });
      navigate(`/viajes/${data.viaje.viaje_id}`);
    } catch (falla) {
      const delServidor = Object.fromEntries((falla.details ?? []).map(({ campo, mensaje }) => [campo, mensaje]));
      if (delServidor.viaje_existente) {
        setExistente(delServidor.viaje_existente);
        delete delServidor.viaje_existente;
      }
      setErrores(delServidor);
      if (Object.keys(delServidor).length === 0 || falla.status === 409) setErrorGeneral(falla.message);
      setEnviando(false);
    }
  };

  if (errorCarga) {
    return (
      <EmptyState
        titulo={errorCarga.status === 404 ? 'Este viaje no existe' : 'No pudimos cargar el formulario'}
        accion={<Button variant="secundario" to="/viajes">Volver a viajes</Button>}
      >
        {errorCarga.message}
      </EmptyState>
    );
  }

  if (viaje?.estado === 'cancelado') {
    return (
      <EmptyState
        titulo="Este viaje está cancelado"
        accion={<Button variant="secundario" to={`/viajes/${id}`}>Volver al viaje</Button>}
      >
        Un viaje cancelado ya no se puede editar.
      </EmptyState>
    );
  }

  const opcionesVehiculo = (vehiculos ?? [])
    .filter((v) => v.estado === 'activo' || v.vehiculo_id === viaje?.vehiculo_id)
    .map((v) => ({ valor: v.vehiculo_id, etiqueta: v.placa }));
  const opcionesChofer = (choferes ?? [])
    .filter((c) => c.estado === 'activo' || c.chofer_id === viaje?.chofer_id)
    .map((c) => ({ valor: c.chofer_id, etiqueta: `${nombreCompleto(c)} · ${c.numero_licencia}` }));

  const titulo = editando ? 'Editar viaje' : 'Registrar salida';
  const cargado = vehiculos && choferes && (!editando || viaje);

  return (
    <>
      <PageHeader
        titulo={titulo}
        migas={[{ texto: 'Viajes', to: '/viajes' }, ...(editando ? [{ texto: 'Viaje', to: `/viajes/${id}` }] : []), { texto: titulo }]}
      />
      {!cargado ? (
        <span className="skeleton" style={{ height: 320, maxWidth: 640 }} aria-busy="true" />
      ) : (
        <form className={styles.form} onSubmit={enviar} noValidate>
          {errorGeneral && (
            <Notice tono="peligro" role="alert">
              {errorGeneral}
              {existente && (
                <>
                  {' '}
                  <Link to={`/viajes/${existente}`}>Ver el viaje existente</Link>
                </>
              )}
            </Notice>
          )}
          <fieldset className={styles.grupo}>
            <legend className={styles.leyenda}>Vehículo y chofer</legend>
            <div className={styles.fila}>
              <Selector
                label="Vehículo"
                opciones={opcionesVehiculo}
                value={form.vehiculo_id}
                onChange={(valor) => actualizar('vehiculo_id', valor)}
                placeholder="Elige un vehículo activo"
                error={errores.vehiculo_id}
              />
              <Selector
                label="Chofer (opcional)"
                opciones={opcionesChofer}
                opcionVacia="Sin chofer"
                value={form.chofer_id}
                onChange={(valor) => actualizar('chofer_id', valor)}
                placeholder="Sin chofer"
                error={errores.chofer_id}
                help="Solo aparecen los choferes activos."
              />
            </div>
          </fieldset>
          <fieldset className={styles.grupo}>
            <legend className={styles.leyenda}>Recorrido</legend>
            <div className={styles.fila}>
              <Field
                label="Origen"
                value={form.origen}
                onChange={(event) => actualizar('origen', event.target.value)}
                error={errores.origen}
                autoComplete="off"
              />
              <Field
                label="Destino"
                value={form.destino}
                onChange={(event) => actualizar('destino', event.target.value)}
                error={errores.destino}
                autoComplete="off"
              />
            </div>
            <div className={styles.fila}>
              <Field
                label="Distancia (km)"
                inputMode="decimal"
                placeholder="64.5"
                value={form.distancia}
                onChange={(event) => actualizar('distancia', event.target.value)}
                error={errores.distancia_km}
                help="El CO₂ se calcula con el factor vigente del vehículo en la fecha de salida."
              />
              {!editando && (
                <Selector
                  label="Estado"
                  opciones={OPCIONES_ESTADO_NUEVO}
                  value={form.estado}
                  onChange={(valor) => actualizar('estado', valor || 'en_curso')}
                  error={errores.estado}
                />
              )}
            </div>
          </fieldset>
          <fieldset className={styles.grupo}>
            <legend className={styles.leyenda}>Horarios (hora de El Salvador)</legend>
            <div className={styles.fila}>
              <Field
                label="Salida"
                type="datetime-local"
                value={form.salida}
                onChange={(event) => actualizar('salida', event.target.value)}
                error={errores.salida_en}
              />
              {conLlegada && (
                <Field
                  label="Llegada"
                  type="datetime-local"
                  value={form.llegada}
                  min={form.salida}
                  onChange={(event) => actualizar('llegada', event.target.value)}
                  error={errores.llegada_en}
                />
              )}
            </div>
          </fieldset>
          <div className={styles.acciones}>
            <Button variant="secundario" to={editando ? `/viajes/${id}` : '/viajes'} disabled={enviando}>
              Cancelar
            </Button>
            <Button type="submit" disabled={enviando}>
              {enviando ? 'Guardando…' : editando ? 'Guardar cambios' : 'Registrar salida'}
            </Button>
          </div>
        </form>
      )}
    </>
  );
}
