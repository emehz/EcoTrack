import { download, request } from "./client";

export const login = (correo, contrasena) =>
  request("/auth/login", { method: "POST", body: { correo, contrasena } });

export const getPerfil = (signal) => request("/auth/perfil", { signal });

export const getCatalogos = (signal) => request("/catalogos", { signal });

export const getFactorVigente = (tipoVehiculo, tipoCombustible, signal) =>
  request("/factores/vigente", {
    signal,
    params: { tipo_vehiculo: tipoVehiculo, tipo_combustible: tipoCombustible },
  });

export const createFactor = (body) =>
  request("/factores", { method: "POST", body });

export const getFactores = (params, signal) =>
  request("/factores", { params, signal });

export const sustituirFactor = (id, body) =>
  request(`/factores/${id}/sustituir`, { method: "POST", body });

export const getVehiculos = (params, signal) =>
  request("/vehiculos", { params, signal });

export const getVehiculo = (id, signal) =>
  request(`/vehiculos/${id}`, { signal });

export const createVehiculo = (body) =>
  request("/vehiculos", { method: "POST", body });

export const updateVehiculo = (id, body) =>
  request(`/vehiculos/${id}`, { method: "PATCH", body });

export const darDeBajaVehiculo = (id) =>
  request(`/vehiculos/${id}/estado`, {
    method: "PATCH",
    body: { estado: "inactivo" },
  });

export const getReportes = (params, signal) =>
  request("/reportes", { params, signal });

export const getReporte = (id, signal) =>
  request(`/reportes/${id}`, { signal });

export const createReporte = (body) =>
  request("/reportes", { method: "POST", body });

export const getFilasReporte = (id, version, params, signal) =>
  request(`/reportes/${id}/versiones/${version}/filas`, { params, signal });

export const reprocesarVersion = (id, version) =>
  request(`/reportes/${id}/versiones/${version}/reprocesar`, {
    method: "POST",
  });

export const descargarCsvReporte = (id, version, nombre) =>
  download(`/reportes/${id}/versiones/${version}/csv`, nombre);

export const getAuditorias = (params, signal) =>
  request("/auditorias", { params, signal });

export const getAuditoria = (id, signal) =>
  request(`/auditorias/${id}`, { signal });

export const iniciarAuditoria = (reporteId, body) =>
  request(`/reportes/${reporteId}/auditorias`, { method: "POST", body });

export const registrarHallazgo = (id, body) =>
  request(`/auditorias/${id}/hallazgos`, { method: "POST", body });

export const cambiarEstadoHallazgo = (id, hallazgoId, body) =>
  request(`/auditorias/${id}/hallazgos/${hallazgoId}`, {
    method: "PATCH",
    body,
  });

export const cerrarAuditoria = (id, body) =>
  request(`/auditorias/${id}/cerrar`, { method: "POST", body });

export const getChoferes = (params, signal) =>
  request("/choferes", { params, signal });

export const getChofer = (id, signal) => request(`/choferes/${id}`, { signal });

export const createChofer = (body) =>
  request("/choferes", { method: "POST", body });

export const updateChofer = (id, body) =>
  request(`/choferes/${id}`, { method: "PATCH", body });

export const cambiarEstadoChofer = (id, estado) =>
  request(`/choferes/${id}/estado`, { method: "PATCH", body: { estado } });

export const getViajes = (params, signal) =>
  request("/viajes", { params, signal });

export const getViaje = (id, signal) => request(`/viajes/${id}`, { signal });

export const createViaje = (body) =>
  request("/viajes", { method: "POST", body });

export const updateViaje = (id, body) =>
  request(`/viajes/${id}`, { method: "PATCH", body });

export const cambiarEstadoViaje = (id, body) =>
  request(`/viajes/${id}/estado`, { method: "PATCH", body });

export const deleteViaje = (id) =>
  request(`/viajes/${id}`, { method: "DELETE", params: { confirmar: true } });

export const exportarViajes = (filtros, nombre) => {
  const params = new URLSearchParams(
    Object.entries(filtros).filter(([, valor]) => valor),
  );
  return download(`/viajes/exportar?${params}`, nombre);
};

export const getCargas = (params, signal) =>
  request("/combustible", { params, signal });

export const getConsumo = (params, signal) =>
  request("/combustible/consumo", { params, signal });

export const createCarga = (body) =>
  request("/combustible", { method: "POST", body });

export const updateCarga = (id, body) =>
  request(`/combustible/${id}`, { method: "PATCH", body });

export const deleteCarga = (id) =>
  request(`/combustible/${id}`, { method: "DELETE", params: { confirmar: true } });

export const getUsuarios = (params, signal) =>
  request("/usuarios", { params, signal });

export const getUsuario = (id, signal) =>
  request(`/usuarios/${id}`, { signal });

export const createUsuario = (body) =>
  request("/usuarios", { method: "POST", body });

export const updateUsuario = (id, body) =>
  request(`/usuarios/${id}`, { method: "PATCH", body });

export const cambiarEstadoUsuario = (id, activo) =>
  request(`/usuarios/${id}/estado`, { method: "PATCH", body: { activo } });

export const restablecerContrasena = (id, contrasenaNueva) =>
  request(`/usuarios/${id}/contrasena`, {
    method: "POST",
    body: { contrasena_nueva: contrasenaNueva },
  });

export const cambiarMiContrasena = (actual, nueva) =>
  request("/auth/contrasena", {
    method: "PATCH",
    body: { contrasena_actual: actual, contrasena_nueva: nueva },
  });
