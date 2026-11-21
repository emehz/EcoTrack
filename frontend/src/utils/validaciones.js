const SOLO_LETRAS = /^\p{L}+(?:[ '’-]\p{L}+)*$/u;
const CORREO = /^[a-z0-9._%+-]+@[a-z0-9](?:[a-z0-9-]*[a-z0-9])?(?:\.[a-z0-9](?:[a-z0-9-]*[a-z0-9])?)*\.[a-z]{2,}$/;
const LUGAR = /^[\p{L}\p{N}][\p{L}\p{N} .,'’#()/-]*$/u;

const limpiar = (valor) => valor.trim().replace(/\s+/g, ' ');

export function validarNombre(valor, mensajeVacio) {
  const limpio = limpiar(valor);
  if (!limpio) return mensajeVacio;
  if (!SOLO_LETRAS.test(limpio)) return 'Usa solo letras; se permiten espacios, guiones y apóstrofos entre palabras.';
  if (limpio.length < 2) return 'Escribe al menos 2 letras.';
  if (limpio.length > 80) return 'Usa como máximo 80 caracteres.';
  return null;
}

export function validarLicencia(valor) {
  const limpio = valor.trim();
  if (!limpio) return 'Escribe el número de licencia.';
  const digitos = limpio.replace(/[\s-]/g, '');
  if (!/^\d+$/.test(digitos)) return 'Usa solo números y guiones, con el formato del DUI (00000000-0) o del NIT (0000-000000-000-0).';
  if (digitos.length !== 9 && digitos.length !== 14) {
    return 'La licencia lleva 9 dígitos (DUI, 00000000-0) o 14 dígitos (NIT, 0000-000000-000-0).';
  }
  return null;
}

export function validarCorreo(valor) {
  const limpio = valor.trim().toLowerCase();
  if (!limpio) return 'Escribe el correo.';
  if (limpio.length > 254 || !CORREO.test(limpio)) return 'Escribe un correo válido, por ejemplo nombre@empresa.com.';
  return null;
}

export function validarLugar(valor, mensajeVacio) {
  const limpio = limpiar(valor);
  if (!limpio) return mensajeVacio;
  if (limpio.length > 120) return 'Usa como máximo 120 caracteres.';
  const letras = [...limpio].filter((c) => /\p{L}/u.test(c)).length;
  if (!LUGAR.test(limpio) || letras < 2) return 'Escribe un lugar con letras; se permiten números, espacios y . , - # /';
  return null;
}
