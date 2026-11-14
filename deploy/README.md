# Despliegue en EC2

Arquitectura (Opción B, IaaS): una instancia EC2 con Nginx y la API, la base de datos en RDS y los archivos en S3.

| Recurso | Nombre en AWS |
|---|---|
| VPC | `ecotrack-vpc` (2 subredes públicas y 2 privadas) |
| EC2 (t3.micro, subred pública) | `ecotrack-web-server` |
| Rol IAM de la EC2 | `ecotrack-ec2-s3-role` |
| RDS PostgreSQL (subredes privadas) | `ecotrack-db` |
| Grupo de seguridad de RDS | `ecotrack-rds-sg` |
| Bucket S3 privado | `ecotrack-reportes-s3` (prefijos `reportes/` y `resultados/`) |

- Nginx sirve el frontend (archivos de `npm run build`) en `/var/www/ecotrack`.
- Nginx reenvía `/api/` a la API, que escucha solo en `127.0.0.1:5070`.
- El frontend y la API quedan en la misma dirección, así que no hace falta CORS en producción.

## Archivos

| Archivo | Destino en la EC2 |
|---|---|
| `nginx/ecotrack.conf` | `/etc/nginx/conf.d/ecotrack.conf` |
| `ecotrack-api.service` | `/etc/systemd/system/ecotrack-api.service` |
| `actualizar.sh` | Se ejecuta en cada despliegue |
| `rds/preparar-produccion.sql` | Se ejecuta una vez en RDS, desde la EC2 |

## Primera vez (en la EC2)

1. Instalar Nginx, rsync, el cliente de PostgreSQL (`psql`) y el runtime de ASP.NET Core 10 (`aspnetcore-runtime-10.0`).
2. Crear el usuario del servicio y las carpetas:

   ```
   sudo useradd --system --no-create-home --shell /usr/sbin/nologin ecotrack
   sudo mkdir -p /opt/ecotrack/api /var/www/ecotrack
   ```

3. Copiar `ecotrack.conf` y `ecotrack-api.service` a sus destinos. En Ubuntu, quitar el sitio por defecto (`/etc/nginx/sites-enabled/default`).
4. Crear `/opt/ecotrack/api/.env` a partir de `.env.example`, con:
   - `ASPNETCORE_ENVIRONMENT=Production`
   - la cadena de RDS con el usuario `ecotrack_app`, la clave elegida en el paso de la base y `;SSL Mode=Require` al final
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
7. Grupo de seguridad de RDS (`ecotrack-rds-sg`): permitir 5432 solo desde el grupo de seguridad de la EC2.
8. Almacenamiento en S3: crear el bucket privado (con "Block all public access") y asignar a la EC2 un rol IAM
   con `s3:GetObject` y `s3:PutObject` sobre `arn:aws:s3:::ecotrack-reportes-s3/*` (es `ecotrack-ec2-s3-role`). En el `.env` poner
   `ALMACENAMIENTO=s3`, `S3_BUCKET` y `AWS_REGION`. No se guardan llaves de AWS en el servidor.

## Base de datos en RDS (una vez)

RDS solo acepta conexiones desde la EC2, así que estos pasos se hacen conectado a la EC2, con los archivos del repositorio copiados ahí.

1. Crear la base `ecotrack_db` y ejecutar los scripts en este orden:

   ```
   psql "host=ENDPOINT-DE-ecotrack-db port=5432 dbname=postgres user=USUARIO_MAESTRO sslmode=require" -c "CREATE DATABASE ecotrack_db"
   psql "host=ENDPOINT-DE-ecotrack-db port=5432 dbname=ecotrack_db user=USUARIO_MAESTRO sslmode=require" -v ON_ERROR_STOP=1 -f ecotrack_unificado.sql
   psql "host=ENDPOINT-DE-ecotrack-db port=5432 dbname=ecotrack_db user=USUARIO_MAESTRO sslmode=require" -v ON_ERROR_STOP=1 -f database/sesiones.sql
   ```

2. Dar acceso a la API y poner la clave del administrador:

   ```
   psql "host=ENDPOINT-DE-ecotrack-db port=5432 dbname=ecotrack_db user=USUARIO_MAESTRO sslmode=require" \
        -v clave_app='CLAVE_PARA_LA_API' -v clave_admin='CLAVE_DEL_ADMINISTRADOR' -f deploy/rds/preparar-produccion.sql
   ```

   El script permite que `ecotrack_app` inicie sesión con `clave_app`, guarda `clave_admin` cifrada con bcrypt para `admin@ecotrack.sv` y desactiva las cuentas de prueba del operador y del auditor. Después, el administrador crea las cuentas reales desde Usuarios (o reactiva las de prueba y les restablece la contraseña).

3. No ejecutar `ajustes_desarrollo_local.sql` en RDS: es solo para las bases locales.

## Sesión y HTTPS

La sesión se renueva con la cookie `ecotrack_refresh` (HttpOnly, SameSite=Strict, ruta `/api/v1/auth`). Como Nginx sirve el frontend y la API en la misma dirección, funciona tanto con la IP pública por HTTP como con un dominio por HTTPS. Con HTTPS la cookie se marca además como Secure de forma automática, gracias a la cabecera `X-Forwarded-Proto` que envía Nginx.

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
