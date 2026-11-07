import { AlertDialog } from '@base-ui/react/alert-dialog';
import { Button } from './ui';
import styles from './ConfirmDialog.module.css';

export function ConfirmDialog({ abierto, titulo, children, accion, ocupado, onConfirmar, onCerrar }) {
  return (
    <AlertDialog.Root open={abierto} onOpenChange={(open) => !open && !ocupado && onCerrar()}>
      <AlertDialog.Portal>
        <AlertDialog.Backdrop className={styles.backdrop} />
        <AlertDialog.Popup className={styles.popup}>
          <AlertDialog.Title className={styles.title}>{titulo}</AlertDialog.Title>
          <AlertDialog.Description className={styles.description} render={<div />}>
            {children}
          </AlertDialog.Description>
          <div className={styles.actions}>
            <AlertDialog.Close render={<Button variant="secundario" disabled={ocupado} />}>Cancelar</AlertDialog.Close>
            <Button variant="peligro" onClick={onConfirmar} disabled={ocupado}>
              {ocupado ? 'Guardando…' : accion}
            </Button>
          </div>
        </AlertDialog.Popup>
      </AlertDialog.Portal>
    </AlertDialog.Root>
  );
}
