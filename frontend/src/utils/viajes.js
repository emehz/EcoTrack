export const ESTADOS_VIAJE = {
  planificado: { tono: 'info', texto: 'Planificado' },
  en_curso: { tono: 'aviso', texto: 'En curso' },
  completado: { tono: 'exito', texto: 'Completado' },
  cancelado: { tono: 'neutro', texto: 'Cancelado' },
};

export const PESTANAS_VIAJE = [
  { valor: 'todos', texto: 'Todos' },
  { valor: 'en_curso', texto: 'En curso' },
  { valor: 'planificado', texto: 'Planificados' },
  { valor: 'completado', texto: 'Completados' },
  { valor: 'cancelado', texto: 'Cancelados' },
];

export const OPCIONES_ESTADO_NUEVO = [
  { valor: 'en_curso', etiqueta: 'En curso (ya salió)' },
  { valor: 'planificado', etiqueta: 'Planificado' },
  { valor: 'completado', etiqueta: 'Completado (ya llegó)' },
];

export const puedeRegistrarViajes = (rol) => rol === 'admin' || rol === 'operador';

const ZONA = 'America/El_Salvador';
const DESFASE = '-06:00';

const partes = new Intl.DateTimeFormat('en-CA', {
  timeZone: ZONA,
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
});

export function aEntradaLocal(valor = new Date()) {
  const p = Object.fromEntries(partes.formatToParts(new Date(valor)).map(({ type, value }) => [type, value]));
  return `${p.year}-${p.month}-${p.day}T${p.hour}:${p.minute}`;
}

export const deEntradaLocal = (entrada) => (entrada ? `${entrada}:00${DESFASE}` : null);

export const leerDistancia = (texto) => {
  const valor = Number(String(texto).replace(',', '.'));
  return texto === '' || Number.isNaN(valor) ? null : valor;
};
