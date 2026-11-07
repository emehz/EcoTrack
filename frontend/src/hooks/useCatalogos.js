import { useEffect, useState } from 'react';
import { getCatalogos } from '../api/endpoints';

export function useCatalogos() {
  const [catalogos, setCatalogos] = useState(null);
  const [error, setError] = useState(null);

  useEffect(() => {
    const controller = new AbortController();
    getCatalogos(controller.signal)
      .then(({ data }) => setCatalogos(data))
      .catch((falla) => {
        if (falla.name !== 'AbortError') setError(falla);
      });
    return () => controller.abort();
  }, []);

  return { catalogos, error };
}

export const etiquetaDe = (opciones, valor) => opciones?.find((opcion) => opcion.valor === valor)?.etiqueta ?? valor;
