import { useState } from 'react';
import { Dialog } from '@base-ui/react/dialog';
import { createFactor } from '../api/endpoints';
import { Button, Field, Notice } from './ui';
import styles from './FactorDialog.module.css';

const FUENTES_EJEMPLO = 'Ej. EPA/GLEC referencia';

export function FactorDialog({ abierto, tipo, combustible, combinacion, onCreado, onCerrar }) {
  const [factor, setFactor] = useState('');
  const [fuente, setFuente] = useState('');
  const [errores, setErrores] = useState({});
  const [errorGeneral, setErrorGeneral] = useState('');
  const [guardando, setGuardando] = useState(false);

  const cerrar = () => {
    if (guardando) return;
    setFactor('');
    setFuente('');
    setErrores({});
    setErrorGeneral('');
    onCerrar();
  };

  const enviar = async (event) => {
    event.preventDefault();
    const valor = Number(factor);
    const nuevos = {};
    if (!factor || !(valor > 0)) nuevos.factor_co2_km = 'Escribe un factor mayor que cero.';
    if (!fuente.trim()) nuevos.fuente = 'Indica de dónde sale el factor.';
    setErrores(nuevos);
    setErrorGeneral('');
    if (Object.keys(nuevos).length > 0) return;

    setGuardando(true);
    try {
      const { data } = await createFactor({
        tipo_vehiculo: tipo,
        tipo_combustible: combustible,
        factor_co2_km: valor,
        fuente: fuente.trim(),
      });
      setFactor('');
      setFuente('');
      onCreado(data);
    } catch (falla) {
      const delServidor = Object.fromEntries((falla.details ?? []).map(({ campo, mensaje }) => [campo, mensaje]));
      setErrores(delServidor);
      if (!delServidor.factor_co2_km && !delServidor.fuente) setErrorGeneral(falla.message);
    } finally {
      setGuardando(false);
    }
  };

  return (
    <Dialog.Root open={abierto} onOpenChange={(open) => !open && cerrar()}>
      <Dialog.Portal>
        <Dialog.Backdrop className={styles.backdrop} />
        <Dialog.Popup className={styles.popup}>
          <Dialog.Title className={styles.title}>Crear factor de CO₂</Dialog.Title>
          <Dialog.Description className={styles.description}>
            Para {combinacion}. Rige desde hoy y los viajes nuevos lo usarán.
          </Dialog.Description>
          <form className={styles.form} onSubmit={enviar} noValidate>
            {errorGeneral && (
              <Notice tono="peligro" role="alert">
                {errorGeneral}
              </Notice>
            )}
            <Field
              label="Factor (kg de CO₂ por km)"
              inputMode="decimal"
              placeholder="0.850000"
              value={factor}
              onChange={(event) => setFactor(event.target.value)}
              error={errores.factor_co2_km}
              autoFocus
            />
            <Field
              label="Fuente"
              placeholder={FUENTES_EJEMPLO}
              value={fuente}
              onChange={(event) => setFuente(event.target.value)}
              error={errores.fuente}
            />
            <div className={styles.actions}>
              <Button variant="secundario" onClick={cerrar} disabled={guardando}>
                Cancelar
              </Button>
              <Button type="submit" disabled={guardando}>
                {guardando ? 'Guardando…' : 'Crear factor'}
              </Button>
            </div>
          </form>
        </Dialog.Popup>
      </Dialog.Portal>
    </Dialog.Root>
  );
}
