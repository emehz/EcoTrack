import { NavLink, Outlet, useLocation } from 'react-router-dom';
import {
  Calculator,
  FileText,
  Fuel,
  IdCard,
  LayoutDashboard,
  LogOut,
  Route,
  ScrollText,
  ShieldCheck,
  Truck,
  UserRound,
  Users,
} from 'lucide-react';
import { clsx } from 'clsx';
import { useAuth } from '../auth/useAuth';
import { ErrorBoundary } from './ErrorBoundary';
import styles from './AppLayout.module.css';

const SECCIONES = [
  { items: [{ texto: 'Panel', to: '/', icono: LayoutDashboard }] },
  {
    titulo: 'Operación',
    items: [
      { texto: 'Vehículos', to: '/vehiculos', icono: Truck },
      { texto: 'Viajes', icono: Route },
      { texto: 'Combustible', icono: Fuel },
      { texto: 'Choferes', icono: IdCard },
    ],
  },
  {
    titulo: 'Datos de emisión',
    items: [
      { texto: 'Factores', icono: Calculator },
    ],
  },
  {
    titulo: 'Reportes',
    items: [
      { texto: 'Reportes', to: '/reportes', icono: FileText },
      { texto: 'Auditorías', to: '/auditorias', icono: ShieldCheck, roles: ['admin', 'auditor'] },
    ],
  },
  {
    titulo: 'Administración',
    roles: ['admin', 'auditor'],
    items: [
      { texto: 'Usuarios', icono: Users, roles: ['admin'] },
      { texto: 'Bitácora', icono: ScrollText },
    ],
  },
];

const ROLES = { admin: 'Administrador', operador: 'Operador', auditor: 'Auditor' };

export function AppLayout() {
  const { usuario, logout } = useAuth();
  const { pathname } = useLocation();
  const visible = ({ roles }) => !roles || roles.includes(usuario.rol);

  return (
    <div className={styles.shell}>
      <a href="#contenido" className={styles.skip}>
        Saltar al contenido
      </a>
      <aside className={styles.rail}>
        <picture>
          <source media="(max-width: 1100px)" srcSet="/favicon.png" width="40" height="40" />
          <img src="/logo.png" alt="EcoTrack" className={styles.logo} width="176" height="59" />
        </picture>
        <div className={styles.franja} aria-hidden="true" />
        <nav aria-label="Principal" className={styles.nav}>
          {SECCIONES.filter(visible).map((seccion, indice) => (
            <div key={seccion.titulo ?? indice}>
              {seccion.titulo && <p className={styles.grupo}>{seccion.titulo}</p>}
              <ul>
                {seccion.items.filter(visible).map(({ texto, to, icono: Icono }) => (
                  <li key={texto}>
                    {to ? (
                      <NavLink
                        to={to}
                        end={to === '/'}
                        title={texto}
                        className={({ isActive }) => clsx(styles.item, isActive && styles.activo)}
                      >
                        <Icono size={16} strokeWidth={1.75} aria-hidden="true" />
                        <span className={styles.texto}>{texto}</span>
                      </NavLink>
                    ) : (
                      <span
                        className={clsx(styles.item, styles.deshabilitado)}
                        role="link"
                        aria-disabled="true"
                        title={`${texto}: disponible en la entrega final`}
                      >
                        <Icono size={16} strokeWidth={1.75} aria-hidden="true" />
                        <span className={styles.texto}>{texto}</span>
                      </span>
                    )}
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </nav>
        <div className={styles.pie}>
          <NavLink
            to="/perfil"
            className={styles.usuario}
            title={`${usuario.nombres} ${usuario.apellidos}`}
            aria-label={`Mi perfil: ${usuario.nombres} ${usuario.apellidos}`}
          >
            <UserRound size={16} strokeWidth={1.75} className={styles.iconoUsuario} aria-hidden="true" />
            <span className={styles.nombre}>
              {usuario.nombres} {usuario.apellidos}
            </span>
            <span className={styles.rol}>{ROLES[usuario.rol]}</span>
          </NavLink>
          <button type="button" className={styles.salir} onClick={logout} title="Cerrar sesión" aria-label="Cerrar sesión">
            <LogOut size={14} strokeWidth={1.75} aria-hidden="true" />
            <span className={styles.texto}>Salir</span>
          </button>
        </div>
      </aside>
      <main id="contenido" className={styles.main} tabIndex={-1}>
        <ErrorBoundary key={pathname}>
          <Outlet />
        </ErrorBoundary>
      </main>
    </div>
  );
}
