const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL ?? (import.meta.env.PROD ? '/api/v1' : 'http://localhost:5070/api/v1');
const REINTENTOS_RENOVACION = 3;

const SIN_CONEXION = {
  code: 'SIN_CONEXION',
  message: 'No pudimos conectar con el servidor. Revisa tu conexión e inténtalo de nuevo.',
};

export class ApiError extends Error {
  constructor(status, { code, message, details = [] }) {
    super(message);
    this.status = status;
    this.code = code;
    this.details = details;
  }
}

let sesion = null;
let renovando = null;
let onSessionExpired = () => {};
let onSessionRenewed = () => {};

export function setSessionHandlers({ expirada, renovada }) {
  onSessionExpired = expirada;
  onSessionRenewed = renovada;
}

export const readSession = () => (sesion && new Date(sesion.expiraEn) > new Date() ? sesion : null);

export function saveSession(nueva) {
  sesion = nueva;
}

export function clearSession() {
  sesion = null;
}

const esperar = (ms) => new Promise((listo) => setTimeout(listo, ms));

async function pedirRenovacion() {
  for (let intento = 1; intento <= REINTENTOS_RENOVACION; intento += 1) {
    let response;
    try {
      response = await fetch(API_BASE_URL + '/auth/refresh', { method: 'POST', credentials: 'include' });
    } catch {
      throw new ApiError(0, SIN_CONEXION);
    }
    if (response.status === 409) {
      await esperar(300 * intento);
      continue;
    }
    if (response.status === 204 || !response.ok) return null;
    const { data } = await response.json();
    return data;
  }
  return null;
}

export function refreshSession() {
  if (!renovando) {
    renovando = pedirRenovacion()
      .then((data) => {
        if (data) {
          sesion = { token: data.token, expiraEn: data.expira_en };
          onSessionRenewed(data);
        }
        return data;
      })
      .finally(() => {
        renovando = null;
      });
  }
  return renovando;
}

async function enviar(url, opciones, esAutenticacion) {
  const peticion = (token) =>
    fetch(url, {
      ...opciones,
      credentials: 'include',
      headers: { ...opciones.headers, ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    });

  let response;
  try {
    response = await peticion(readSession()?.token);
    if (response.status === 401 && !esAutenticacion) {
      const tenia = sesion !== null;
      const renovada = await refreshSession();
      if (renovada) {
        response = await peticion(renovada.token);
      } else if (tenia) {
        clearSession();
        onSessionExpired();
      }
    }
  } catch (error) {
    if (error.name === 'AbortError' || error instanceof ApiError) throw error;
    throw new ApiError(0, SIN_CONEXION);
  }
  return response;
}

export async function request(path, { method = 'GET', body, signal, params } = {}) {
  const url = new URL(API_BASE_URL + path, window.location.origin);
  Object.entries(params ?? {}).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== '') url.searchParams.set(key, value);
  });

  const response = await enviar(
    url,
    {
      method,
      signal,
      headers: body ? { 'Content-Type': 'application/json' } : {},
      body: body ? JSON.stringify(body) : undefined,
    },
    path.startsWith('/auth/') && path !== '/auth/perfil' && path !== '/auth/contrasena',
  );

  const payload = await response.json().catch(() => null);
  if (response.ok) return payload;

  throw new ApiError(
    response.status,
    payload?.error ?? { code: 'ERROR', message: 'Ocurrió un error. Inténtalo de nuevo.' },
  );
}

export async function download(path, nombre) {
  const response = await enviar(new URL(API_BASE_URL + path, window.location.origin), { headers: {} }, false);

  if (!response.ok) {
    const payload = await response.json().catch(() => null);
    throw new ApiError(response.status, payload?.error ?? { code: 'ERROR', message: 'No se pudo descargar el archivo.' });
  }

  const url = URL.createObjectURL(await response.blob());
  const enlace = document.createElement('a');
  enlace.href = url;
  enlace.download = nombre;
  document.body.append(enlace);
  enlace.click();
  enlace.remove();
  URL.revokeObjectURL(url);
}
