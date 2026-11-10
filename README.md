DOCUMENTACION DE INFRAESTRUCTURA AWS - PROYECTO ECOTRACK

Este documento detalla la infraestructura aprovisionada en Amazon Web Services (AWS) para el backend de la plataforma EcoTrack. La arquitectura sigue el modelo IaaS (Infraestructura como Servicio), cumpliendo con los estandares de seguridad y manteniendose estrictamente dentro de la capa gratuita (Free Tier).

REGION Y ENTORNO

Region Principal: us-east-2 (Ohio).

Nota: Todos los recursos (VPC, EC2, RDS, S3) deben residir en esta region para permitir su comunicacion interna y evitar costos de transferencia de datos.

Entorno: Produccion / Sandbox Academico.

INVENTARIO DE RECURSOS APROVISIONADOS

2.1 Red (Amazon VPC)

VPC: ecotrack-vpc (CIDR: 10.0.0.0/16).

Subredes Publicas: 10.0.0.0/24 y 10.0.2.0/24 (Zonas: us-east-2a, us-east-2b). Albergan la instancia EC2.

Subredes Privadas: 10.0.1.0/24 y 10.0.3.0/24 (Zonas: us-east-2a, us-east-2b). Albergan la base de datos RDS.

Internet Gateway: ecotrack-igw (Asociado a la VPC para proporcionar salida a internet a la subred publica).

2.2 Computo (Amazon EC2)

Instancia: ecotrack-web-server.

Tipo: t3.micro (Elegible para el Free Tier: 750 horas/mes).

Sistema Operativo: Amazon Linux 2023.

Ubicacion: Subred Publica en us-east-2a.

Rol IAM Adjunto: ecotrack-ec2-s3-role (Permite a la instancia interactuar con S3 sin credenciales estaticas).

Software a instalar: runtime de ASP.NET Core (.NET 10) para la API y Nginx como proxy inverso. El frontend se publica como archivos estaticos generados con npm run build.

2.3 Base de Datos (Amazon RDS)

Motor: PostgreSQL.

Identificador: ecotrack-db.

Clase de Instancia: db.t3.micro (Elegible para el Free Tier).

Almacenamiento: 20 GiB (SSD de uso general).

Ubicacion: Grupo de subredes privadas.

Acceso Publico: Deshabilitado. El acceso esta restringido exclusivamente a la instancia EC2.

Credenciales: Cuenta de administrador (ecotrack_admin) y rol de aplicacion con permisos limitados (ecotrack_app).

2.4 Almacenamiento (Amazon S3)

Bucket: ecotrack-reportes-s3 (Nombre unico global).

Prefijos (Carpetas): /reportes/ y /resultados/.

Seguridad: Bloqueo total de acceso publico habilitado (Bucket estrictamente privado).

Versionado: Habilitado (Requerido para la inmutabilidad de reportes).

SEGURIDAD Y CONTROL DE ACCESO

3.1 Grupos de Seguridad (Security Groups)

ecotrack-web-sg (EC2):

Entrada: HTTP (80) y HTTPS (443) desde 0.0.0.0/0.

Entrada: SSH (22) exclusivamente desde la IP del administrador.

Salida: Todo el trafico.

ecotrack-rds-sg (Base de Datos):

Entrada: PostgreSQL (5432) exclusivamente desde el grupo de seguridad ecotrack-web-sg. No se permiten conexiones externas directas.

3.2 Gestion de Identidades (IAM)

No se utiliza la cuenta Root para tareas de desarrollo.

Se creo un usuario IAM con autenticacion multifactor (MFA) habilitada.

Los permisos estan limitados a traves del grupo Developers-EcoTrack (acceso a EC2, S3, RDS y CloudWatch).

RESTRICCIONES Y CONTROL DE COSTOS (FREE TIER)

Para garantizar que el proyecto no genere cobros, se han establecido las siguientes restricciones:

AWS Budgets: Alerta configurada con un umbral de 0.01 USD.

Sin NAT Gateway: Se omitio su creacion debido a su costo por hora. La EC2 utiliza el Internet Gateway para salir a internet, y RDS no requiere salida a internet.

RDS Single-AZ: No se habilita Multi-AZ ni replicas de lectura para evitar duplicar costos.

Sin Secrets Manager: Las credenciales se gestionan internamente mediante variables de entorno (.env).

Limite de instancias: Solo se permite una instancia EC2 y una instancia RDS activas simultaneamente.

CONFIGURACION DE VARIABLES DE ENTORNO

La API (.NET) lee su configuracion con la libreria DotNetEnv desde un archivo .env en la raiz del proyecto. El archivo .env real nunca se sube al repositorio (esta incluido en .gitignore).

Para configurar un entorno se copia .env.example como .env y se completan sus valores:

ASPNETCORE_ENVIRONMENT: Development en local y Production en la EC2.
ConnectionStrings\_\_DefaultConnection: cadena de conexion a PostgreSQL con el usuario ecotrack_app. En AWS apunta al endpoint de RDS y agrega SSL Mode=Require.
CORS_ORIGINS: direccion del frontend que puede llamar a la API.
JWT_SECRET, JWT_ISSUER y JWT_AUDIENCE: firma de los tokens de sesion. JWT_SECRET debe tener al menos 32 caracteres.
ALMACENAMIENTO_LOCAL_RUTA: carpeta que simula el bucket de S3, con reportes/ y resultados/.
AUDITORIA_TOLERANCIA_CO2_KG y AUDITORIA_UMBRAL_DISTANCIA_KM: parametros opcionales de la auditoria de reportes.

4. BASE DE DATOS: EJECUCION DEL SCRIPT (LOCAL)

La base de datos se crea con un unico script: ecotrack_unificado.sql (EcoTrack v6.6, PostgreSQL 14 o superior). El script crea el schema "ecotrack" con sus tablas, tipos, triggers y vista, carga datos de prueba y crea el rol ecotrack_app con permisos limitados.

El script esta dividido en tres partes que se ejecutan en este orden dentro del mismo archivo:

Parte 1: Esquema (tablas, indices, triggers, funciones y vista).
Parte 2: Datos semilla (usuarios, choferes, factores de emision, vehiculos, viajes y cargas de combustible de prueba).
Parte 3: Rol de aplicacion y permisos (ecotrack_app).

IMPORTANTE: el script debe ejecutarse sobre una base de datos NUEVA y VACIA. No es repetible: si se ejecuta dos veces sobre la misma base, fallara porque los tipos y tablas ya existen.

4.1 Requisitos

- PostgreSQL 14 o superior instalado.
- pgAdmin 4 o el cliente psql.
- Un usuario con permisos para crear esquemas, extensiones y roles (por ejemplo, postgres).

  4.2 Ejecucion con pgAdmin 4

1. Crear una base de datos nueva llamada ecotrack_db (clic derecho en Databases > Create > Database).
2. Abrir Query Tool sobre ecotrack_db.
3. Abrir el archivo ecotrack_unificado.sql y ejecutarlo completo (F5).

4.3 Ejecucion con psql

createdb -U postgres ecotrack_db
psql -U postgres -d ecotrack_db -v ON_ERROR_STOP=1 -f ecotrack_unificado.sql

La opcion -v ON_ERROR_STOP=1 detiene la ejecucion en el primer error.

4.4 Verificacion

Las tablas estan en el schema ecotrack, por lo que se consultan con ese prefijo:

\dt ecotrack._ (debe listar 14 tablas)
SELECT count(_) FROM ecotrack.usuario; (3)
SELECT count(_) FROM ecotrack.factor_emision; (18)
SELECT count(_) FROM ecotrack.vehiculo; (10)
SELECT count(\*) FROM ecotrack.viaje; (10)

4.5 Datos de prueba

Los usuarios semilla (admin@ecotrack.sv, operador@ecotrack.sv y auditor@ecotrack.sv) usan una contrasena de prueba y existen solo para desarrollo. Deben eliminarse o cambiarse antes de usar datos reales.

4.6 Reinicio de la base de datos (solo desarrollo)

Para volver a empezar desde cero:

DROP SCHEMA IF EXISTS ecotrack CASCADE;
DROP ROLE IF EXISTS ecotrack_app;

Luego se vuelve a ejecutar el script completo. Esto elimina todos los datos.
