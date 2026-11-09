import { useId } from 'react';
import { Dialog } from '@base-ui/react/dialog';
import { clsx } from 'clsx';
import { Button, Notice } from './ui';
import uiStyles from './ui.module.css';
import styles from './DialogoFormulario.module.css';

export function DialogoFormulario({ abierto, titulo, descripcion, accion, ocupado, error, onEnviar, onCerrar, children }) {
  const enviar = (event) => {
    event.preventDefault();
    onEnviar();
  };

  return (
    <Dialog.Root open={abierto} onOpenChange={(open) => !open && !ocupado && onCerrar()}>
      <Dialog.Portal>
        <Dialog.Backdrop className={styles.backdrop} />
        <Dialog.Popup className={styles.popup}>
          <Dialog.Title className={styles.title}>{titulo}</Dialog.Title>
          {descripcion && <Dialog.Description className={styles.description}>{descripcion}</Dialog.Description>}
          <form className={styles.form} onSubmit={enviar} noValidate>
            {error && (
              <Notice tono="peligro" role="alert">
                {error}
              </Notice>
            )}
            {children}
            <div className={styles.actions}>
              <Button variant="secundario" onClick={onCerrar} disabled={ocupado}>
                Cancelar
              </Button>
              <Button type="submit" disabled={ocupado}>
                {ocupado ? 'Guardando…' : accion}
              </Button>
            </div>
          </form>
        </Dialog.Popup>
      </Dialog.Portal>
    </Dialog.Root>
  );
}

export function AreaTexto({ label, error, help, ...props }) {
  const id = useId();
  const messageId = `${id}-mensaje`;
  return (
    <div className={uiStyles.field}>
      <label htmlFor={id} className={uiStyles.label}>
        {label}
      </label>
      <textarea
        id={id}
        rows={3}
        aria-invalid={error ? true : undefined}
        aria-describedby={error || help ? messageId : undefined}
        className={clsx(uiStyles.control, styles.area, error && uiStyles.controlError)}
        {...props}
      />
      {(error || help) && (
        <p id={messageId} className={error ? uiStyles.errorText : uiStyles.helpText}>
          {error || help}
        </p>
      )}
    </div>
  );
}
