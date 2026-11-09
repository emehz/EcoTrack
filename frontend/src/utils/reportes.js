export const ESTADOS_REPORTE = {
  generado: { tono: 'neutro', texto: 'Generado' },
  subido: { tono: 'info', texto: 'Calculando' },
  calculado: { tono: 'exito', texto: 'Calculado' },
  en_auditoria: { tono: 'aviso', texto: 'En auditoría' },
  auditado: { tono: 'exito', texto: 'Auditado' },
  fallido: { tono: 'peligro', texto: 'Fallido' },
};

export const ESTADOS_CALCULO = {
  pendiente: { tono: 'info', texto: 'Pendiente' },
  procesando: { tono: 'info', texto: 'Calculando' },
  completado: { tono: 'exito', texto: 'Completado' },
  fallido: { tono: 'peligro', texto: 'Fallido' },
};

export const puedeGenerarReportes = (rol) => rol === 'admin' || rol === 'operador';

export const calculoEnCurso = (estado) => estado === 'pendiente' || estado === 'procesando';

export const versionVigente = (versiones) =>
  versiones.find((version) => version.es_oficial) ?? versiones[versiones.length - 1];

export const nombreDescarga = (nombreArchivo, version) => `${nombreArchivo.replace(/\.csv$/i, '')}-v${version}.csv`;

const hoyElSalvador = () =>
  new Intl.DateTimeFormat('en-CA', { timeZone: 'America/El_Salvador' }).format(new Date());

export const periodoInicial = () => {
  const hoy = hoyElSalvador();
  return { desde: `${hoy.slice(0, 4)}-01-01`, hasta: hoy };
};
