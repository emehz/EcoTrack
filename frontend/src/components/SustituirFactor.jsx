import { useState } from 'react';
import { toast } from 'sonner';
import { sustituirFactor } from '../api/endpoints';
import { DialogoFormulario } from './DialogoFormulario';
import { Field } from './ui';
import { formatFactor, formatOnlyDate } from '../utils/format';
import { hoyElSalvador } from '../utils/factores';

export function SustituirFactor({ factor, combinacion, onSustituido, onCerrar }) {
  const [form, setForm] = useState(() => ({ factor: '', desde: hoyElSalvador(), fuente: '' }));
  const [errores, setErrores] = useState({});
  const [errorGeneral, setErrorGeneral] = useState('');
  const [ocupado, setOcupado] = useState(false);

  const actualizar = (campo, valor) => setForm((actual) => ({ ...actual, [campo]: valor }));

  const guardar = async () => {
    const valor = Number(form.factor.replace(',', '.'));
    const nuevos = {};
    if (!form.factor || !(valor > 0)) nuevos.factor_co2_km = 'Escribe un factor mayor que cero.';
    if (!form.desde) nuevos.vigente_desde = 'Indica desde qué fecha rige.';
    if (!form.fuente.trim()) nuevos.fuente = 'Indica de dónde sale el nuevo factor.';
    setErrores(nuevos);
    setErrorGeneral('');
    if (Object.keys(nuevos).length > 0) return;

    setOcupado(true);
    try {
      const { data } = await sustituirFactor(factor.factor_emision_id, {
        factor_co2_km: valor,
        vigente_desde: form.desde,
        fuente: form.fuente.trim(),
      });
      toast.success('Factor sustituido', {
        description: `${combinacion}: ${formatFactor(data.nuevo.factor_co2_km)} kg/km desde el ${formatOnlyDate(data.nuevo.vigente_desde)}.`,
      });
      onSustituido(data);
    } catch (falla) {
      const porCampo = Object.fromEntries((falla.details ?? []).map(({ campo, mensaje }) => [campo, mensaje]));
      setErrores(porCampo);
      if (Object.keys(porCampo).length === 0 || falla.status === 409) setErrorGeneral(falla.message);
    } finally {
      setOcupado(false);
    }
  };

  return (
    <DialogoFormulario
      abierto={Boolean(factor)}
      titulo="Sustituir factor"
      descripcion={`${combinacion} · Actual: ${formatFactor(factor.factor_co2_km)} kg de CO₂ por km desde el ${formatOnlyDate(factor.vigente_desde)}.`}
      accion="Sustituir"
      ocupado={ocupado}
      error={errorGeneral}
      onEnviar={guardar}
      onCerrar={onCerrar}
    >
      <Field
        label="Nuevo factor (kg de CO₂ por km)"
        inputMode="decimal"
        placeholder="0.850000"
        value={form.factor}
        onChange={(event) => actualizar('factor', event.target.value)}
        error={errores.factor_co2_km}
        autoFocus
      />
      <Field
        label="Rige desde"
        type="date"
        value={form.desde}
        onChange={(event) => actualizar('desde', event.target.value)}
        error={errores.vigente_desde}
        help="El factor actual se cierra ese día. Los viajes anteriores conservan el factor con el que se calcularon."
      />
      <Field
        label="Fuente"
        placeholder="Ej. EPA/GLEC 2026"
        value={form.fuente}
        onChange={(event) => actualizar('fuente', event.target.value)}
        error={errores.fuente}
      />
    </DialogoFormulario>
  );
}
