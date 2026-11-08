import { useLayoutEffect, useRef, useState } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import gsap from 'gsap';
import { useAuth } from '../auth/useAuth';
import { Button, Field, Notice } from '../components/ui';
import styles from './Login.module.css';

const ROAD_PATH = 'M-60 930 C 180 770 560 740 500 560 C 440 400 140 430 220 270 C 300 120 660 170 980 -40';

function Road() {
  const revealRef = useRef(null);

  useLayoutEffect(() => {
    const media = gsap.matchMedia();
    media.add('(prefers-reduced-motion: no-preference)', () => {
      gsap.fromTo(revealRef.current, { strokeDashoffset: 1 }, { strokeDashoffset: 0, duration: 1.1, ease: 'power2.out' });
    });
    return () => media.revert();
  }, []);

  return (
    <svg className={styles.road} viewBox="0 0 920 900" preserveAspectRatio="xMidYMid slice" fill="none" aria-hidden="true">
      <mask id="revelar-carretera" maskUnits="userSpaceOnUse" x="-100" y="-100" width="1200" height="1200">
        <path ref={revealRef} d={ROAD_PATH} stroke="#fff" strokeWidth="110" pathLength="1" strokeDasharray="1" />
      </mask>
      <g mask="url(#revelar-carretera)">
        <path d={ROAD_PATH} stroke="var(--hoja-400)" strokeWidth="86" />
        <path d={ROAD_PATH} stroke="var(--bosque-800)" strokeWidth="74" />
        <path d={ROAD_PATH} stroke="var(--hoja-200)" strokeWidth="3" strokeDasharray="26 22" />
      </g>
    </svg>
  );
}

export default function Login() {
  const { usuario, login, sesionExpirada } = useAuth();
  const location = useLocation();
  const [correo, setCorreo] = useState('');
  const [contrasena, setContrasena] = useState('');
  const [errores, setErrores] = useState({});
  const [errorGeneral, setErrorGeneral] = useState('');
  const [enviando, setEnviando] = useState(false);

  if (usuario) return <Navigate to={location.state?.desde ?? '/'} replace />;

  const enviar = async (event) => {
    event.preventDefault();
    const nuevos = {};
    if (!correo.trim()) nuevos.correo = 'Escribe tu correo electrónico.';
    if (!contrasena) nuevos.contrasena = 'Escribe tu contraseña.';
    setErrores(nuevos);
    setErrorGeneral('');
    if (Object.keys(nuevos).length > 0) return;

    setEnviando(true);
    try {
      await login(correo.trim(), contrasena);
    } catch (error) {
      setErrorGeneral(error.message);
      setEnviando(false);
    }
  };

  return (
    <div className={styles.page}>
      <title>Iniciar sesión · EcoTrack</title>
      <main className={styles.panel}>
        <img src="/logo.png" alt="EcoTrack" className={styles.logo} width="216" height="73" />
        <form className={styles.form} onSubmit={enviar} noValidate>
          <h1 className={styles.title}>Iniciar sesión</h1>
          {sesionExpirada && !errorGeneral && (
            <Notice tono="info" titulo="Tu sesión expiró">
              Vuelve a entrar para continuar.
            </Notice>
          )}
          {errorGeneral && (
            <Notice tono="peligro" titulo="No pudimos iniciar sesión" role="alert">
              {errorGeneral}
            </Notice>
          )}
          <Field
            label="Correo electrónico"
            type="email"
            autoComplete="username"
            placeholder="nombre@empresa.sv"
            value={correo}
            onChange={(event) => setCorreo(event.target.value)}
            error={errores.correo}
            autoFocus
          />
          <Field
            label="Contraseña"
            type="password"
            autoComplete="current-password"
            value={contrasena}
            onChange={(event) => setContrasena(event.target.value)}
            error={errores.contrasena}
          />
          <Button type="submit" disabled={enviando} className={styles.submit}>
            {enviando ? 'Entrando…' : 'Entrar'}
          </Button>
        </form>
        <p className={styles.ayuda}>¿No puedes entrar? Pide a un administrador que restablezca tu contraseña.</p>
      </main>
      <aside className={styles.mundo}>
        <Road />
        <div className={styles.formula}>
          <p className={styles.formulaTexto}>CO₂ = distancia × factor</p>
          <p>Cada cifra conserva el factor con el que se calculó.</p>
        </div>
      </aside>
    </div>
  );
}
