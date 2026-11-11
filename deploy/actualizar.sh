#!/usr/bin/env bash
# Uso en la EC2: bash actualizar.sh /home/ec2-user/subida
# La carpeta de subida debe traer api/ (dotnet publish) y web/ (npm run build).
set -euo pipefail

SUBIDA="${1:?Indica la carpeta con api/ y web/}"
API=/opt/ecotrack/api
WEB=/var/www/ecotrack

test -f "$SUBIDA/api/EcoTrack.dll" || { echo "Falta $SUBIDA/api/EcoTrack.dll"; exit 1; }
test -f "$SUBIDA/web/index.html" || { echo "Falta $SUBIDA/web/index.html"; exit 1; }
test -f "$API/.env" || { echo "Falta $API/.env con las variables de produccion"; exit 1; }

sudo rsync -a --delete --exclude .env --exclude bucket-local "$SUBIDA/api/" "$API/"
sudo chown -R ecotrack:ecotrack "$API"
sudo chmod 600 "$API/.env"

sudo rsync -a --delete "$SUBIDA/web/" "$WEB/"
sudo chmod -R a+rX "$WEB"

sudo systemctl restart ecotrack-api
sleep 3
curl -fsS http://127.0.0.1/api/v1/health && echo
echo "Despliegue listo"
