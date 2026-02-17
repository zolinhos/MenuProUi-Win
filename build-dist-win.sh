#!/usr/bin/env bash
set -euo pipefail

VERSION="${1:-1.7.5}"
RUNTIME="${RUNTIME:-win-x64}"
BUILD_MSI="${BUILD_MSI:-1}"

ROOT_DIR="$(cd "$(dirname "$0")" && pwd)"
DIST_DIR="$ROOT_DIR/dist"
PUBLISH_DIR="$ROOT_DIR/publish/$RUNTIME"
ZIP_OUT="$DIST_DIR/MenuProUI-${VERSION}-${RUNTIME}.zip"

mkdir -p "$DIST_DIR" "$PUBLISH_DIR"

echo "==> Publicando aplicação Windows ($RUNTIME)..."
dotnet publish "$ROOT_DIR/MenuProUI.csproj" \
  -c Release \
  -r "$RUNTIME" \
  --self-contained true \
  -o "$PUBLISH_DIR"

echo "==> Gerando ZIP de distribuição..."
rm -f "$ZIP_OUT"
(
  cd "$ROOT_DIR"
  zip -qry "$ZIP_OUT" "publish/$RUNTIME"
)

if [[ "$BUILD_MSI" == "1" ]]; then
  echo "==> Tentando gerar MSI..."
  if command -v pwsh >/dev/null 2>&1; then
    pwsh -ExecutionPolicy Bypass -File "$ROOT_DIR/Installer/build-msi.ps1" -Version "$VERSION" || {
      echo "Aviso: falha ao gerar MSI automaticamente neste ambiente."
      echo "Execute no Windows com WiX instalado para gerar o instalador."
    }
  else
    echo "Aviso: pwsh não encontrado. MSI requer execução no Windows com WiX."
  fi
fi

echo "==> Gerando checksums (SHA256/SHA512) em dist..."
(
  cd "$DIST_DIR"
  files=( *.zip *.msi )
  valid=()
  for f in "${files[@]}"; do
    [[ -f "$f" ]] && valid+=("$f")
  done

  if [[ ${#valid[@]} -gt 0 ]]; then
    shasum -a 256 "${valid[@]}" > SHA256SUMS
    shasum -a 512 "${valid[@]}" > SHA512SUMS
  else
    : > SHA256SUMS
    : > SHA512SUMS
  fi
)

echo
echo "Artefatos gerados em: $DIST_DIR"
ls -lah "$DIST_DIR"
