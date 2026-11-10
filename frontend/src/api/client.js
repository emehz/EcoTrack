const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5070/api/v1';
const SESSION_KEY = 'ecotrack.sesion';

export class ApiError extends Error {
  constructor(status, { code, message, details = [] }) {
    super(message);
    this.status = status;
    this.code = code;
    this.details = details;
  }
}

let onSessionExpired = () => {};

export function setSessionExpiredHandler(handler) {
  onSessionExpired = handler;
}

export function readSession() {
  try {
    const stored = JSON.parse(sessionStorage.getItem(SESSION_KEY));
    return stored && new Date(stored.expiraEn) > new Date() ? stored : null;
  } catch {
    return null;
  }
}

export function saveSession(session) {
  sessionStorage.setItem(SESSION_KEY, JSON.stringify(session));
}

export function clearSession() {
  sessionStorage.removeItem(SESSION_KEY);
}

export async function request(path, { method = 'GET', body, signal, params } = {}) {
  const url = new URL(API_BASE_URL + path);
  Object.entries(params ?? {}).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== '') url.searchParams.set(key, value);
  });

  const session = readSession();
  const headers = {};
  if (body) headers['Content-Type'] = 'application/json';
  if (session) headers.Authorization = `Bearer ${session.token}`;

  let response;
  try {
    response = await fetch(url, { method, headers, signal, body: body ? JSON.stringify(body) : undefined });
  } catch (error) {
    if (error.name === 'AbortError') throw error;
    throw new ApiError(0, {
      code: 'SIN_CONEXION',
      message: 'No pudimos conectar con el servidor. Revisa tu conexión e inténtalo de nuevo.',
    });
  }

  const payload = await response.json().catch(() => null);

  if (response.ok) return payload;

  if (response.status === 401 && session) {
    clearSession();
    onSessionExpired();
  }

  throw new ApiError(
    response.status,
    payload?.error ?? { code: 'ERROR', message: 'Ocurrió un error. Inténtalo de nuevo.' },
  );
}

export async function download(path, nombre) {
  const session = readSession();
  let response;
  try {
    response = await fetch(API_BASE_URL + path, {
      headers: session ? { Authorization: `Bearer ${session.token}` } : {},
    });
  } catch {
    throw new ApiError(0, {
      code: 'SIN_CONEXION',
      message: 'No pudimos conectar con el servidor. Revisa tu conexión e inténtalo de nuevo.',
    });
  }

  if (!response.ok) {
    const payload = await response.json().catch(() => null);
    if (response.status === 401 && session) {
      clearSession();
      onSessionExpired();
    }
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
