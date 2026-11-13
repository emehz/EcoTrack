import { lazy, Suspense } from "react";
import {
  BrowserRouter,
  Navigate,
  Outlet,
  Route,
  Routes,
  useLocation,
} from "react-router-dom";
import { Toaster } from "sonner";
import { AuthProvider } from "./auth/AuthContext";
import { useAuth } from "./auth/useAuth";
import { AppLayout } from "./components/AppLayout";
import { EmptyState, Button } from "./components/ui";

const Login = lazy(() => import("./pages/Login"));
const Panel = lazy(() => import("./pages/Panel"));
const Vehiculos = lazy(() => import("./pages/Vehiculos"));
const VehiculoForm = lazy(() => import("./pages/VehiculoForm"));
const VehiculoDetalle = lazy(() => import("./pages/VehiculoDetalle"));
const Perfil = lazy(() => import("./pages/Perfil"));
const Usuarios = lazy(() => import("./pages/Usuarios"));
const UsuarioForm = lazy(() => import("./pages/UsuarioForm"));
const Choferes = lazy(() => import("./pages/Choferes"));
const ChoferForm = lazy(() => import("./pages/ChoferForm"));
const ChoferDetalle = lazy(() => import("./pages/ChoferDetalle"));
const Factores = lazy(() => import("./pages/Factores"));
const Viajes = lazy(() => import("./pages/Viajes"));
const ViajeForm = lazy(() => import("./pages/ViajeForm"));
const ViajeDetalle = lazy(() => import("./pages/ViajeDetalle"));
const Combustible = lazy(() => import("./pages/Combustible"));
const Bitacora = lazy(() => import("./pages/Bitacora"));
const Reportes = lazy(() => import("./pages/Reportes"));
const ReporteNuevo = lazy(() => import("./pages/ReporteNuevo"));
const ReporteDetalle = lazy(() => import("./pages/ReporteDetalle"));
const Auditorias = lazy(() => import("./pages/Auditorias"));
const AuditoriaDetalle = lazy(() => import("./pages/AuditoriaDetalle"));

function RequireAuth() {
  const { usuario, cargando } = useAuth();
  const location = useLocation();
  if (cargando) return <div aria-busy="true" />;
  if (!usuario)
    return (
      <Navigate to="/login" replace state={{ desde: location.pathname }} />
    );
  return <AppLayout />;
}

function RequireWrite() {
  const { puedeEscribir } = useAuth();
  if (puedeEscribir) return <Outlet />;
  return (
    <EmptyState
      titulo="No tienes permiso para esta acción"
      accion={
        <Button to="/vehiculos" variant="secundario">
          Volver a vehículos
        </Button>
      }
    >
      Tu rol permite consultar vehículos. Los registra y modifica un
      administrador.
    </EmptyState>
  );
}

function RequireRoles({ roles }) {
  const { usuario } = useAuth();
  if (roles.includes(usuario?.rol)) return <Outlet />;
  return (
    <EmptyState
      titulo="No tienes permiso para esta sección"
      accion={
        <Button to="/" variant="secundario">
          Volver al panel
        </Button>
      }
    >
      Tu rol no tiene acceso a esta pantalla.
    </EmptyState>
  );
}

function NotFound() {
  return (
    <EmptyState
      titulo="Esta página no existe"
      accion={
        <Button to="/" variant="secundario">
          Volver al panel
        </Button>
      }
    >
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
              <Route element={<RequireRoles roles={["admin"]} />}>
                <Route path="usuarios" element={<Usuarios />} />
                <Route path="usuarios/nuevo" element={<UsuarioForm />} />
                <Route path="usuarios/:id/editar" element={<UsuarioForm />} />
              </Route>
              <Route path="choferes" element={<Choferes />} />
              <Route element={<RequireRoles roles={["admin"]} />}>
                <Route path="choferes/nuevo" element={<ChoferForm />} />
                <Route path="choferes/:id/editar" element={<ChoferForm />} />
              </Route>
              <Route path="choferes/:id" element={<ChoferDetalle />} />
              <Route path="factores" element={<Factores />} />
              <Route path="viajes" element={<Viajes />} />
              <Route element={<RequireRoles roles={["admin", "operador"]} />}>
                <Route path="viajes/nuevo" element={<ViajeForm />} />
                <Route path="viajes/:id/editar" element={<ViajeForm />} />
              </Route>
              <Route path="viajes/:id" element={<ViajeDetalle />} />
              <Route path="combustible" element={<Combustible />} />
              <Route path="reportes" element={<Reportes />} />
              <Route element={<RequireRoles roles={["admin", "operador"]} />}>
                <Route path="reportes/nuevo" element={<ReporteNuevo />} />
              </Route>
              <Route path="reportes/:id" element={<ReporteDetalle />} />
              <Route element={<RequireRoles roles={["admin", "auditor"]} />}>
                <Route path="auditorias" element={<Auditorias />} />
                <Route path="auditorias/:id" element={<AuditoriaDetalle />} />
                <Route path="bitacora" element={<Bitacora />} />
              </Route>
              <Route path="*" element={<NotFound />} />
            </Route>
          </Routes>
        </Suspense>
      </AuthProvider>
      <Toaster
        position="bottom-right"
        toastOptions={{
          classNames: { description: "toast-descripcion" },
          style: {
            background: "var(--bosque-800)",
            border: "none",
            borderRadius: "var(--radio-m)",
            color: "var(--blanco)",
            fontFamily: "var(--fuente)",
            fontSize: "14px",
          },
        }}
      />
    </BrowserRouter>
  );
}
