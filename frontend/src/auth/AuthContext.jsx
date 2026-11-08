import { useCallback, useEffect, useMemo, useState } from 'react';
import { clearSession, readSession, saveSession, setSessionExpiredHandler } from '../api/client';
import { getPerfil, login as loginRequest } from '../api/endpoints';
import { AuthContext } from './useAuth';

export function AuthProvider({ children }) {
  const [usuario, setUsuario] = useState(null);
  const [cargando, setCargando] = useState(() => readSession() !== null);
  const [sesionExpirada, setSesionExpirada] = useState(false);
  const [expiraEn, setExpiraEn] = useState(() => readSession()?.expiraEn ?? null);

  const expirar = useCallback(() => {
    clearSession();
    setUsuario(null);
    setExpiraEn(null);
    setSesionExpirada(true);
  }, []);

  useEffect(() => {
    setSessionExpiredHandler(expirar);
  }, [expirar]);

  useEffect(() => {
    if (!readSession()) return undefined;
    const controller = new AbortController();
    getPerfil(controller.signal)
      .then(({ data }) => setUsuario(data))
      .catch(() => {})
      .finally(() => {
        if (!controller.signal.aborted) setCargando(false);
      });
    return () => controller.abort();
  }, []);

  useEffect(() => {
    if (!expiraEn) return undefined;
    const timer = setTimeout(expirar, new Date(expiraEn) - new Date());
    return () => clearTimeout(timer);
  }, [expiraEn, expirar]);

  const login = useCallback(async (correo, contrasena) => {
    const { data } = await loginRequest(correo, contrasena);
    saveSession({ token: data.token, expiraEn: data.expira_en });
    setExpiraEn(data.expira_en);
    setSesionExpirada(false);
    setUsuario(data.usuario);
  }, []);

  const logout = useCallback(() => {
    clearSession();
    setUsuario(null);
    setExpiraEn(null);
    setSesionExpirada(false);
  }, []);

  const value = useMemo(
    () => ({
      usuario,
      cargando,
      sesionExpirada,
      login,
      logout,
      puedeEscribir: usuario?.rol === 'admin',
    }),
    [usuario, cargando, sesionExpirada, login, logout],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
