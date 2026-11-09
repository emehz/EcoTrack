export const SEVERIDADES = {
  critica: { tono: 'peligro', texto: 'Crítica' },
  alta: { tono: 'peligro', texto: 'Alta' },
  media: { tono: 'aviso', texto: 'Media' },
  baja: { tono: 'info', texto: 'Baja' },
  observacion: { tono: 'neutro', texto: 'Observación' },
};

export const ESTADOS_HALLAZGO = {
  abierto: { tono: 'aviso', texto: 'Abierto' },
  corregido: { tono: 'exito', texto: 'Corregido' },
  riesgo_aceptado: { tono: 'info', texto: 'Riesgo aceptado' },
  descartado: { tono: 'neutro', texto: 'Descartado' },
};

export const ESTADOS_AUDITORIA = {
  en_revision: { tono: 'aviso', texto: 'En revisión' },
  cerrada: { tono: 'neutro', texto: 'Cerrada' },
};

export const RESULTADOS = {
  aprobado: { tono: 'exito', texto: 'Aprobado' },
  aprobado_con_ajustes: { tono: 'exito', texto: 'Aprobado con ajustes' },
  rechazado: { tono: 'peligro', texto: 'Rechazado' },
};

export const CONTROLES = {
  INTEGRIDAD_HASH: 'Integridad del archivo',
  COMPLETITUD: 'Completitud',
  EXACTITUD_CO2: 'Exactitud del CO₂',
  VALIDEZ: 'Validez de los datos',
  DUPLICADO: 'Viaje duplicado',
  FACTOR_VIGENTE: 'Factor vigente',
  ATIPICO: 'Distancia atípica',
};

export const opcionesDe = (catalogo) => Object.entries(catalogo).map(([valor, { texto }]) => ({ valor, etiqueta: texto }));

export const esGrave = (hallazgo) => hallazgo.severidad === 'critica' || hallazgo.severidad === 'alta';
