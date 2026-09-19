#!/bin/bash
# Script de publication automatisé pour WinSpaces

if [ -z "$1" ]; then
  echo "Erreur : Version non spécifiée. Usage : ./scripts/publish.sh <version> (ex: v2.5.2)"
  exit 1
fi

VERSION=$1
BINARY="src/WinSpaces/bin/Release/net8.0-windows/win-x64/publish/WinSpaces.exe"

# 1. Vérification du binaire
if [ ! -f "$BINARY" ]; then
    echo "Erreur : Binaire introuvable à $BINARY."
    echo "Merci de builder le projet en mode Release avant de publier."
    exit 1
fi

# 2. Tag et Push
echo "--- Tagging et Push de la version $VERSION ---"
git tag -a "$VERSION" -m "Release $VERSION"
git push origin "$VERSION"

# 3. Création de la release GitHub
echo "--- Création de la release GitHub $VERSION ---"
gh release create "$VERSION" --title "Release $VERSION" --notes "Release automatique $VERSION"

# 4. Upload du binaire
echo "--- Upload de l'exécutable ---"
gh release upload "$VERSION" "$BINARY" --clobber

echo "--- Publication terminée avec succès ! ---"
