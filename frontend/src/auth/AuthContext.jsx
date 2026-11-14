import { useCallback, useEffect, useMemo, useState } from 'react';
import { clearSession, refreshSession, saveSession, setSessionHandlers } from '../api/client';
import { login as loginRequest, logout as logoutRequest } from '../api/endpoints';
import { AuthContext } from './useAuth';

const ANTICIPO_MS = 60 * 1000;

export function AuthProvider({ children }) {
  const [usuario, setUsuario] = useState(null);
  const [cargando, setCargando] = useState(true);
  const [sesionExpirada, setSesionExpirada] = useState(false);
  const [expiraEn, setExpiraEn] = useState(null);

  const expirar = useCallback(() => {
    clearSession();
    setUsuario(null);
    setExpiraEn(null);
    setSesionExpirada(true);
  }, []);

  const iniciar = useCallback((data) => {
    saveSession({ token: data.token, expiraEn: data.expira_en });
    setExpiraEn(data.expira_en);
    setSesionExpirada(false);
    setUsuario(data.usuario);
  }, []);

  useEffect(() => {
    setSessionHandlers({
      expirada: expirar,
      renovada: (data) => {
        setExpiraEn(data.expira_en);
        setUsuario(data.usuario);
      },
    });
  }, [expirar]);

  useEffect(() => {
    let vigente = true;
    refreshSession()
      .then((data) => {
        if (vigente && data) iniciar(data);
      })
      .catch(() => {})
      .finally(() => {
        if (vigente) setCargando(false);
      });
    return () => {
      vigente = false;
    };
  }, [iniciar]);

  useEffect(() => {
    if (!expiraEn) return undefined;
    const espera = Math.max(new Date(expiraEn) - new Date() - ANTICIPO_MS, 0);
    const timer = setTimeout(() => {
      refreshSession()
        .then((data) => {
          if (!data) expirar();
        })
        .catch(() => {});
    }, espera);
    return () => clearTimeout(timer);
  }, [expiraEn, expirar]);

  const login = useCallback(
    async (correo, contrasena) => {
      const { data } = await loginRequest(correo, contrasena);
      iniciar(data);
    },
    [iniciar],
  );

  const logout = useCallback(async () => {
    await logoutRequest().catch(() => {});
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
      renovar: iniciar,
      logout,
      puedeEscribir: usuario?.rol === 'admin',
    }),
    [usuario, cargando, sesionExpirada, login, iniciar, logout],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
