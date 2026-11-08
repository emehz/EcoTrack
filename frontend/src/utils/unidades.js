const LITROS_POR_GALON = 3.785411784;

export const litrosAGalones = (litros) => litros / LITROS_POR_GALON;

export const galonesALitros = (galones) => Math.round(galones * LITROS_POR_GALON * 100) / 100;

export const precioLitroAGalon = (precioLitro) => precioLitro * LITROS_POR_GALON;

export const precioGalonALitro = (precioGalon) => Math.round((precioGalon / LITROS_POR_GALON) * 10000) / 10000;
