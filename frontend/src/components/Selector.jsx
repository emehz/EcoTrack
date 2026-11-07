import { useId } from 'react';
import { Select } from '@base-ui/react/select';
import { Check, ChevronDown } from 'lucide-react';
import { clsx } from 'clsx';
import uiStyles from './ui.module.css';
import styles from './Selector.module.css';

export function Selector({ label, opciones = [], value, onChange, placeholder = 'Selecciona', opcionVacia, error, help, compacto }) {
  const messageId = `${useId()}-mensaje`;
  const items = [
    ...(opcionVacia ? [{ value: null, label: opcionVacia }] : []),
    ...opciones.map(({ valor, etiqueta }) => ({ value: valor, label: etiqueta })),
  ];

  return (
    <Select.Root items={items} value={value || null} onValueChange={(nuevo) => onChange(nuevo ?? '')} modal={false}>
      <div className={compacto ? undefined : uiStyles.field}>
        <Select.Label className={compacto ? 'visually-hidden' : uiStyles.label}>{label}</Select.Label>
        <Select.Trigger
          className={clsx(uiStyles.control, styles.trigger, compacto && styles.compacto, error && uiStyles.controlError)}
          aria-invalid={error ? true : undefined}
          aria-describedby={error || help ? messageId : undefined}
        >
          <Select.Value placeholder={placeholder} />
          <Select.Icon className={styles.icono}>
            <ChevronDown size={16} strokeWidth={1.75} aria-hidden="true" />
          </Select.Icon>
        </Select.Trigger>
        {(error || help) && (
          <p id={messageId} className={error ? uiStyles.errorText : uiStyles.helpText}>
            {error || help}
          </p>
        )}
      </div>
      <Select.Portal>
        <Select.Positioner className={styles.positioner} alignItemWithTrigger={false} sideOffset={4}>
          <Select.Popup className={styles.popup}>
            <Select.List>
              {items.map((item) => (
                <Select.Item key={item.label} value={item.value} className={styles.item}>
                  <Select.ItemText>{item.label}</Select.ItemText>
                  <Select.ItemIndicator className={styles.indicador}>
                    <Check size={15} strokeWidth={2} aria-hidden="true" />
                  </Select.ItemIndicator>
                </Select.Item>
              ))}
            </Select.List>
          </Select.Popup>
        </Select.Positioner>
      </Select.Portal>
    </Select.Root>
  );
}
