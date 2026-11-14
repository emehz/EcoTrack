# Colección de Postman

| Archivo | Para qué |
|---|---|
| `EcoTrack.postman_collection.json` | Las peticiones de todos los módulos, con pruebas de cada respuesta |
| `EcoTrack-local.postman_environment.json` | Entorno para la API local (`http://localhost:5070/api/v1`) |
| `EcoTrack-nube.postman_environment.json` | Entorno para la EC2 (cambiar `IP-DE-LA-EC2` por la dirección real) |

## Uso

1. En Postman: Import y elegir los tres archivos.
2. Elegir el entorno (arriba a la derecha) y escribir `contrasena` y `contrasena_auditor` en sus variables. Los archivos no traen contraseñas.
3. Ejecutar las carpetas 01 a 12 con el Runner, en orden. Crean su propio vehículo, chofer, viaje, carga, reporte, auditoría y usuario de prueba, y al final los dan de baja. Se pueden ejecutar varias veces.
4. La carpeta 13 no va en el Runner: cambia contraseñas, factores o cierra auditorías. Se usa a mano, ajustando los valores.

El token se guarda solo al iniciar sesión. La renovación usa la cookie `ecotrack_refresh`, que Postman guarda como un navegador; por eso el auditor inicia sesión antes que el administrador.

## Desde la terminal (opcional)

```
npx newman run EcoTrack.postman_collection.json -e EcoTrack-local.postman_environment.json --env-var contrasena=... --env-var contrasena_auditor=...
```

## Lista de pruebas para el despliegue (20 de noviembre)

- [ ] Entorno nube: carpetas 01 a 12 sin fallas.
- [ ] `GET /health` responde `base_de_datos: true` (la EC2 llega a RDS).
- [ ] Iniciar sesión, recargar la página y seguir dentro (cookie de sesión en la misma dirección).
- [ ] Generar un reporte: el CSV y el resultado aparecen en el bucket de S3.
- [ ] Panel, viajes, combustible, factores, reportes, auditorías, usuarios y bitácora cargan en el navegador.
- [ ] Un operador no ve Usuarios ni Bitácora; un auditor no registra viajes.
- [ ] Desactivar un usuario lo saca en su siguiente acción.
