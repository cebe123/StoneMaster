# Development

## Layering

- UI: WPF Docker
- Application services: PythonEngineService, SettingsService
- Corel adapter: CorelDrawService
- Rendering: StoneRenderer
- Image engine: Python/OpenCV
- IPC: request/response JSON

## Production hardening next

1. Replace dynamic COM calls with `Corel.Interop.VGCore` per supported SDK target.
2. Add a version adapter for v25/v26/v27 if a typed interop build is required.
3. Add an actual raster preview inside the Docker.
4. Move the selected bitmap export bounding box from current selection to a dedicated `SelectionAdapter`.
5. Add large-document batching/grouped Corel objects.
6. Add cancellation checkpoints in the Python loop.
7. Add structured logging to `%LOCALAPPDATA%\StoneMaster\logs`.
8. Add a proper palette editor and presets.
9. Add max-color quantization and explicit per-color mapping UI.
10. Add installer detection for non-default Corel install locations.
