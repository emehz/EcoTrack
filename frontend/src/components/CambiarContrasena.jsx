import { useState } from "react";
import { toast } from "sonner";
import { cambiarMiContrasena } from "../api/endpoints";
import { DialogoFormulario } from "./DialogoFormulario";
import { Field } from "./ui";
import { erroresDe, validarContrasena } from "../utils/usuarios";

const VACIO = { actual: "", nueva: "", confirmacion: "" };

export function CambiarContrasena({ abierto, onCerrar }) {
  const [form, setForm] = useState(VACIO);
  const [errores, setErrores] = useState({});
  const [errorGeneral, setErrorGeneral] = useState("");
  const [ocupado, setOcupado] = useState(false);

  const actualizar = (campo, valor) =>
    setForm((anterior) => ({ ...anterior, [campo]: valor }));

  const cerrar = () => {
    setForm(VACIO);
    setErrores({});
    setErrorGeneral("");
    onCerrar();
  };

  const guardar = async () => {
    const nuevos = {};
    if (!form.actual)
      nuevos.contrasena_actual = "Escribe tu contraseña actual.";
    const problema = validarContrasena(form.nueva, form.confirmacion);
    if (problema) nuevos.contrasena_nueva = problema;
    setErrores(nuevos);
    setErrorGeneral("");
    if (Object.keys(nuevos).length > 0) return;

    setOcupado(true);
    try {
      await cambiarMiContrasena(form.actual, form.nueva);
      toast.success("Contraseña actualizada", {
        description: "Úsala la próxima vez que inicies sesión.",
      });
      cerrar();
    } catch (falla) {
      const porCampo = erroresDe(falla);
      setErrores(porCampo);
      if (Object.keys(porCampo).length === 0) setErrorGeneral(falla.message);
    } finally {
      setOcupado(false);
    }
  };

  return (
    <DialogoFormulario
      abierto={abierto}
      titulo="Cambiar contraseña"
      accion="Cambiar contraseña"
      ocupado={ocupado}
      error={errorGeneral}
      onEnviar={guardar}
      onCerrar={cerrar}
    >
      <Field
        label="Contraseña actual"
        type="password"
        autoComplete="current-password"
        value={form.actual}
        onChange={(event) => actualizar("actual", event.target.value)}
        error={errores.contrasena_actual}
      />
      <Field
        label="Nueva contraseña"
        type="password"
        autoComplete="new-password"
        value={form.nueva}
        onChange={(event) => actualizar("nueva", event.target.value)}
        error={errores.contrasena_nueva}
        help="Al menos 8 caracteres, con letras y números."
      />
      <Field
        label="Confirmar nueva contraseña"
        type="password"
        autoComplete="new-password"
        value={form.confirmacion}
        onChange={(event) => actualizar("confirmacion", event.target.value)}
      />
    </DialogoFormulario>
  );
}
