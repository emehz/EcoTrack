import { useEffect, useState } from "react";
import { Dialog } from "@base-ui/react/dialog";
import { clsx } from "clsx";
import { getRegistroBitacora } from "../api/endpoints";
import { Button, Notice } from "./ui";
import { formatDateTime } from "../utils/format";
import { ACCIONES, ENTIDADES, valorLegible } from "../utils/bitacora";
import base from "./DialogoFormulario.module.css";
import styles from "./DetalleBitacora.module.css";

export function DetalleBitacora({ registro, onCerrar }) {
  const [respuesta, setRespuesta] = useState({ id: null });

  useEffect(() => {
    const controller = new AbortController();
    getRegistroBitacora(registro.auditoria_id, controller.signal)
      .then(({ data }) => setRespuesta({ id: registro.auditoria_id, data }))
      .catch((falla) => {
        if (falla.name !== "AbortError")
          setRespuesta({ id: registro.auditoria_id, error: falla });
      });
    return () => controller.abort();
  }, [registro.auditoria_id]);

  const cambios = respuesta.data?.cambios ?? [];
  const accion = ACCIONES[registro.accion];

  return (
    <Dialog.Root open onOpenChange={(open) => !open && onCerrar()}>
      <Dialog.Portal>
        <Dialog.Backdrop className={base.backdrop} />
        <Dialog.Popup className={clsx(base.popup, styles.ancho)}>
          <Dialog.Title className={base.title}>
            {accion.texto} de{" "}
            {ENTIDADES[registro.entidad]?.toLowerCase() ?? registro.entidad}
          </Dialog.Title>
          <Dialog.Description className={base.description}>
            {formatDateTime(registro.creado_en)} ·{" "}
            {registro.usuario ?? "Sistema"}
          </Dialog.Description>
          {respuesta.error ? (
            <Notice tono="peligro" role="alert">
              {respuesta.error.message}
            </Notice>
          ) : !respuesta.data ? (
            <span
              className="skeleton"
              style={{ height: 160 }}
              aria-busy="true"
            />
          ) : cambios.length === 0 ? (
            <p>No hubo cambios en los datos guardados.</p>
          ) : (
            <table className={styles.cambios}>
              <thead>
                <tr>
                  <th scope="col">Campo</th>
                  {registro.accion !== "crear" && <th scope="col">Antes</th>}
                  {registro.accion !== "eliminar" && (
                    <th scope="col">Después</th>
                  )}
                </tr>
              </thead>
              <tbody>
                {cambios.map((cambio) => (
                  <tr key={cambio.campo}>
                    <th scope="row">{cambio.campo}</th>
                    {registro.accion !== "crear" && (
                      <td
                        className={clsx(
                          styles.valor,
                          registro.accion === "editar" && styles.antes,
                        )}
                      >
                        {valorLegible(cambio.antes)}
                      </td>
                    )}
                    {registro.accion !== "eliminar" && (
                      <td className={styles.valor}>
                        {valorLegible(cambio.despues)}
                      </td>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
          )}
          <div className={base.actions}>
            <Dialog.Close render={<Button variant="secundario" />}>
              Cerrar
            </Dialog.Close>
          </div>
        </Dialog.Popup>
      </Dialog.Portal>
    </Dialog.Root>
  );
}
