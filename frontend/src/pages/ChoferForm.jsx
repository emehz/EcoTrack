import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { toast } from 'sonner';
import { createChofer, getChofer, updateChofer } from '../api/endpoints';
import { Button, EmptyState, Field, Notice, PageHeader } from '../components/ui';
import { nombreCompleto } from '../utils/choferes';
import styles from '../components/Formulario.module.css';

const VACIO = { nombres: '', apellidos: '', numero_licencia: '', telefono: '' };

export default function ChoferForm() {
  const { id } = useParams();
  const editando = Boolean(id);
  const navigate = useNavigate();
  const [form, setForm] = useState(VACIO);
  const [cargado, setCargado] = useState(!editando);
  const [errorCarga, setErrorCarga] = useState(null);
  const [errores, setErrores] = useState({});
  const [errorGeneral, setErrorGeneral] = useState('');
  const [enviando, setEnviando] = useState(false);

  useEffect(() => {
    if (!editando) return undefined;
    const controller = new AbortController();
    getChofer(id, controller.signal)
      .then(({ data }) => {
        const { nombres, apellidos, numero_licencia: licencia, telefono } = data.chofer;
        setForm({ nombres, apellidos, numero_licencia: licencia, telefono: telefono ?? '' });
        setCargado(true);
      })
      .catch((falla) => {
        if (falla.name !== 'AbortError') setErrorCarga(falla);
      });
    return () => controller.abort();
  }, [editando, id]);

  const actualizar = (campo, valor) => setForm((actual) => ({ ...actual, [campo]: valor }));

  const enviar = async (event) => {
    event.preventDefault();
    const nuevos = {};
    if (!form.nombres.trim()) nuevos.nombres = 'Escribe los nombres.';
    if (!form.apellidos.trim()) nuevos.apellidos = 'Escribe los apellidos.';
    if (!form.numero_licencia.trim()) nuevos.numero_licencia = 'Escribe el número de licencia.';
    setErrores(nuevos);
    setErrorGeneral('');
    if (Object.keys(nuevos).length > 0) return;

    setEnviando(true);
    const cuerpo = { ...form, telefono: form.telefono.trim() || null };
    try {
      const { data } = editando ? await updateChofer(id, cuerpo) : await createChofer(cuerpo);
      toast.success(editando ? 'Chofer actualizado' : 'Chofer registrado', { description: nombreCompleto(data) });
      navigate(`/choferes/${data.chofer_id}`);
    } catch (falla) {
      const delServidor = Object.fromEntries((falla.details ?? []).map(({ campo, mensaje }) => [campo, mensaje]));
      setErrores(delServidor);
      if (Object.keys(delServidor).length === 0) setErrorGeneral(falla.message);
      setEnviando(false);
    }
  };

  if (errorCarga) {
    return (
      <EmptyState
        titulo={errorCarga.status === 404 ? 'Este chofer no existe' : 'No pudimos cargar el chofer'}
        accion={<Button variant="secundario" to="/choferes">Volver a choferes</Button>}
      >
        {errorCarga.message}
      </EmptyState>
    );
  }

  const titulo = editando ? 'Editar chofer' : 'Nuevo chofer';

  return (
    <>
      <PageHeader titulo={titulo} migas={[{ texto: 'Choferes', to: '/choferes' }, { texto: titulo }]} />
      {!cargado ? (
        <span className="skeleton" style={{ height: 220, maxWidth: 640 }} aria-busy="true" />
      ) : (
        <form className={styles.form} onSubmit={enviar} noValidate>
          {errorGeneral && (
            <Notice tono="peligro" role="alert">
              {errorGeneral}
            </Notice>
          )}
          <fieldset className={styles.grupo}>
            <legend className={styles.leyenda}>Datos del chofer</legend>
            <div className={styles.fila}>
              <Field
                label="Nombres"
                value={form.nombres}
                onChange={(event) => actualizar('nombres', event.target.value)}
                error={errores.nombres}
                autoComplete="off"
              />
              <Field
                label="Apellidos"
                value={form.apellidos}
                onChange={(event) => actualizar('apellidos', event.target.value)}
                error={errores.apellidos}
                autoComplete="off"
              />
            </div>
            <div className={styles.fila}>
              <Field
                label="Número de licencia"
                value={form.numero_licencia}
                onChange={(event) => actualizar('numero_licencia', event.target.value.toUpperCase())}
                error={errores.numero_licencia}
                help="No puede repetirse entre choferes."
                autoComplete="off"
              />
              <Field
                label="Teléfono (opcional)"
                type="tel"
                inputMode="tel"
                placeholder="7777-1111"
                value={form.telefono}
                onChange={(event) => actualizar('telefono', event.target.value)}
                error={errores.telefono}
              />
            </div>
          </fieldset>
          <div className={styles.acciones}>
            <Button variant="secundario" to={editando ? `/choferes/${id}` : '/choferes'} disabled={enviando}>
              Cancelar
            </Button>
            <Button type="submit" disabled={enviando}>
              {enviando ? 'Guardando…' : editando ? 'Guardar cambios' : 'Registrar chofer'}
            </Button>
          </div>
        </form>
      )}
    </>
  );
}
