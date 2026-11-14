export const ROLES = {
  admin: "Administrador",
  operador: "Operador",
  auditor: "Auditor",
};

export const OPCIONES_ROL = Object.entries(ROLES).map(([valor, etiqueta]) => ({
  valor,
  etiqueta,
}));

export const erroresDe = (falla) =>
  Object.fromEntries(
    (falla.details ?? []).map(({ campo, mensaje }) => [campo, mensaje]),
  );

export const validarContrasena = (contrasena, confirmacion) => {
  if (contrasena.length < 8) return "Usa al menos 8 caracteres.";
  if (!/\p{L}/u.test(contrasena) || !/\d/.test(contrasena))
    return "Combina letras y números.";
  if (confirmacion !== undefined && contrasena !== confirmacion)
    return "Las contraseñas no coinciden.";
  return null;
};
