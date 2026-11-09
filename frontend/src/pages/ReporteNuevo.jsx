import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { toast } from 'sonner';
import { createReporte } from '../api/endpoints';
import { Selector } from '../components/Selector';
import { Button, Field, Notice, PageHeader } from '../components/ui';
import { useCatalogos } from '../hooks/useCatalogos';
import { periodoInicial } from '../utils/reportes';
import styles from './ReporteNuevo.module.css';

export default function ReporteNuevo() {
  const navigate = useNavigate();
  const { catalogos } = useCatalogos();
  const [form, setForm] = useState(() => ({
    ...periodoInicial(),
    tipo_vehiculo: '',
    tipo_combustible: '',
    solo_completados: true,
  }));
  const [errores, setErrores] = useState({});
  const [errorGeneral, setErrorGeneral] = useState('');
  const [enviando, setEnviando] = useState(false);

  const actualizar = (campo, valor) => setForm((actual) => ({ ...actual, [campo]: valor }));

  const enviar = async (event) => {
    event.preventDefault();
    const nuevos = {};
    if (!form.desde) nuevos.desde = 'Indica la fecha inicial.';
    if (!form.hasta) nuevos.hasta = 'Indica la fecha final.';
    if (form.desde && form.hasta && form.hasta < form.desde) nuevos.hasta = 'La fecha final no puede ser anterior a la inicial.';
    setErrores(nuevos);
    setErrorGeneral('');
    if (Object.keys(nuevos).length > 0) return;

    setEnviando(true);
    try {
      const { data } = await createReporte({
        desde: form.desde,
        hasta: form.hasta,
        tipo_vehiculo: form.tipo_vehiculo || null,
        tipo_combustible: form.tipo_combustible || null,
        solo_completados: form.solo_completados,
      });
      toast.success('Reporte generado', { description: `${data.total_filas} viajes. Calculando la huella de carbono…` });
      navigate(`/reportes/${data.reporte_viaje_id}`);
    } catch (falla) {
      const delServidor = Object.fromEntries((falla.details ?? []).map(({ campo, mensaje }) => [campo, mensaje]));
      setErrores(delServidor);
      if (!delServidor.desde && !delServidor.hasta) setErrorGeneral(falla.message);
      setEnviando(false);
    }
  };

  return (
    <>
      <PageHeader titulo="Generar reporte" migas={[{ texto: 'Reportes', to: '/reportes' }, { texto: 'Generar' }]} />

      <form className={styles.form} onSubmit={enviar} noValidate>
        {errorGeneral && (
          <Notice tono="peligro" role="alert">
            {errorGeneral}
          </Notice>
        )}

        <fieldset className={styles.grupo}>
          <legend className={styles.leyenda}>Periodo</legend>
          <div className={styles.fila}>
            <Field
              label="Desde"
              type="date"
              value={form.desde}
              onChange={(event) => actualizar('desde', event.target.value)}
              error={errores.desde}
            />
            <Field
              label="Hasta"
              type="date"
              value={form.hasta}
              onChange={(event) => actualizar('hasta', event.target.value)}
              error={errores.hasta}
            />
          </div>
          <p className={styles.ayuda}>Se usa la fecha de salida de cada viaje, en hora de El Salvador.</p>
        </fieldset>

        <fieldset className={styles.grupo}>
          <legend className={styles.leyenda}>Filtros opcionales</legend>
          <div className={styles.fila}>
            <Selector
              label="Tipo de vehículo"
              opcionVacia="Todos los tipos"
              opciones={catalogos?.tipos_vehiculo}
              value={form.tipo_vehiculo}
              onChange={(valor) => actualizar('tipo_vehiculo', valor)}
              error={errores.tipo_vehiculo}
            />
            <Selector
              label="Combustible"
              opcionVacia="Todos los combustibles"
              opciones={catalogos?.tipos_combustible}
              value={form.tipo_combustible}
              onChange={(valor) => actualizar('tipo_combustible', valor)}
              error={errores.tipo_combustible}
            />
          </div>
          <label className={styles.casilla}>
            <input
              type="checkbox"
              checked={!form.solo_completados}
              onChange={(event) => actualizar('solo_completados', !event.target.checked)}
            />
            Incluir viajes planificados y en curso
          </label>
          <p className={styles.ayuda}>Por defecto solo entran los viajes completados. Los cancelados nunca se incluyen.</p>
        </fieldset>

        <div className={styles.acciones}>
          <Button variant="secundario" to="/reportes" disabled={enviando}>
            Cancelar
          </Button>
          <Button type="submit" disabled={enviando}>
            {enviando ? 'Generando…' : 'Generar reporte'}
          </Button>
        </div>
      </form>
    </>
  );
}
