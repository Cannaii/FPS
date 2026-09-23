# Imported asset provenance

These assets were copied from `Assets/ThirdParty/Cowsins` on 2026-09-23 so the
AFPS game content owns independent Unity assets and does not reference the
third-party package at runtime.

- `Models/Weapons/Rifle_FPSEngineRig.fbx` came from the matching path under the
  third-party package.
- `Materials/Arms/*.mat` and `Materials/Weapons/**/*.mat` came from the matching
  paths under the third-party package.

The copies intentionally use new GUIDs. AFPS prefabs and animation controllers
must reference these copies, never the originals.
