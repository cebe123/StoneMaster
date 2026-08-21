# CorelDRAW SDK Compatibility

## Verified

CorelDRAW SDK documentation currently exposes:

- `FrameWork.AddDocker(Guid, ClassName, AssemblyPath)` for custom .NET dockers.
- 64-bit add-ons under `Programs64\Addons`.
- WPF hosted Docker examples using `type="wpfhost"` and `hostedType`.
- `Document.ExportBitmap(...)` for exporting a selection/current page to an image file.
- `Layer.CreateEllipse2(...)` as center + radius API.
- `cdrExportRange.cdrSelection = 2`.
- `cdrFilter.cdrJPEG = 774`, `cdrFilter.cdrPNG = 802`.
- `cdrImageType.cdrRGBColorImage = 4`.

## Version strategy

The Corel API is exposed through COM automation and is version-sensitive. The project therefore keeps Corel automation isolated in:

```text
CorelDrawService.cs
CoordinateMapper.cs
LayerService.cs
StoneRenderer.cs
```

The production build references `Corel.Interop.VGCore` from the licensed CorelDRAW installation. The reference uses `Copy Local=false` and `Embed Interop Types=true`: COM contracts are embedded for the Docker, but Corel's proprietary DLL is neither committed nor placed in the installer. `scripts\build.ps1` defaults to the supplied DLL path and also accepts `-CorelInteropPath <path>`.

## 2024 / 2025 / 2026

The custom Docker API is documented for CorelDRAW v25, v26 and v27. The installer should detect the installed version and install into the matching `Programs64\Addons` directory.

Use one build artifact per target CorelDRAW major version if strong-typed Interop references are enabled. The supplied DLL is `26.2.0.170`; validate the Docker against the installed CorelDRAW major version before distribution.
