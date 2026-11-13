import { useEffect, useState } from 'react';
import { toast } from 'sonner';
import { createCarga, getViajes, updateCarga } from '../api/endpoints';
import { DialogoFormulario } from './DialogoFormulario';
import { Selector } from './Selector';
import { Field } from './ui';
import { formatDate, formatNumber } from '../utils/format';
import { UNIDADES, aLitros, desdeLitros, leerNumero, precioEnUnidad, precioPorLitro } from '../utils/combustible';
import { litrosAGalones } from '../utils/unidades';
import { aEntradaLocal, deEntradaLocal } from '../utils/viajes';

const redondear = (valor, decimales) => String(Math.round(valor * 10 ** decimales) / 10 ** decimales);

function inicial(carga) {
  if (!carga) {
    return { vehiculo_id: '', fecha: aEntradaLocal(), unidad: 'galones', cantidad: '', precio: '', viaje_id: '' };
  }
  return {
    vehiculo_id: carga.vehiculo_id,
    fecha: aEntradaLocal(carga.cargado_en),
    unidad: 'galones',
    cantidad: redondear(desdeLitros(carga.litros, 'galones'), 2),
    precio: carga.precio_por_litro == null ? '' : redondear(precioEnUnidad(carga.precio_por_litro, 'galones'), 4),
    viaje_id: carga.viaje_id ?? '',
  };
}

export function CargaDialog({ carga, vehiculos, onGuardada, onCerrar }) {
  const editando = Boolean(carga);
  const [form, setForm] = useState(() => inicial(carga));
  const [viajes, setViajes] = useState([]);
  const [errores, setErrores] = useState({});
  const [errorGeneral, setErrorGeneral] = useState('');
  const [ocupado, setOcupado] = useState(false);

  useEffect(() => {
    if (!form.vehiculo_id) return undefined;
    const controller = new AbortController();
    getViajes({ vehiculo_id: form.vehiculo_id, estado: 'todos', limit: 30 }, controller.signal)
      .then(({ data }) =>
        setViajes(
          data
            .filter((v) => v.estado !== 'cancelado')
            .map((v) => ({ valor: v.viaje_id, etiqueta: `${formatDate(v.salida_en)} · ${v.origen} › ${v.destino}` })),
        ),
      )
      .catch(() => {});
    return () => controller.abort();
  }, [form.vehiculo_id]);

  const actualizar = (campo, valor) => setForm((actual) => ({ ...actual, [campo]: valor }));

  const cambiarUnidad = (unidad) =>
    setForm((actual) => {
      if (!unidad || unidad === actual.unidad) return actual;
      const cantidad = leerNumero(actual.cantidad);
      const precio = leerNumero(actual.precio);
      const galonesPorLitro = litrosAGalones(1);
      const litros = cantidad === null ? null : actual.unidad === 'galones' ? cantidad / galonesPorLitro : cantidad;
      const precioLitro = precio === null ? null : actual.unidad === 'galones' ? precio * galonesPorLitro : precio;
      return {
        ...actual,
        unidad,
        cantidad: litros === null ? actual.cantidad : redondear(desdeLitros(litros, unidad), 2),
        precio: precioLitro === null ? actual.precio : redondear(precioEnUnidad(precioLitro, unidad), unidad === 'galones' ? 2 : 4),
      };
    });

  const opcionesVehiculo = vehiculos
    .filter((v) => v.tipo_combustible !== 'electrico' && (v.estado === 'activo' || v.vehiculo_id === carga?.vehiculo_id))
    .map((v) => ({ valor: v.vehiculo_id, etiqueta: v.placa }));

  const cantidad = leerNumero(form.cantidad);
  const precio = leerNumero(form.precio);
  const nombreUnidad = form.unidad === 'galones' ? 'galón' : 'litro';

  const guardar = async () => {
    const nuevos = {};
    if (!form.vehiculo_id) nuevos.vehiculo_id = 'Elige el vehículo.';
    if (!form.fecha) nuevos.cargado_en = 'Indica la fecha y hora de la carga.';
    if (cantidad === null || cantidad <= 0) nuevos.litros = 'Escribe una cantidad mayor que cero.';
    if (form.precio !== '' && (precio === null || precio < 0)) nuevos.precio_por_litro = 'Escribe un precio válido o déjalo vacío.';
    setErrores(nuevos);
    setErrorGeneral('');
    if (Object.keys(nuevos).length > 0) return;

    setOcupado(true);
    const cuerpo = {
      vehiculo_id: form.vehiculo_id,
      cargado_en: deEntradaLocal(form.fecha),
      litros: aLitros(cantidad, form.unidad),
      precio_por_litro: precio === null ? null : precioPorLitro(precio, form.unidad),
      viaje_id: form.viaje_id || null,
    };
    try {
      const { data } = editando ? await updateCarga(carga.carga_combustible_id, cuerpo) : await createCarga(cuerpo);
      toast.success(editando ? 'Carga corregida' : 'Carga registrada', {
        description: `${data.placa} · ${formatNumber(desdeLitros(data.litros, 'galones'))} gal (${formatNumber(data.litros)} L)`,
      });
      onGuardada(data);
    } catch (falla) {
      const porCampo = Object.fromEntries((falla.details ?? []).map(({ campo, mensaje }) => [campo, mensaje]));
      setErrores(porCampo);
      if (Object.keys(porCampo).length === 0) setErrorGeneral(falla.message);
    } finally {
      setOcupado(false);
    }
  };

  return (
    <DialogoFormulario
      abierto
      titulo={editando ? 'Corregir carga' : 'Registrar carga'}
      descripcion="Se guarda en litros. Puedes escribirla en galones o en litros."
      accion={editando ? 'Guardar cambios' : 'Registrar carga'}
      ocupado={ocupado}
      error={errorGeneral}
      onEnviar={guardar}
      onCerrar={onCerrar}
    >
      <Selector
        label="Vehículo"
        opciones={opcionesVehiculo}
        value={form.vehiculo_id}
        onChange={(valor) => setForm((actual) => ({ ...actual, vehiculo_id: valor, viaje_id: '' }))}
        placeholder="Elige un vehículo"
        error={errores.vehiculo_id}
        help="Los vehículos eléctricos no aparecen porque no cargan combustible."
      />
      <Field
        label="Fecha y hora (El Salvador)"
        type="datetime-local"
        value={form.fecha}
        onChange={(event) => actualizar('fecha', event.target.value)}
        error={errores.cargado_en}
      />
      <Selector label="Unidad" opciones={UNIDADES} value={form.unidad} onChange={cambiarUnidad} />
      <Field
        label={`Cantidad (${form.unidad})`}
        inputMode="decimal"
        value={form.cantidad}
        onChange={(event) => actualizar('cantidad', event.target.value)}
        error={errores.litros}
        help={
          cantidad > 0 && form.unidad === 'galones' ? `Equivale a ${formatNumber(aLitros(cantidad, 'galones'))} litros.` : undefined
        }
      />
      <Field
        label={`Precio por ${nombreUnidad} (opcional)`}
        inputMode="decimal"
        placeholder="4.29"
        value={form.precio}
        onChange={(event) => actualizar('precio', event.target.value)}
        error={errores.precio_por_litro}
      />
      <Selector
        label="Viaje asociado (opcional)"
        opciones={viajes}
        opcionVacia="Sin viaje"
        value={form.viaje_id}
        onChange={(valor) => actualizar('viaje_id', valor)}
        placeholder={form.vehiculo_id ? 'Sin viaje' : 'Primero elige el vehículo'}
        error={errores.viaje_id}
      />
    </DialogoFormulario>
  );
}
