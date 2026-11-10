export const ESTADOS_CHOFER = {
  activo: { tono: 'exito', texto: 'Activo' },
  inactivo: { tono: 'neutro', texto: 'Inactivo' },
  suspendido: { tono: 'aviso', texto: 'Suspendido' },
};

export const OPCIONES_ESTADO_CHOFER = Object.entries(ESTADOS_CHOFER).map(([valor, { texto }]) => ({ valor, etiqueta: texto }));

export const nombreCompleto = (chofer) => `${chofer.nombres} ${chofer.apellidos}`;
