export const VIGENCIAS = [
  { valor: 'vigentes', texto: 'Vigentes' },
  { valor: 'futuros', texto: 'Programados' },
  { valor: 'historicos', texto: 'Históricos' },
  { valor: 'todos', texto: 'Todos' },
];

export const hoyElSalvador = () =>
  new Intl.DateTimeFormat('en-CA', { timeZone: 'America/El_Salvador' }).format(new Date());

export function estadoFactor(factor, hoy = hoyElSalvador()) {
  if (factor.vigente) return { tono: 'exito', texto: 'Vigente' };
  if (factor.vigente_desde > hoy) return { tono: 'info', texto: 'Programado' };
  return { tono: 'neutro', texto: 'Histórico' };
}
