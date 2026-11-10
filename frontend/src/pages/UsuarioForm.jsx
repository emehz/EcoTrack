import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { toast } from "sonner";
import { createUsuario, getUsuario, updateUsuario } from "../api/endpoints";
import { Selector } from "../components/Selector";
import {
  Button,
  EmptyState,
  Field,
  Notice,
  PageHeader,
} from "../components/ui";
import { OPCIONES_ROL, erroresDe, validarContrasena } from "../utils/usuarios";
import styles from "../components/Formulario.module.css";

const VACIO = {
  correo: "",
  nombres: "",
  apellidos: "",
  rol: "",
  contrasena: "",
  confirmacion: "",
};

export default function UsuarioForm() {
  const { id } = useParams();
  const editando = Boolean(id);
  const navigate = useNavigate();
  const [form, setForm] = useState(VACIO);
  const [cargado, setCargado] = useState(!editando);
  const [errorCarga, setErrorCarga] = useState(null);
  const [errores, setErrores] = useState({});
  const [errorGeneral, setErrorGeneral] = useState("");
  const [enviando, setEnviando] = useState(false);

  useEffect(() => {
    if (!editando) return undefined;
    const controller = new AbortController();
    getUsuario(id, controller.signal)
      .then(({ data }) => {
        setForm({
          ...VACIO,
          correo: data.correo,
          nombres: data.nombres,
          apellidos: data.apellidos,
          rol: data.rol,
        });
        setCargado(true);
      })
      .catch((falla) => {
        if (falla.name !== "AbortError") setErrorCarga(falla);
      });
    return () => controller.abort();
  }, [editando, id]);

  const actualizar = (campo, valor) =>
    setForm((anterior) => ({ ...anterior, [campo]: valor }));

  const enviar = async (event) => {
    event.preventDefault();
    const nuevos = {};
    if (!editando && !form.correo.trim()) nuevos.correo = "Escribe el correo.";
    if (!form.nombres.trim()) nuevos.nombres = "Escribe los nombres.";
    if (!form.apellidos.trim()) nuevos.apellidos = "Escribe los apellidos.";
    if (!form.rol) nuevos.rol = "Elige el rol.";
    if (!editando) {
      const problema = validarContrasena(form.contrasena, form.confirmacion);
      if (problema) nuevos.contrasena = problema;
    }
    setErrores(nuevos);
    setErrorGeneral("");
    if (Object.keys(nuevos).length > 0) return;

    setEnviando(true);
    try {
      const cuerpo = {
        nombres: form.nombres,
        apellidos: form.apellidos,
        rol: form.rol,
      };
      if (editando) await updateUsuario(id, cuerpo);
      else
        await createUsuario({
          ...cuerpo,
          correo: form.correo,
          contrasena: form.contrasena,
        });
      toast.success(editando ? "Usuario actualizado" : "Usuario creado", {
        description: `${form.nombres} ${form.apellidos}`,
      });
      navigate("/usuarios");
    } catch (falla) {
      const porCampo = erroresDe(falla);
      setErrores(porCampo);
      if (Object.keys(porCampo).length === 0) setErrorGeneral(falla.message);
      setEnviando(false);
    }
  };

  if (errorCarga) {
    return (
      <EmptyState
        titulo={
          errorCarga.status === 404
            ? "Este usuario no existe"
            : "No pudimos cargar el usuario"
        }
        accion={
          <Button variant="secundario" to="/usuarios">
            Volver a usuarios
          </Button>
        }
      >
        {errorCarga.message}
      </EmptyState>
    );
  }

  const titulo = editando ? "Editar usuario" : "Nuevo usuario";

  return (
    <>
      <PageHeader
        titulo={titulo}
        migas={[{ texto: "Usuarios", to: "/usuarios" }, { texto: titulo }]}
      />
      {!cargado ? (
        <span
          className="skeleton"
          style={{ height: 260, maxWidth: 640 }}
          aria-busy="true"
        />
      ) : (
        <form className={styles.form} onSubmit={enviar} noValidate>
          {errorGeneral && (
            <Notice tono="peligro" role="alert">
              {errorGeneral}
            </Notice>
          )}
          <fieldset className={styles.grupo}>
            <legend className={styles.leyenda}>Cuenta</legend>
            <Field
              label="Correo"
              type="email"
              autoComplete="off"
              value={form.correo}
              onChange={(event) => actualizar("correo", event.target.value)}
              error={errores.correo}
              disabled={editando}
              help={
                editando
                  ? "El correo no se puede cambiar."
                  : "Con este correo iniciará sesión."
              }
            />
            <div className={styles.fila}>
              <Field
                label="Nombres"
                value={form.nombres}
                onChange={(event) => actualizar("nombres", event.target.value)}
                error={errores.nombres}
              />
              <Field
                label="Apellidos"
                value={form.apellidos}
                onChange={(event) =>
                  actualizar("apellidos", event.target.value)
                }
                error={errores.apellidos}
              />
            </div>
            <Selector
              label="Rol"
              opciones={OPCIONES_ROL}
              value={form.rol}
              onChange={(valor) => actualizar("rol", valor)}
              error={errores.rol}
            />
          </fieldset>

          {!editando && (
            <fieldset className={styles.grupo}>
              <legend className={styles.leyenda}>Contraseña inicial</legend>
              <div className={styles.fila}>
                <Field
                  label="Contraseña"
                  type="password"
                  autoComplete="new-password"
                  value={form.contrasena}
                  onChange={(event) =>
                    actualizar("contrasena", event.target.value)
                  }
                  error={errores.contrasena}
                  help="Al menos 8 caracteres, con letras y números."
                />
                <Field
                  label="Confirmar contraseña"
                  type="password"
                  autoComplete="new-password"
                  value={form.confirmacion}
                  onChange={(event) =>
                    actualizar("confirmacion", event.target.value)
                  }
                />
              </div>
              <p className={styles.ayuda}>
                La persona puede cambiarla después desde Mi perfil.
              </p>
            </fieldset>
          )}

          <div className={styles.acciones}>
            <Button variant="secundario" to="/usuarios" disabled={enviando}>
              Cancelar
            </Button>
            <Button type="submit" disabled={enviando}>
              {enviando
                ? "Guardando…"
                : editando
                  ? "Guardar cambios"
                  : "Crear usuario"}
            </Button>
          </div>
        </form>
      )}
    </>
  );
}
