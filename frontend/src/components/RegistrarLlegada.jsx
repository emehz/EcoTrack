import { useState } from 'react';
import { toast } from 'sonner';
import { cambiarEstadoViaje } from '../api/endpoints';
import { DialogoFormulario } from './DialogoFormulario';
import { Field } from './ui';
import { formatNumber } from '../utils/format';
import { aEntradaLocal, deEntradaLocal } from '../utils/viajes';

export function RegistrarLlegada({ viaje, onRegistrada, onCerrar }) {
  const salida = aEntradaLocal(viaje.salida_en);
  const ahora = aEntradaLocal();
  const [llegada, setLlegada] = useState(ahora < salida ? salida : ahora);
  const [error, setError] = useState('');
  const [errorGeneral, setErrorGeneral] = useState('');
  const [ocupado, setOcupado] = useState(false);

  const guardar = async () => {
    if (!llegada) return setError('Indica la fecha y hora de llegada.');
    if (llegada < salida) return setError('La llegada no puede ser anterior a la salida.');
    setError('');
    setErrorGeneral('');
    setOcupado(true);
    try {
      const { data } = await cambiarEstadoViaje(viaje.viaje_id, { estado: 'completado', llegada_en: deEntradaLocal(llegada) });
      toast.success('Llegada registrada', {
        description: `${data.viaje.destino} · ${formatNumber(data.viaje.co2_kg)} kg de CO₂`,
      });
      onRegistrada(data);
    } catch (falla) {
      const campo = (falla.details ?? []).find((detalle) => detalle.campo === 'llegada_en');
      if (campo) setError(campo.mensaje);
      else setErrorGeneral(falla.message);
    } finally {
      setOcupado(false);
    }
    return undefined;
  };

  return (
    <DialogoFormulario
      abierto
      titulo="Registrar llegada"
      descripcion={`${viaje.origen} › ${viaje.destino}. El viaje quedará completado.`}
      accion="Registrar llegada"
      ocupado={ocupado}
      error={errorGeneral}
      onEnviar={guardar}
      onCerrar={onCerrar}
    >
      <Field
        label="Llegada (hora de El Salvador)"
        type="datetime-local"
        value={llegada}
        min={salida}
        onChange={(event) => setLlegada(event.target.value)}
        error={error}
        autoFocus
      />
    </DialogoFormulario>
  );
}
