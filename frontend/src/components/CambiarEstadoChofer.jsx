import { useState } from 'react';
import { toast } from 'sonner';
import { cambiarEstadoChofer } from '../api/endpoints';
import { DialogoFormulario } from './DialogoFormulario';
import { Selector } from './Selector';
import { OPCIONES_ESTADO_CHOFER, nombreCompleto } from '../utils/choferes';

export function CambiarEstadoChofer({ chofer, onCambiado, onCerrar }) {
  const [estado, setEstado] = useState(chofer?.estado ?? '');
  const [ocupado, setOcupado] = useState(false);
  const [error, setError] = useState('');

  const guardar = async () => {
    setOcupado(true);
    setError('');
    try {
      const { data } = await cambiarEstadoChofer(chofer.chofer_id, estado);
      toast.success('Estado actualizado', { description: `${nombreCompleto(data)} quedó ${data.estado}.` });
      onCambiado(data);
    } catch (falla) {
      setError(falla.message);
    } finally {
      setOcupado(false);
    }
  };

  return (
    <DialogoFormulario
      abierto={Boolean(chofer)}
      titulo="Cambiar estado del chofer"
      descripcion={chofer ? `${nombreCompleto(chofer)} · Licencia ${chofer.numero_licencia}` : undefined}
      accion="Guardar"
      ocupado={ocupado}
      error={error}
      onEnviar={guardar}
      onCerrar={onCerrar}
    >
      <Selector
        label="Estado"
        opciones={OPCIONES_ESTADO_CHOFER}
        value={estado}
        onChange={setEstado}
        help="Un chofer inactivo o suspendido no se puede asignar a viajes nuevos. Su historial se conserva."
      />
    </DialogoFormulario>
  );
}
