import { Component } from 'react';
import { Button, EmptyState } from './ui';

export class ErrorBoundary extends Component {
  state = { conError: false };

  static getDerivedStateFromError() {
    return { conError: true };
  }

  render() {
    if (!this.state.conError) return this.props.children;

    return (
      <EmptyState
        titulo="Esta pantalla no se pudo mostrar"
        accion={<Button onClick={() => window.location.assign('/')}>Volver al panel</Button>}
      >
        Ocurrió un error inesperado. Tus datos guardados no se perdieron.
      </EmptyState>
    );
  }
}
