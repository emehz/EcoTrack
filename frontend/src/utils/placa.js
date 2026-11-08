const CLASES = ['AB', 'MB', 'PR', 'MI', 'RE', 'CD', 'CC', 'CR', 'P', 'A', 'C', 'R', 'N', 'O', 'D', 'V', 'M'];
const MAX_CHARS = 6;

export function normalizarPlaca(valor) {
  const limpia = valor.replace(/[\s-]+/g, '').toUpperCase();

  for (const clase of CLASES) {
    if (!limpia.startsWith(clase)) continue;
    const numero = limpia.slice(clase.length);
    const patron = clase === 'P' ? /^[0-9A-F]+$/ : /^[0-9]+$/;
    if (numero.length > 0 && numero.length <= MAX_CHARS && patron.test(numero)) {
      return `${clase} ${numero}`;
    }
  }

  return null;
}
