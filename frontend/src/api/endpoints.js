import { request } from './client';

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
