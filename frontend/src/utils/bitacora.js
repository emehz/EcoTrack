export const ENTIDADES = {
  vehiculo: "Vehículo",
  viaje: "Viaje",
  chofer: "Chofer",
  carga_combustible: "Carga de combustible",
  factor_emision: "Factor de emisión",
  usuario: "Usuario",
  reporte_viaje: "Reporte",
  auditoria_reporte: "Auditoría de reporte",
  hallazgo_auditoria: "Hallazgo",
};

export const OPCIONES_ENTIDAD = Object.entries(ENTIDADES).map(
  ([valor, etiqueta]) => ({ valor, etiqueta }),
);

export const ACCIONES = {
  crear: { tono: "exito", texto: "Creación" },
  editar: { tono: "info", texto: "Edición" },
  eliminar: { tono: "peligro", texto: "Eliminación" },
};

export const OPCIONES_ACCION = Object.entries(ACCIONES).map(
  ([valor, { texto }]) => ({ valor, etiqueta: texto }),
);

const RUTAS = {
  vehiculo: (id) => `/vehiculos/${id}`,
  viaje: (id) => `/viajes/${id}`,
  chofer: (id) => `/choferes/${id}`,
  reporte_viaje: (id) => `/reportes/${id}`,
  auditoria_reporte: (id) => `/auditorias/${id}`,
};

export const enlaceDe = (entidad, id) =>
  id && RUTAS[entidad] ? RUTAS[entidad](id) : null;

export const idCorto = (id) => (id ? id.slice(0, 8) : "—");

export function valorLegible(valor) {
  if (valor === null || valor === undefined) return "—";
  if (typeof valor === "boolean") return valor ? "Sí" : "No";
  if (typeof valor === "object") return JSON.stringify(valor);
  return String(valor);
}
