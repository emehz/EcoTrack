import { useState } from 'react';
import { Dialog } from '@base-ui/react/dialog';
import { createFactor } from '../api/endpoints';
import { Selector } from './Selector';
import { Button, Field, Notice } from './ui';
import styles from './FactorDialog.module.css';

const FUENTES_EJEMPLO = 'Ej. EPA/GLEC referencia';
const FACTOR_MAXIMO = 10;
const LARGO_MAXIMO_FUENTE = 200;

export function FactorDialog({ abierto, tipo, combustible, combinacion, catalogos, onCreado, onCerrar }) {
  const elegible = !tipo || !combustible;
  const [tipoElegido, setTipoElegido] = useState('');
  const [combustibleElegido, setCombustibleElegido] = useState('');
  const [factor, setFactor] = useState('');
  const [fuente, setFuente] = useState('');
  const [errores, setErrores] = useState({});
  const [errorGeneral, setErrorGeneral] = useState('');
  const [guardando, setGuardando] = useState(false);

  const limpiar = () => {
    setTipoElegido('');
    setCombustibleElegido('');
    setFactor('');
    setFuente('');
    setErrores({});
    setErrorGeneral('');
  };

  const cerrar = () => {
    if (guardando) return;
    limpiar();
    onCerrar();
  };

  const enviar = async (event) => {
    event.preventDefault();
    const tipoFinal = elegible ? tipoElegido : tipo;
    const combustibleFinal = elegible ? combustibleElegido : combustible;
    const valor = Number(factor);
    const nuevos = {};
    if (!tipoFinal) nuevos.tipo_vehiculo = 'Elige el tipo de vehículo.';
    if (!combustibleFinal) nuevos.tipo_combustible = 'Elige el combustible.';
    if (!factor || !(valor > 0) || valor > FACTOR_MAXIMO) {
      nuevos.factor_co2_km = 'Escribe un factor mayor que 0 y de hasta 10 kg de CO₂ por km.';
    }
    if (!fuente.trim()) nuevos.fuente = 'Indica de dónde sale el factor.';
    else if (fuente.trim().length > LARGO_MAXIMO_FUENTE) nuevos.fuente = 'Usa como máximo 200 caracteres.';
    setErrores(nuevos);
    setErrorGeneral('');
    if (Object.keys(nuevos).length > 0) return;

    setGuardando(true);
    try {
      const { data } = await createFactor({
        tipo_vehiculo: tipoFinal,
        tipo_combustible: combustibleFinal,
        factor_co2_km: valor,
        fuente: fuente.trim(),
      });
      limpiar();
      onCreado(data);
    } catch (falla) {
      const delServidor = Object.fromEntries((falla.details ?? []).map(({ campo, mensaje }) => [campo, mensaje]));
      setErrores(delServidor);
      const porCampo = ['tipo_vehiculo', 'tipo_combustible', 'factor_co2_km', 'fuente'].some((campo) => delServidor[campo]);
      if (falla.status === 409) {
        setErrorGeneral('Esta combinación ya tiene un factor vigente. Para cambiarlo, usa Sustituir en la lista de factores.');
      } else if (!porCampo) {
        setErrorGeneral(falla.message);
      }
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
            {elegible
              ? 'Para una combinación que todavía no tiene factor. Rige desde hoy y los viajes nuevos lo usarán. Si ya tiene uno, usa Sustituir.'
              : `Para ${combinacion}. Rige desde hoy y los viajes nuevos lo usarán.`}
          </Dialog.Description>
          <form className={styles.form} onSubmit={enviar} noValidate>
            {errorGeneral && (
              <Notice tono="peligro" role="alert">
                {errorGeneral}
              </Notice>
            )}
            {elegible && (
              <>
                <Selector
                  label="Tipo de vehículo"
                  opciones={catalogos?.tipos_vehiculo ?? []}
                  value={tipoElegido}
                  onChange={setTipoElegido}
                  error={errores.tipo_vehiculo}
                />
                <Selector
                  label="Combustible"
                  opciones={catalogos?.tipos_combustible ?? []}
                  value={combustibleElegido}
                  onChange={setCombustibleElegido}
                  error={errores.tipo_combustible}
                />
              </>
            )}
            <Field
              label="Factor (kg de CO₂ por km)"
              inputMode="decimal"
              placeholder="0.850000"
              value={factor}
              onChange={(event) => setFactor(event.target.value)}
              error={errores.factor_co2_km}
              autoFocus={!elegible}
            />
            <Field
              label="Fuente"
              placeholder={FUENTES_EJEMPLO}
              maxLength={LARGO_MAXIMO_FUENTE}
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
