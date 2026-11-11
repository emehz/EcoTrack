# Despliegue en EC2

Arquitectura (Opción B, IaaS): una instancia EC2 con Nginx y la API, y la base de datos en RDS.

- Nginx sirve el frontend (archivos de `npm run build`) en `/var/www/ecotrack`.
- Nginx reenvía `/api/` a la API, que escucha solo en `127.0.0.1:5070`.
- El frontend y la API quedan en la misma dirección, así que no hace falta CORS en producción.

## Archivos

| Archivo | Destino en la EC2 |
|---|---|
| `nginx/ecotrack.conf` | `/etc/nginx/conf.d/ecotrack.conf` |
| `ecotrack-api.service` | `/etc/systemd/system/ecotrack-api.service` |
| `actualizar.sh` | Se ejecuta en cada despliegue |

## Primera vez (en la EC2)

1. Instalar Nginx, rsync y el runtime de ASP.NET Core 10 (`aspnetcore-runtime-10.0`).
2. Crear el usuario del servicio y las carpetas:

   ```
   sudo useradd --system --no-create-home --shell /usr/sbin/nologin ecotrack
   sudo mkdir -p /opt/ecotrack/api /var/www/ecotrack
   ```

3. Copiar `ecotrack.conf` y `ecotrack-api.service` a sus destinos. En Ubuntu, quitar el sitio por defecto (`/etc/nginx/sites-enabled/default`).
4. Crear `/opt/ecotrack/api/.env` a partir de `.env.example`, con:
   - `ASPNETCORE_ENVIRONMENT=Production`
   - la cadena de RDS con `;SSL Mode=Require` al final
   - un `JWT_SECRET` propio de producción
   - `CORS_ORIGINS` con la dirección pública de la EC2

   Luego, `sudo chmod 600 /opt/ecotrack/api/.env`.
5. Activar los servicios:

   ```
   sudo nginx -t
   sudo systemctl enable --now nginx
   sudo systemctl daemon-reload
   sudo systemctl enable ecotrack-api
   ```

6. Grupo de seguridad de la EC2: abrir 80 (y 443 si hay certificado) a Internet y 22 solo a las IP del equipo. La API (5070) no se abre.
7. Grupo de seguridad de RDS: permitir 5432 solo desde el grupo de seguridad de la EC2.

## Cada despliegue

En una computadora del equipo:

```
dotnet publish EcoTrack.csproj -c Release -o publish/api
cd frontend
npm ci
npm run build
```

Con eso, `publish/api` tiene la API, y `frontend/dist` el frontend. En el build de producción el frontend llama a `/api/v1` en la misma dirección.

Copiar `publish/api` como `subida/api` y `frontend/dist` como `subida/web` a la EC2 (por ejemplo con WinSCP o `scp -r`), y en la EC2:

```
bash actualizar.sh ~/subida
```

El script copia los archivos sin tocar el `.env`, reinicia la API y consulta `/api/v1/health`.

## Revisar

- Estado de la API: `systemctl status ecotrack-api`
- Registros de la API: `journalctl -u ecotrack-api -n 100`
- Configuración de Nginx: `sudo nginx -t`
