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

## Ink-focused follow-up

The obsolete `lastTouchDownStrokeCollection` was deeply cloned on three touch paths and before smoothing/shape replacements. Its only consumer compared counts inside an empty branch. Removing the field, assignments and method eliminates unused whole-page copies; TimeMachine, saved pages and save snapshots are separate and retained. With 300 strokes/60,000 points, a removed clone measured 5.43 ms and 2,725,572 managed bytes locally. This measures the removed operation, not end-to-end input latency.

Pressure normalization now leaves an already-uniform point collection untouched. When pressure changes are necessary, the cloned collection retains its original point description and tilt data, and publishes one replacement. Edge-hint detection consumes the source point collection after its eligibility checks rather than allocating a full coordinate list for every completed stroke.

An invalidation guard now rejects pending asynchronous smoothing results after the source geometry or style changes. Previously a moved/recolored source could still be replaced by its older worker snapshot merely because it remained on the canvas. The guard is detached on completion/failure and does not clone the source.

Async smoothing now updates the applied undo entry containing its source stroke, preserving other members of grouped input. Previously it replaced the newest user-input entry unconditionally, which could belong to a later stroke during rapid writing. Undone entries are excluded; if no matching desktop entry exists, the established shape-replacement history path is used. Word retains its independent history ownership. Six production-history checks cover late completion order, undone entries and grouped input.

After the user reported continuous-writing lag following the contrast-icon fix, the contrast commit and callers were inspected. Board icon updates occur on registration/color/mode changes, rather than on individual point packets. The local configuration at inspection had WinRT disabled, asynchronous advanced smoothing enabled and realtime debug logging disabled. This observation does not establish the cause of the reported regression. Repeated equivalent fill/outline updates now preserve existing brushes/pens and do not invalidate the icon drawing; actual foreground/theme changes still refresh it. Frozen solid fills are used for the board pen icon.

The pen suite now includes pressure normalization, tilt/ISF preservation, invalidation on movement/recolor/replacement and guard cleanup (220 checks total). The production WPF preview fixture adds 480 varying-pressure points across 120 redraws, verifies bounded retained visuals, early/final segment pixels, pressure-dependent thickness and shortened-stroke redraw cleanup (5 checks). The contrast fixture also verifies that 1,000 equivalent updates generate no drawing invalidations, alongside white/transparent pen visibility and theme updates.

Final follow-up validation: full Debug/x64 build, zero warnings/errors; 220 pen checks, 11 production preview/history checks, 7 icon checks, 20 performance behavior checks, 4 booth history checks and 32 settings/native-window checks passed. The last production preview fixture took 22.2 ms for 120 redraws (480 points), retaining 10 visual chunks. This is a WPF fixture measurement, not physical input-to-display latency.

Further device profiling should distinguish active writing from pen-up work: WPF touch packets are coalesced and render in bounded chunks; gesture history still snapshots changed strokes, shape recognition runs when enabled, and detailed realtime debug logging still performs history I/O when explicitly enabled. These are identified code paths, not measured causes of this user's continuous-writing stall. Physical writing latency, WinRT wet/dry handoff and actual undo/gesture behavior require device acceptance testing.

## Rapid-writing scheduling and allocation follow-up

The audit covered WPF touch/stylus sampling, live preview and frame coalescing, pressure processing, asynchronous smoothing and its completion, editable/presentation collections, history notifications, recognition snapshots, saving and WinRT wet/dry handoff. This patch targets verified scheduling and allocation problems; it does not change input filtering, sample spacing, curve fitting, pressure interpolation, recognition preferences or WinRT's composition fence.

The production smoother previously used `Dispatcher.InvokeAsync`'s Normal priority for its completion callback, ahead of pending Input and Render work. A baseline fixture against the previously built host reproduced `smoothing, input` in that order. Completions now use Background priority with cancellation; manager statistics no longer require an additional UI continuation on that async path. The fixture holds the dispatcher until a real worker completion is queued, posts an Input operation and verifies ordering without a wall-clock latency assertion. It also tests cancellation of queued completion and a 32-stroke/100-input burst.

Smoothing now uses one `StrokeCollection.Replace` notification, preserving the original stroke's paint position. Both merged-history and separate shape-history modes remain supported, with commit reason restored in a finally block. The history handler accepts both sides of an atomic replacement. Mixed ordinary/highlighter presentation updates replace the affected ordinary stroke directly, preserving existing highlighter groups. Pixel tests cover a late processed stroke underneath a later stroke and the replacement's visibility after the upper stroke is removed.

Resampling formerly built oversized intermediate arrays before rejecting them and retrying with a larger interval. A rejected attempt now stops as soon as it exceeds the existing limit. The interval sequence and fallback are unchanged. Sixteen deterministic dense/sparse input cases serialize every output X/Y/pressure value into a SHA-256 fingerprint for baseline/current comparison, alongside 48 bounds/endpoint/pressure checks. The baseline fingerprint is `0E5C6B915E9D231BF84C8487CE00E97FCD87F1B76A5D57661CA6CCB2E6C72CA8`. The old sparse-input fixture allocated 198,983,128 managed bytes over 20 runs and took 159.2 ms; these are microbenchmark results, not physical writing latency.

The current fingerprint matches the baseline exactly. The same sparse fixture now allocates 2,171,240 bytes and takes 3.5 ms over 20 runs. The new scheduling fixture observes `input, smoothing` with Background priority; all 32 completions and 100 Input-priority operations drain (72.9 ms locally). These queued operations test dispatcher ordering, not physical tablet delivery.

Final validation: Debug/x64 solution and fixture builds passed with zero warnings/errors; 223 pen checks, 6 scheduling/burst checks, 48 resampling checks, 11 production preview/history checks, 20 performance behavior checks, 4 booth history checks, 7 icon checks, 32 settings/native-window checks, and 295 Word logic checks passed. The deliberately failing old-host scheduling run is retained as baseline evidence, not counted as a current failure. Physical rapid-writing latency and sustained low-end-device performance remain acceptance items.

## Drawing-system optimization follow-up

This changes the ten reviewed drawing areas, plus scene-object eraser dispatch. It retains the existing input filters, pressure conversion, shape geometry, palm classification thresholds and WinRT wet/dry handoff. The live tip uses the latest filtered actual sample, not prediction; pen-up commits that endpoint once. Frozen pen reuse is bounded, and variable-pressure chunks release their temporary lookup entries.

- Ordinary-board roaming previews a combined presentation matrix and applies it to source points/images once on completion. Camera/plugin backgrounds keep their immediate coordinate path. Cancellation, tool changes and mouse capture loss flush pending roaming state. Drag history tracks the live endpoint during movement and owns deep copies at commit; history replay clones the applied endpoint to prevent later edits from corrupting undo/redo.
- Shape movement is coalesced at Render priority. Single-stage shapes use a private noninteractive preview canvas and enter formal ink once at completion. Cuboid/hyperbola multi-stage ownership remains on its existing path, with coalesced movement. Aborting input clears the private preview.
- Highlighter presentation retains its render strokes and drawing groups, replacing only changed geometry ranges. Export draws directly without allocating retained presentation nodes. Alpha grouping, overlap darkening, ordinary-stroke order and editing source collections retain their existing semantics.
- The roaming minimap skips closed-popup work and reuses its raster until content/bounds change; viewport movement alone updates the viewport indicator. Segmented fade uses one drawing host per stroke and animation BeginTime rather than one UIElement/timer per segment. Completion/cancellation clears animation clocks; shared expiry still bounds lifetime.
- Camera paper detection waits for a 250 ms input quiet period; glass refresh waits for 300 ms. Failed native glass capture backs off before trying hide/show fallback again (2/4/8 seconds). These avoid costly periodic readback/capture during writing.
- Saves may reuse immutable snapshots for unchanged inactive board-page history versions through weak keys/values. Active pages are invalidated and captured afresh; PPT and plugin companion state retain their existing capture paths. Scene-object hit testing caches weakly held typed delegates rather than allocating reflection argument arrays for each hit, with a compatibility fallback for other method signatures.

Final source build: Debug/x64 solution and WPF fixture, zero warnings/errors. Passed: 226 pen/fade, 37 drawing-system, 8 fade-geometry, 11 production preview/history, 6 scheduling/burst, 48 resampling, 20 performance, 8 PPT navigation, 18 Word navigation/settings, 4 booth history, 7 pen-icon and 32 settings/native-window checks. The wider regression run also passed 295 Word logic, 217 reliability and 55 canvas-margin checks. No UI strings or icon assets were added.

The drawing fixture performs 500 private-preview replacements without formal document notifications, commits once, exercises seven production shape generators, renders matrix-aligned ink/images, verifies interrupted roaming is applied exactly once, checks tip finalization and immutable history replay, and tests inactive snapshot versioning. A warmed 10,000 scene point-hit loop allocates less than 1 KB. It injects only the fields needed by pure geometry/history/roaming methods into an uninitialized MainWindow; it does not represent startup or a complete app-window test.

All eight old/new segmented-geometry render cases have identical pixel fingerprint `A799978B42313F5B37CF22FA6057B5F261B9E4D80BAD46D024BDE8D8F22AF553`. Resampling retains the earlier exact coordinate/pressure fingerprint. The latest production WPF preview fixture took 22.7 ms for 120 redraws/480 points, allocated 766,336 bytes and retained 10 visual chunks. These are local fixtures, not physical input-to-display latency claims.

Physical touch rapid-writing, multi-stage shape gestures, actual camera and magnifier drivers, low-end-device sustained load, Word/WPS integration and complete visible-window acceptance remain unverified. The existing VSTO build limitation is unchanged. These gaps must not be described in the PR as completed full device testing under CONTRIBUTING.md.

## Word stall report and input rollback

The user reported a clear Word-link regression immediately after installing input commit 5e28f94f. That entire input commit was reverted in 077c0143; its 21 fixture checks were not sufficient to establish device or Word compatibility. The original-directory executable/DLL/deps/runtimeconfig/PDB were restored from the pre-input backup after matching their hashes against the previously validated 547657b2 drawing build. The distribution build was stopped and has not been delivered. The precise physical-input cause of this regression is not established.

The local application log separately records repeated Ready queries taking about 0.5–1.8 seconds, a 14,188 ms document phase followed by timeout/RPC failure, and a 2,337 ms UI stall. These support investigating slow-query amplification; they do not prove that those older controller paths caused the new input regression.

Three independently scoped Word changes address verified code paths: a hard recovery deadline after queries costing at least 250 ms survives scroll-settle/fast-retry overrides; delayed settle verification reads cached anchors instead of unconditionally rediscovering page geometry; helper pipe/timeout continuations do not capture the WPF synchronization context. Lost-geometry recovery still requests recalibration, normal inexpensive discovery still probes promptly, and later document/revision checks retain their existing path. Transport generation cleanup handles a concurrent Stop/Dispose before releasing the exchange semaphore.

The Word fixture passes 300 checks, including a real fake-helper process completing and timing out while its caller's synchronization context does not execute any posted callbacks, slow-query deadlines surviving retry/input overrides, prompt recovery on cheap cached frames, and cached post-settle verification. Existing assertions that required an immediate query after expensive discovery or unconditional full settle discovery were updated to the deliberate new behavior, retaining cheap-query, recovery and geometry checks. Physical Word acceptance remains pending; this candidate is kept separate from the restored local baseline until the rollback result is confirmed.

The user subsequently confirmed that rollback alone did not resolve the stalls and reported ICCCE hanging/exiting when Word closed. No matching new managed crash report or Windows Application Error stack was available, so the exact exit cause is not claimed. The independent 40 ms native-window boundary watcher now detects the closed Word HWND before input-contact guards, stops the outstanding helper exchange, detaches the session and restores desktop ink without awaiting the Office reply. Generation checks reject a late frame. Five additional controller checks hold a query open, ignore an unrelated window close, detach the matching window, then deliver the stale response and repeat the close notification; desktop ink identity is retained.

Final Word logic suite: 305 passing checks. A real, isolated Microsoft Word process also passed 79 stability/tracking/close checks on a generated ten-page document: page targets 2/6/9/3/6 were confirmed (93/92/108/87/154 ms in that fixture), the owned test document and application were closed while the observer retained COM references, the closed HWND was rejected, and connection references were released without terminating the test host. These results do not reproduce the user's document or establish end-to-end ICCCE input latency or resolution of the reported crash. WPS is not registered on this machine and was not live-tested. The original directory will receive only the reverted-input baseline plus these independently tested Word fixes; distribution packaging remains suspended for user verification.

## Persistent Word alignment follow-up

The user confirmed that closing Word no longer crashed ICCCE, but supplied a screenshot of severe page-boundary ink misalignment that did not recover after scrolling stopped. Cached-anchor-only delayed verification was too weak and has been withdrawn: the settled view again requests one full recalibration. Slow-query deadlines, transport isolation and closed-window detachment remain in place. Recalibration receives the existing full-query timeout rather than the shorter scroll-preview budget.

The observer's recalibration loop also reused cached page-relative X/Y and page numbers while only refreshing screen X/Y. Word reflow can invalidate those page-relative values even when the character range still exists. Recalibration now reads fresh page identity and page-relative coordinates as well as the screen point, including cached ranges on other visible pages. It limits successful candidates to two per previously known page. Ordinary live scroll keeps its existing lightweight path.

A new isolated real-Word regression seeds the anchor cache, changes paragraph spacing, repaginates, and compares recalibrated page origins with a completely new observer. With the pre-fix observer DLL, the test fails at the confirmed-view assertion; with the fixed observer, all 82 Microsoft Word stability/tracking/reflow/close checks pass and common page origins agree within three physical pixels. The 305 Word logic checks also pass, retaining delayed full recalibration and closed-window/late-reply tests. The old-host failure is intentional baseline evidence, not a current passing check. This is a generated document fixture, not a reproduction of the user's document; user verification remains necessary. No annotation files are deleted or rewritten as a repair operation.

Final input gating also treats pending recalibration as blocked: the WPF canvas cannot collect new ink against a page origin still awaiting confirmation. A held-reply fixture verifies the blocked state during recalibration, raising the logic total to 306 passing checks. The exact Debug helper DLL/executable intended for installation also passed the 82 live Word checks. Installation pairs the new main executable with all four newly built helper files under the original `word-link` directory and verifies their hashes; it does not reuse the old helper copied into a nested validation output by the project's default-path copy target. The user has exited ICCCE for this paired update.
