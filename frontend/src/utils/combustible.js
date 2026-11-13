import { galonesALitros, litrosAGalones, precioGalonALitro, precioLitroAGalon } from './unidades';

export const UNIDADES = [
  { valor: 'galones', etiqueta: 'Galones' },
  { valor: 'litros', etiqueta: 'Litros' },
];

export const leerNumero = (texto) => {
  const valor = Number(String(texto).replace(',', '.'));
  return String(texto).trim() === '' || Number.isNaN(valor) ? null : valor;
};

export const aLitros = (cantidad, unidad) => (unidad === 'galones' ? galonesALitros(cantidad) : Math.round(cantidad * 100) / 100);

export const precioPorLitro = (precio, unidad) => (unidad === 'galones' ? precioGalonALitro(precio) : precio);

export const desdeLitros = (litros, unidad) => (unidad === 'galones' ? litrosAGalones(litros) : litros);

export const precioEnUnidad = (precioLitro, unidad) => (unidad === 'galones' ? precioLitroAGalon(precioLitro) : precioLitro);

export const kmPorGalon = (kmPorLitro) => precioLitroAGalon(kmPorLitro);

export const puedeRegistrarCargas = (rol) => rol === 'admin' || rol === 'operador';

export const formatoDinero = (valor) =>
  `$${Number(valor ?? 0).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
