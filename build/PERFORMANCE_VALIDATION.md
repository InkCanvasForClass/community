# Performance changes and validation (2026-11-01)

## Changed paths

- Settings search reads localized headers and setting keys from embedded XAML on a worker. Indexing does not construct pages, apply templates, enumerate devices or attach page event handlers. Search navigation retains explicit keys and header fallback for unvisited pages.
- Board/video-booth toolbars reuse built-in buttons per host and layout position. Configuration, culture, theme and foreground changes invalidate the relevant views. Plugin controls and page-info controls are rebuilt. Configuration deserialization is skipped while the selected/default files are unchanged.
- Highlighter presentations cache source geometry and render groups per canvas. Editing invalidates the affected group; page replacement clears the cache. Ordinary pen append/removal preserves the existing highlighter presentation without rebuilding it. Persistent/editable stroke identity is unchanged.
- Asynchronous smoothing takes one point/style snapshot instead of cloning a stroke and copying the points again. Automatic concurrency is capped at four, leaving a core available where possible; explicit user preferences remain effective. Pressure interpolation, curve fitting and resampling parameters are unchanged.
- The synchronous legacy smoothing entry point computes directly instead of waiting for an asynchronous continuation on its own UI dispatcher. Unused full-screen render-target allocation is removed. Diagnostic aggregation runs at most once per second; reset still publishes immediately.
- Word projections no longer allocate unused desktop point-history snapshots. Desktop snapshots are restored once when canvas ownership returns. Word page detection and projection algorithms are unchanged in this patch.
- Camera paper detection can consume a frozen, downscaled capture-thread frame. Full-quality preview/photo paths are unchanged. Drivers without sample events retain the existing UI readback fallback, with a sampling interval that backs off when readback is expensive.
- Desktop startup defers board-only initialization until background dispatcher work; entering the board initializes it immediately if necessary. Media hit testing polls less often while the cursor is stationary and returns to its fast interval on movement.

## Reproduction

Build the solution with Debug/x64 into a separate output directory, then build `SettingsPersistenceTests` into that directory with `BuildProjectReferences=false`. Run its `--performance-only`, `--presentation-only`, `--word-navigation-only`, `--booth-history-only`, `--board-pen-icon-only` and default modes.

Run `PenToolTests`, `ReliabilityTests` and `WordLinkTests` in Release. `WordLinkTests --projection-perf-only` isolates the dense/live projection fixture. Do not compare timings while another compilation is running.

## Measurements

These are local microbenchmarks, not application-wide speedup claims:

| Fixture | Reuse/cache | Forced reconstruction |
| --- | --- | --- |
| 12 toolbar buttons, 50 rebuilds | 202.7 ms / 6,720,152 bytes | 1010.7 ms / 26,197,696 bytes |
| 500 highlighter strokes, 30 presentations | 20.9 ms / 4,357,720 bytes | 28.4 ms / 5,843,432 bytes |

The toolbar comparison deliberately invalidates configuration each iteration in the reconstruction case. The highlighter comparison uses the same warm source strokes in both cases. Allocation figures are managed allocations on the calling thread, not process working set.

Word's plain-InkCanvas dense fixture varied substantially between runs. Alternating baseline/current runs produced overlapping ranges; this does not establish a Word scrolling speedup. The final full Word run measured 120 live-transform updates at 9.6 ms with zero stroke invalidations and 20 dense-projection updates at 465.2 ms.

## Validation limits

Debug/x64 solution build passed with zero warnings and errors. Completed checks: 20 performance behavior, 206 pen style/fade, 217 reliability, 295 Word link, 8 production presentation navigation, 18 Word navigation/settings, 4 booth history, 5 pen icon rendering, and 32 settings persistence/native-window checks.

The Windows WPF fixtures verify rendered highlighter pixels, alpha/overlap, selection/erasing, ordinary-ink updates, pressure preservation, snapshot isolation, toolbar identity/handlers/visibility/config changes, localized metadata and detached camera pixel data. They are not physical touch-screen, camera-driver, Word/WPS or low-end-device acceptance tests. Those scenarios still require device verification. The first reliability run under compilation load missed a timer deadline; an isolated run passed all 217 checks without a production change.

Full VSTO build remains unavailable on this machine because the Visual Studio OfficeTools targets are not installed. This is not reported as a passing check.
