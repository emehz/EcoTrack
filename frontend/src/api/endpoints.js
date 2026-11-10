import { download, request } from './client';

export const login = (correo, contrasena) =>
  request('/auth/login', { method: 'POST', body: { correo, contrasena } });

export const getPerfil = (signal) => request('/auth/perfil', { signal });

export const getCatalogos = (signal) => request('/catalogos', { signal });

export const getFactorVigente = (tipoVehiculo, tipoCombustible, signal) =>
  request('/factores/vigente', {
    signal,
    params: { tipo_vehiculo: tipoVehiculo, tipo_combustible: tipoCombustible },
  });

export const createFactor = (body) => request('/factores', { method: 'POST', body });

export const getVehiculos = (params, signal) => request('/vehiculos', { params, signal });

export const getVehiculo = (id, signal) => request(`/vehiculos/${id}`, { signal });

export const createVehiculo = (body) => request('/vehiculos', { method: 'POST', body });

export const updateVehiculo = (id, body) => request(`/vehiculos/${id}`, { method: 'PATCH', body });

export const darDeBajaVehiculo = (id) =>
  request(`/vehiculos/${id}/estado`, { method: 'PATCH', body: { estado: 'inactivo' } });

export const getReportes = (params, signal) => request('/reportes', { params, signal });

export const getReporte = (id, signal) => request(`/reportes/${id}`, { signal });

export const createReporte = (body) => request('/reportes', { method: 'POST', body });

export const getFilasReporte = (id, version, params, signal) =>
  request(`/reportes/${id}/versiones/${version}/filas`, { params, signal });

export const reprocesarVersion = (id, version) =>
  request(`/reportes/${id}/versiones/${version}/reprocesar`, { method: 'POST' });

export const descargarCsvReporte = (id, version, nombre) => download(`/reportes/${id}/versiones/${version}/csv`, nombre);

export const getAuditorias = (params, signal) => request('/auditorias', { params, signal });

export const getAuditoria = (id, signal) => request(`/auditorias/${id}`, { signal });

export const iniciarAuditoria = (reporteId, body) =>
  request(`/reportes/${reporteId}/auditorias`, { method: 'POST', body });

export const registrarHallazgo = (id, body) => request(`/auditorias/${id}/hallazgos`, { method: 'POST', body });

export const cambiarEstadoHallazgo = (id, hallazgoId, body) =>
  request(`/auditorias/${id}/hallazgos/${hallazgoId}`, { method: 'PATCH', body });

export const cerrarAuditoria = (id, body) => request(`/auditorias/${id}/cerrar`, { method: 'POST', body });
