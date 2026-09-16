# AGENTS.md

Unity 6.3 project (`6000.3.24f1`), Universal Render Pipeline (URP). Early stage: scene/asset scaffolding only, no C# scripts or tests yet.

## Layout

- All content lives under `Assets/_Project/`, split into domains: `00_Common` (shared), `01_Menu`, `02_Gameplay`.
- Each domain has subfolders: `00_Scenes`, `_Scripts`, `_Audio`, `_Material`, `_Texture`. Do not create content at `Assets/` root.
- Empty scaffold folders are deliberately tracked via their `.meta` files — keep them in commits.
- Only scene in the project: `Assets/_Project/00_Common/00_Scenes/Test.unity`.

## Unity gotchas

- Every asset has a sidecar `.meta` (contains the GUID). Always commit it together with the asset; never edit GUIDs by hand.
- `Library/`, `Logs/`, `UserSettings/` are generated and gitignored (safe to delete `Library/`; Unity regenerates it).
- New Input System only (`activeInputHandler: 1`): the legacy `UnityEngine.Input` API is disabled. Use `UnityEngine.InputSystem`. Shared action maps live in `00_Common/_Unity/InputSystem_Actions.inputactions` (Player map already has Move/Look/Attack/Interact/Jump/Sprint etc.).
- URP pipeline assets (PC + Mobile), renderers, and volume profiles live in `00_Common/_Unity/Settings/` — configure rendering there, not in Quality settings.
- Build scene list (EditorBuildSettings) still references the deleted template scene `Assets/Scenes/SampleScene.unity`; add real scenes via Build Profiles and remove the stale entry.

## Verification

- No CLI lint/test pipeline. Scripts compile and tests run inside the Unity Editor (Test Runner: Window > General > Test Runner); `com.unity.test-framework` is installed but no tests exist yet.
- After creating/editing scripts or scenes, let the Editor refresh and check the Console for compile errors — there is no other automated check.
