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

Software Instalado: Node.js, npm, Nginx (como proxy inverso).

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

La aplicacion utiliza la libreria dotenv para leer las credenciales. El archivo .env real nunca se sube al repositorio (esta incluido en .gitignore).

Para configurar un entorno, se debe crear un archivo .env basado en el archivo .env.example provisto en la raiz del proyecto, con la siguiente estructura:

Configuracion del Servidor Backend
PORT=3000
NODE_ENV=production

Configuracion de Amazon RDS (PostgreSQL)
DB_HOST=ecotrack-db.xxxxxxxxxxxx.us-east-2.rds.amazonaws.com
DB_PORT=5432
DB_NAME=ecotrack_db
DB_USER=ecotrack_app
DB_PASSWORD=********
DB_SSL=true
DB_SSL_CA=/opt/ecotrack/certs/global-bundle.pem
DB_POOL_MAX=5

Configuracion de AWS (Para el SDK de S3)
AWS_REGION=us-east-2
S3_BUCKET_NAME=ecotrack-reportes-s3
