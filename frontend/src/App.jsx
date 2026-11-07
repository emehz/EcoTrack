import { lazy, Suspense } from 'react';
import { BrowserRouter, Navigate, Outlet, Route, Routes, useLocation } from 'react-router-dom';
import { Toaster } from 'sonner';
import { AuthProvider } from './auth/AuthContext';
import { useAuth } from './auth/useAuth';
import { AppLayout } from './components/AppLayout';
import { EmptyState, Button } from './components/ui';

const Login = lazy(() => import('./pages/Login'));
const Panel = lazy(() => import('./pages/Panel'));
const Vehiculos = lazy(() => import('./pages/Vehiculos'));
const VehiculoForm = lazy(() => import('./pages/VehiculoForm'));
const VehiculoDetalle = lazy(() => import('./pages/VehiculoDetalle'));
const Perfil = lazy(() => import('./pages/Perfil'));

function RequireAuth() {
  const { usuario, cargando } = useAuth();
  const location = useLocation();
  if (cargando) return <div aria-busy="true" />;
  if (!usuario) return <Navigate to="/login" replace state={{ desde: location.pathname }} />;
  return <AppLayout />;
}

function RequireWrite() {
  const { puedeEscribir } = useAuth();
  if (puedeEscribir) return <Outlet />;
  return (
    <EmptyState titulo="No tienes permiso para esta acción" accion={<Button to="/vehiculos" variant="secundario">Volver a vehículos</Button>}>
      Tu rol permite consultar vehículos. Los registra y modifica un administrador.
    </EmptyState>
  );
}

function NotFound() {
  return (
    <EmptyState titulo="Esta página no existe" accion={<Button to="/" variant="secundario">Volver al panel</Button>}>
      Revisa la dirección o vuelve al panel.
    </EmptyState>
  );
}

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <Suspense fallback={<div aria-busy="true" />}>
          <Routes>
            <Route path="/login" element={<Login />} />
            <Route element={<RequireAuth />}>
              <Route index element={<Panel />} />
              <Route path="vehiculos" element={<Vehiculos />} />
              <Route element={<RequireWrite />}>
                <Route path="vehiculos/nuevo" element={<VehiculoForm />} />
                <Route path="vehiculos/:id/editar" element={<VehiculoForm />} />
              </Route>
              <Route path="vehiculos/:id" element={<VehiculoDetalle />} />
              <Route path="perfil" element={<Perfil />} />
              <Route path="*" element={<NotFound />} />
            </Route>
          </Routes>
        </Suspense>
      </AuthProvider>
      <Toaster
        position="bottom-right"
        toastOptions={{
          classNames: { description: 'toast-descripcion' },
          style: {
            background: 'var(--bosque-800)',
            border: 'none',
            borderRadius: 'var(--radio-m)',
            color: 'var(--blanco)',
            fontFamily: 'var(--fuente)',
            fontSize: '14px',
          },
        }}
      />
    </BrowserRouter>
  );
}
