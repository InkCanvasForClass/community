using Ink_Canvas.Helpers;
using Ink_Canvas.Plugins;
using System;
using System.Collections.Generic;

namespace InkCanvas.PluginSdk.Tests
{
    internal static class Program
    {
        private static int _passed;

        [STAThread]
        private static void Main()
        {
            Run(nameof(PluginStateHistoryRestoresBeforeAndAfterSnapshots), PluginStateHistoryRestoresBeforeAndAfterSnapshots);
            Run(nameof(PluginStateHistoryDropsRedoBranchAfterNewCommit), PluginStateHistoryDropsRedoBranchAfterNewCommit);
            Run(nameof(PluginStateHistoryRejectsInvalidSnapshots), PluginStateHistoryRejectsInvalidSnapshots);
            Run(nameof(PluginInkConversionHistoryIsAtomic), PluginInkConversionHistoryIsAtomic);
            Run(nameof(CanvasPointerContractHasStableDefaults), CanvasPointerContractHasStableDefaults);
            Run(nameof(CanvasLineConversionContractHasStableDefaults), CanvasLineConversionContractHasStableDefaults);
            Run(nameof(CanvasViewportContractHasStableDefaults), CanvasViewportContractHasStableDefaults);
            Run(nameof(OfficialSdkSyncIncludesCompositionAndPresentationContracts), OfficialSdkSyncIncludesCompositionAndPresentationContracts);
            Run(nameof(CanvasToolSessionStopsRoutingAfterDispose), CanvasToolSessionStopsRoutingAfterDispose);
            Run(nameof(PluginCleanupContinuesAfterIndividualFailure), PluginCleanupContinuesAfterIndividualFailure);
            Run(nameof(PluginSettingsDiscoveryDeduplicatesAndIsolatesFailures), PluginSettingsDiscoveryDeduplicatesAndIsolatesFailures);
            Run(nameof(WhiteboardPageIdsRemainStableAcrossInsertAndRemove), WhiteboardPageIdsRemainStableAcrossInsertAndRemove);
            Run(nameof(WhiteboardStateFollowsStablePageIds), WhiteboardStateFollowsStablePageIds);
            Run(nameof(WhiteboardProviderFailuresAreIsolated), WhiteboardProviderFailuresAreIsolated);
            Run(nameof(WhiteboardDocumentSnapshotRoundTripsStableIdsAndUnknownPlugins), WhiteboardDocumentSnapshotRoundTripsStableIdsAndUnknownPlugins);
            Run(nameof(WhiteboardDocumentRejectsCorruptionWithoutReplacingState), WhiteboardDocumentRejectsCorruptionWithoutReplacingState);
            Run(nameof(WhiteboardSinglePageImportRemapsSavedPageToCurrentIndex), WhiteboardSinglePageImportRemapsSavedPageToCurrentIndex);
            Run(nameof(WhiteboardLegacyImporterFillsOnlyMissingPluginState), WhiteboardLegacyImporterFillsOnlyMissingPluginState);
            Run(nameof(WhiteboardInitialHistoryPreservesLoadedStateAndEmptyBaseline), WhiteboardInitialHistoryPreservesLoadedStateAndEmptyBaseline);
            Run(nameof(WhiteboardInitialHistoryFailuresAreIsolated), WhiteboardInitialHistoryFailuresAreIsolated);
            Run(nameof(WhiteboardCompanionStatesUseCapturedPageState), WhiteboardCompanionStatesUseCapturedPageState);
            Run(nameof(PluginCompatibilityUsesActualHostAndCanvasApiVersions), PluginCompatibilityUsesActualHostAndCanvasApiVersions);
            Console.WriteLine($"Plugin SDK contract tests passed: {_passed}.");
        }

        private static void PluginCompatibilityUsesActualHostAndCanvasApiVersions()
        {
            Assert(HostApiRequirement.CurrentApiVersion == "1.10.0",
                "The canvas viewport contract must advance the compatible API minor version.");
            Assert(Version.TryParse(HostApiRequirement.HostVersion, out var hostVersion) &&
                   hostVersion >= new Version(1, 7, 19, 9),
                "The compatibility host version must be generated from the current version.json baseline.");

            var current = PluginCompatibility.Check(new PluginManifest
            {
                Version = "1.0.0",
                ApiVersion = "1.10.0",
                MinHostVersion = "1.7.19.9"
            });
            Assert(current.IsCompatible, "The current math plugin contract should be accepted.");

            var previousApi = PluginCompatibility.Check(new PluginManifest
            {
                Version = "1.0.0",
                ApiVersion = "1.9.0"
            });
            Assert(previousApi.IsCompatible, "The API minor bump must remain compatible with 1.9 plugins.");

            var legacyApi = PluginCompatibility.Check(new PluginManifest
            {
                Version = "1.0.0",
                ApiVersion = "1.0.0"
            });
            Assert(legacyApi.IsCompatible, "The API minor bump must remain compatible with 1.0 plugins.");

            var futureHost = PluginCompatibility.Check(new PluginManifest
            {
                Version = "1.0.0",
                MinHostVersion = "1.8.0.3"
            });
            Assert(!futureHost.IsCompatible, "A plugin requiring a newer host must be rejected.");

            var futureApi = PluginCompatibility.Check(new PluginManifest
            {
                Version = "1.0.0",
                ApiVersion = "1.11.0"
            });
            Assert(!futureApi.IsCompatible, "A plugin requiring a newer API minor version must be rejected.");
        }

        private static void PluginStateHistoryRestoresBeforeAndAfterSnapshots()
        {
            var history = new TimeMachine();
            history.CommitPluginStateHistory("sample.plugin", "before", "after");

            var undo = history.Undo();
            Assert(undo != null, "Undo should return the plugin history item.");
            Assert(undo.CommitType == TimeMachineHistoryType.PluginStateChange, "Unexpected history type.");
            Assert(undo.StrokeHasBeenCleared, "Undo should select the before snapshot.");
            Assert(undo.PluginStateBefore == "before", "Before snapshot changed.");

            var redo = history.Redo();
            Assert(ReferenceEquals(undo, redo), "Redo should return the same history item.");
            Assert(!redo.StrokeHasBeenCleared, "Redo should select the after snapshot.");
            Assert(redo.PluginStateAfter == "after", "After snapshot changed.");
        }

        private static void PluginStateHistoryDropsRedoBranchAfterNewCommit()
        {
            var history = new TimeMachine();
            history.CommitPluginStateHistory("sample.plugin", "0", "1");
            history.CommitPluginStateHistory("sample.plugin", "1", "2");
            history.Undo();
            history.CommitPluginStateHistory("sample.plugin", "1", "3");

            Assert(!history.CanRedo, "A new commit must remove the stale redo branch.");
            var items = history.ExportTimeMachineHistory();
            Assert(items.Length == 2, "Expected the original commit and replacement branch.");
            Assert(items[1].PluginStateAfter == "3", "Replacement branch was not retained.");
        }

        private static void PluginInkConversionHistoryIsAtomic()
        {
            var points = new System.Windows.Input.StylusPointCollection
            {
                new System.Windows.Input.StylusPoint(10, 20),
                new System.Windows.Input.StylusPoint(30, 40)
            };
            var strokes = new System.Windows.Ink.StrokeCollection
            {
                new System.Windows.Ink.Stroke(points)
            };
            var history = new TimeMachine();

            history.CommitPluginInkConversionHistory("sample.plugin", "before", "after", strokes);

            var item = history.Undo();
            Assert(item.CommitType == TimeMachineHistoryType.PluginInkConversion,
                "Conversion must use one composite history item.");
            Assert(!item.StrokeHasBeenCleared, "Undo must restore the consumed ink.");
            Assert(item.CurrentStroke.Count == 1 && item.PluginStateBefore == "before",
                "Undo lost the ink or plugin before-state.");

            var redo = history.Redo();
            Assert(ReferenceEquals(item, redo) && redo.StrokeHasBeenCleared,
                "Redo must consume the same ink in the same history item.");
            Assert(redo.PluginStateAfter == "after", "Redo lost the plugin after-state.");
        }

        private static void CanvasLineConversionContractHasStableDefaults()
        {
            var candidate = new CanvasLineFinalizedEventArgs();
            Assert(candidate.Source == CanvasLineSource.GeometryLine,
                "The first source value must remain the geometry line for compatibility.");
            Assert(candidate.Start == default && candidate.End == default,
                "Line candidate coordinates must preserve neutral defaults.");
            Assert(typeof(ICanvasLineConversionService).GetEvent("LineFinalized") != null,
                "The finalized line event is missing.");
            Assert(typeof(ICanvasLineConversionService).GetMethod("TryConvertToPluginState") != null,
                "The atomic conversion operation is missing.");
        }

        private static void CanvasViewportContractHasStableDefaults()
        {
            var transform = new CanvasViewportTransformEventArgs();
            Assert(transform.Delta.IsIdentity && !transform.IsCompleted,
                "Viewport transforms must preserve neutral defaults.");
            Assert(typeof(ICanvasViewportService).GetEvent("TransformChanged") != null,
                "The canvas viewport transform event is missing.");
        }

        private static void CanvasPointerContractHasStableDefaults()
        {
            var pointer = new CanvasPointerEventArgs();
            Assert(Math.Abs(pointer.Pressure - 0.5f) < 0.0001f, "Default pressure must remain neutral.");
            Assert((int)CanvasPointerAction.Cancel == 3 && (int)CanvasPointerAction.Wheel == 4,
                "Wheel must extend the pointer action contract without renumbering existing actions.");
            Assert(pointer.WheelDelta == 0 && pointer.Modifiers == System.Windows.Input.ModifierKeys.None,
                "Wheel fields must preserve non-wheel event defaults.");
            Assert(!pointer.Handled, "Pointer events must be unhandled by default.");
            var key = new CanvasKeyEventArgs();
            Assert(key.Key == System.Windows.Input.Key.None &&
                   key.Modifiers == System.Windows.Input.ModifierKeys.None && !key.Handled,
                "Key events must preserve neutral defaults.");
            var toolbar = new PluginToolbarItemInfo();
            Assert(toolbar.Surface == PluginToolbarSurface.Floating &&
                   (int)PluginToolbarSurface.Whiteboard == 1,
                "Existing plugin toolbar items must remain on the floating surface by default.");
        }

        private static void OfficialSdkSyncIncludesCompositionAndPresentationContracts()
        {
            Assert(typeof(ICanvasCompositionService).GetMethod("SetVisiblePagesAsync") != null,
                "The official multi-page composition contract is missing.");
            Assert(typeof(ICanvasCompositionService).GetMethod("ScrollOffsetAsync") != null,
                "The official continuous-scroll composition contract is missing.");
            Assert(typeof(IPresentationSourceService).IsInterface,
                "The official presentation source contract is missing.");
        }

        private static void PluginSettingsDiscoveryDeduplicatesAndIsolatesFailures()
        {
            var errors = 0;
            var plugins = new[]
            {
                new PluginInfo { Id = "with.settings", Name = "Settings", Instance = new SettingsPlugin(new object()) },
                new PluginInfo { Id = "with.settings", Name = "Duplicate", Instance = new SettingsPlugin(new object()) },
                new PluginInfo { Id = "without.settings", Name = "None", Instance = new SettingsPlugin(null) },
                new PluginInfo { Id = "broken.settings", Name = "Broken", Instance = new SettingsPlugin(null, true) }
            };

            var entries = PluginSettingsNavigationRegistry.Discover(plugins, (_, __) => errors++);

            Assert(entries.Count == 1, "Only one unique plugin settings page should be discovered.");
            Assert(entries[0].PageTag == "PluginSettings_with.settings",
                "Plugin settings page tags must remain stable.");
            Assert(errors == 1, "A failing settings view must be isolated and reported once.");
        }

        private static void PluginStateHistoryRejectsInvalidSnapshots()
        {
            var history = new TimeMachine();
            AssertThrows<ArgumentException>(() => history.CommitPluginStateHistory(" ", "before", "after"));
            AssertThrows<ArgumentNullException>(() => history.CommitPluginStateHistory("sample.plugin", null, "after"));
            AssertThrows<ArgumentNullException>(() => history.CommitPluginStateHistory("sample.plugin", "before", null));
        }

        private static void CanvasToolSessionStopsRoutingAfterDispose()
        {
            var captures = 0;
            var releases = 0;
            var pointerEvents = 0;
            var keyEvents = 0;
            var session = new PluginCanvasToolSession(
                "sample.plugin",
                "tool",
                _ => { captures++; return true; },
                _ => releases++,
                value => value.MarkInactive());
            session.Pointer += (_, __) => pointerEvents++;
            session.KeyDown += (_, __) => keyEvents++;

            Assert(session.CapturePointer(7), "Active session should delegate pointer capture.");
            session.ReleasePointer(7);
            session.Publish(new CanvasPointerEventArgs());
            session.Publish(new CanvasKeyEventArgs());
            session.Dispose();
            session.Publish(new CanvasPointerEventArgs());
            session.Publish(new CanvasKeyEventArgs());
            Assert(!session.CapturePointer(8), "Disposed session must reject pointer capture.");
            session.ReleasePointer(8);

            Assert(!session.IsActive, "Disposed session must be inactive.");
            Assert(captures == 1, "Disposed session delegated an extra capture.");
            Assert(releases == 1, "Disposed session delegated an extra release.");
            Assert(pointerEvents == 1, "Disposed session retained pointer subscribers.");
            Assert(keyEvents == 1, "Disposed session retained key subscribers.");
        }

        private static void WhiteboardPageIdsRemainStableAcrossInsertAndRemove()
        {
            var ids = new Queue<string>(new[] { "page-1", "page-2", "page-new" });
            var store = new PluginWhiteboardStateStore(6, () => ids.Dequeue());
            var first = store.GetPageId(1);
            var second = store.GetPageId(2);

            store.InsertPageId(2, "inserted", 3);
            Assert(store.GetPageId(1) == first, "Insertion changed the preceding page ID.");
            Assert(store.GetPageId(2) == "inserted", "Inserted page did not retain its assigned ID.");
            Assert(store.GetPageId(3) == second, "Insertion did not shift the following stable page ID.");

            var removed = store.RemovePageId(2, 3);
            Assert(removed == "inserted", "Removal reported the wrong stable page ID.");
            Assert(store.GetPageId(1) == first, "Removal changed the preceding page ID.");
            Assert(store.GetPageId(2) == second, "Removal did not shift the following stable page ID back.");
        }

        private static void PluginCleanupContinuesAfterIndividualFailure()
        {
            var tool = new RecordingToolService { ThrowOnCleanup = true };
            var layer = new RecordingLayerService();
            var focus = new RecordingFocusInteractionService();
            var undo = new RecordingUndoService();
            var document = new RecordingDocumentService();
            var errors = 0;

            PluginCanvasResourceCleanup.Release(
                "sample.plugin",
                tool,
                layer,
                focus,
                undo,
                document,
                _ => errors++);

            Assert(tool.CleanupCalls == 1, "Tool cleanup was not attempted.");
            Assert(layer.CleanupCalls == 1, "Layer cleanup was skipped after tool cleanup failed.");
            Assert(focus.CleanupCalls == 1, "Focus cleanup was skipped after layer cleanup.");
            Assert(undo.CleanupCalls == 1, "Undo cleanup was skipped after tool cleanup failed.");
            Assert(document.CleanupCalls == 1, "Page provider cleanup was skipped after tool cleanup failed.");
            Assert(errors == 1, "Individual cleanup failure was not reported exactly once.");
        }

        private static void WhiteboardStateFollowsStablePageIds()
        {
            var store = new PluginWhiteboardStateStore(6, () => Guid.NewGuid().ToString("N"));
            var provider = new RecordingPageStateProvider();
            var first = store.GetPageId(1);
            var second = store.GetPageId(2);
            store.RegisterProvider("sample.plugin", provider, first, null);

            provider.State = "first-state";
            store.Capture(first, null);
            store.Restore(second, null);
            Assert(provider.LastRestored == null, "A new page must restore an empty plugin state.");

            provider.State = "second-state";
            store.Capture(second, null);
            store.Restore(first, null);
            Assert(provider.LastRestored == "first-state", "First page state was not restored by stable ID.");

            store.RemovePageId(1, 2);
            store.Restore(first, null);
            Assert(provider.LastRestored == null, "Removed page state was retained.");

            var restoreCount = provider.RestoreCount;
            store.UnregisterProvider("sample.plugin");
            store.Restore(second, null);
            Assert(provider.RestoreCount == restoreCount, "Unregistered provider still received restores.");
        }

        private static void WhiteboardProviderFailuresAreIsolated()
        {
            var errors = 0;
            var store = new PluginWhiteboardStateStore();
            var pageId = store.GetPageId(1);
            store.RegisterProvider("broken.plugin", new ThrowingPageStateProvider(), pageId, (_, __) => errors++);
            var healthy = new RecordingPageStateProvider { State = "healthy" };
            store.RegisterProvider("healthy.plugin", healthy, pageId, (_, __) => errors++);

            store.Capture(pageId, (_, __) => errors++);
            healthy.State = null;
            store.Restore(pageId, (_, __) => errors++);

            Assert(healthy.LastRestored == "healthy", "One provider failure prevented another provider from restoring.");
            Assert(errors == 3, "Expected register, capture, and restore failures to be reported.");
        }

        private static void WhiteboardDocumentSnapshotRoundTripsStableIdsAndUnknownPlugins()
        {
            var source = new PluginWhiteboardStateStore(6, () => Guid.NewGuid().ToString("N"));
            var firstId = source.GetPageId(1);
            var secondId = source.GetPageId(2);
            var provider = new RecordingPageStateProvider { State = "first" };
            source.RegisterProvider("sample.plugin", provider, firstId, null);
            source.Capture(firstId, null);
            provider.State = "second";
            source.Capture(secondId, null);
            var json = source.ExportDocumentJson(1, 2).Replace(
                "\"states\": {",
                "\"states\": {\"missing.plugin\": \"opaque\",");

            var restored = new PluginWhiteboardStateStore(6);
            restored.ImportDocumentJson(json, 1, 2);
            Assert(restored.GetPageId(1) == firstId, "First stable page ID changed after document round trip.");
            Assert(restored.GetPageId(2) == secondId, "Second stable page ID changed after document round trip.");
            var restoredProvider = new RecordingPageStateProvider();
            restored.RegisterProvider("sample.plugin", restoredProvider, firstId, null);
            Assert(restoredProvider.LastRestored == "first", "First page plugin state was not restored.");
            restored.Restore(secondId, null);
            Assert(restoredProvider.LastRestored == "second", "Second page plugin state was not restored.");
            var exportedAgain = restored.ExportDocumentJson(1, 2);
            Assert(exportedAgain.Contains("missing.plugin"), "Unknown plugin state was not preserved.");
        }

        private static void WhiteboardDocumentRejectsCorruptionWithoutReplacingState()
        {
            var store = new PluginWhiteboardStateStore(6, () => "original");
            Assert(store.GetPageId(1) == "original", "Test setup failed.");
            AssertThrows<System.Text.Json.JsonException>(() => store.ImportDocumentJson("{ broken", 1, 1));
            Assert(store.GetPageId(1) == "original", "Corrupt document replaced the existing page identity.");
        }

        private static void WhiteboardSinglePageImportRemapsSavedPageToCurrentIndex()
        {
            var source = new PluginWhiteboardStateStore(6, () => "saved-page");
            var provider = new RecordingPageStateProvider { State = "saved-state" };
            source.RegisterProvider("sample.plugin", provider, source.GetPageId(4), null);
            source.Capture(source.GetPageId(4), null);
            var json = source.ExportDocumentJson(4, 1);

            var target = new PluginWhiteboardStateStore(6, () => "old-page");
            target.GetPageId(1);
            target.ImportSinglePageJson(json, 1);
            Assert(target.GetPageId(1) == "saved-page", "Saved page identity was not remapped to the current index.");
            var restored = new RecordingPageStateProvider();
            target.RegisterProvider("sample.plugin", restored, target.GetPageId(1), null);
            Assert(restored.LastRestored == "saved-state", "Saved plugin state was not restored on the current page.");
        }

        private static void WhiteboardLegacyImporterFillsOnlyMissingPluginState()
        {
            var store = new PluginWhiteboardStateStore(8, () => "generated-page");
            var importer = new RecordingLegacyImporter();
            store.RegisterLegacyImporter("sample.plugin", importer);
            store.ImportLegacyPageSidecars("lesson.icstk", 1, (_, ex) => throw ex);
            store.ImportLegacyPageSidecars("lesson.icstk", 1, (_, ex) => throw ex);

            var provider = new RecordingPageStateProvider();
            store.RegisterProvider(
                "sample.plugin",
                provider,
                store.GetPageId(1),
                (_, ex) => throw ex);
            Assert(provider.LastRestored == "sidecar:lesson.icstk", "Legacy sidecar state was not restored.");
            Assert(importer.SidecarCalls == 1, "Existing imported state must not be overwritten.");
        }

        private static void WhiteboardInitialHistoryPreservesLoadedStateAndEmptyBaseline()
        {
            var store = new PluginWhiteboardStateStore(4, () => "page-1");
            var pageId = store.GetPageId(1);
            var provider = new RecordingPageStateProvider { State = "loaded" };
            store.RegisterProvider("sample.plugin", provider, pageId, null);
            store.Capture(pageId, null);

            var histories = store.GetInitialHistories(1);
            Assert(histories.Count == 1, "Loaded plugin state did not create an initial history entry.");
            Assert(histories[0].PluginId == "sample.plugin" &&
                   histories[0].EmptyState == "empty" &&
                   histories[0].LoadedState == "loaded",
                "Initial plugin history did not preserve its empty and loaded snapshots.");
        }

        private static void WhiteboardInitialHistoryFailuresAreIsolated()
        {
            var errors = 0;
            var store = new PluginWhiteboardStateStore(4, () => "page-1");
            var pageId = store.GetPageId(1);
            store.RegisterProvider("broken.plugin", new ThrowingInitialHistoryProvider(), pageId, null);
            store.RegisterProvider("healthy.plugin", new RecordingPageStateProvider { State = "loaded" }, pageId, null);
            store.Capture(pageId, null);

            var histories = store.GetInitialHistories(1, (_, __) => errors++);
            Assert(histories.Count == 1 && histories[0].PluginId == "healthy.plugin",
                "One initial-history provider failure prevented a healthy provider from exporting history.");
            Assert(errors == 1, "Initial-history provider failure was not isolated exactly once.");
        }

        private static void WhiteboardCompanionStatesUseCapturedPageState()
        {
            var store = new PluginWhiteboardStateStore(4, () => "page-1");
            var pageId = store.GetPageId(1);
            var provider = new RecordingPageStateProvider { State = "schema-3-json" };
            store.RegisterProvider("sample.plugin", provider, pageId, null);
            store.Capture(pageId, null);

            var companions = store.GetCompanionStates(1, (_, ex) => throw ex);
            Assert(companions.Count == 1, "Captured page state did not produce one companion file.");
            Assert(companions[0].FileExtension == ".sample.json" &&
                   companions[0].Content == "companion:schema-3-json",
                "Companion export did not preserve the provider extension and captured state.");
        }

        private static void Run(string name, Action test)
        {
            test();
            _passed++;
            Console.WriteLine("PASS " + name);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void AssertThrows<T>(Action action) where T : Exception
        {
            try
            {
                action();
            }
            catch (T)
            {
                return;
            }

            throw new InvalidOperationException($"Expected {typeof(T).Name}.");
        }

        private sealed class RecordingPageStateProvider :
            IWhiteboardPageStateProvider,
            IWhiteboardInitialHistoryProvider,
            IWhiteboardCompanionStateProvider
        {
            internal string State { get; set; }
            internal string LastRestored { get; private set; }
            internal int RestoreCount { get; private set; }

            public string CaptureState() => State;
            public string CaptureEmptyState() => "empty";
            public string CompanionFileExtension => ".sample.json";
            public string ExportCompanionState(string state) => "companion:" + state;

            public void RestoreState(string state)
            {
                LastRestored = state;
                RestoreCount++;
            }
        }

        private sealed class SettingsPlugin : IPlugin
        {
            private readonly object _view;
            private readonly bool _throw;

            internal SettingsPlugin(object view, bool @throw = false)
            {
                _view = view;
                _throw = @throw;
            }

            public string Id => "test";
            public string Name => "Test";
            public string Version => "1.0.0";
            public string Description => string.Empty;
            public string Author => string.Empty;
            public int Order => 0;
            public void Initialize(IPluginHost host) { }
            public void Shutdown() { }
            public object GetMainView() => null;
            public object GetSettingsView()
            {
                if (_throw) throw new InvalidOperationException("settings failed");
                return _view;
            }
        }

        private sealed class ThrowingInitialHistoryProvider :
            IWhiteboardPageStateProvider,
            IWhiteboardInitialHistoryProvider
        {
            public string CaptureState() => "loaded";
            public string CaptureEmptyState() => throw new InvalidOperationException("empty failed");
            public void RestoreState(string state) { }
        }

        private sealed class ThrowingPageStateProvider : IWhiteboardPageStateProvider
        {
            public string CaptureState() => throw new InvalidOperationException("capture failed");
            public void RestoreState(string state) => throw new InvalidOperationException("restore failed");
        }

        private sealed class RecordingLegacyImporter : IWhiteboardLegacyStateImporter
        {
            internal int SidecarCalls { get; private set; }

            public string TryImportPageSidecar(string contentFilePath)
            {
                SidecarCalls++;
                return "sidecar:" + contentFilePath;
            }

            public string TryImportPackagePage(string extractedDirectory, int pageIndex)
                => $"package:{extractedDirectory}:{pageIndex}";
        }

        private sealed class RecordingToolService : ICanvasToolService
        {
            internal bool ThrowOnCleanup { get; set; }
            internal int CleanupCalls { get; private set; }
            public bool TryActivateTool(string pluginId, string toolId, out ICanvasToolSession session)
            {
                session = null;
                return false;
            }
            public void DeactivateTools(string pluginId)
            {
                CleanupCalls++;
                if (ThrowOnCleanup) throw new InvalidOperationException("tool cleanup failed");
            }
        }

        private sealed class RecordingLayerService : ICanvasLayerService
        {
            internal int CleanupCalls { get; private set; }
            public void RegisterLayer(string pluginId, string layerId, CanvasLayerPlacement placement,
                Func<System.Windows.FrameworkElement> layerFactory, bool isHitTestVisible = false) { }
            public bool RemoveLayer(string pluginId, string layerId) => false;
            public void RemoveLayers(string pluginId) => CleanupCalls++;
        }

        private sealed class RecordingFocusInteractionService : IFocusInteractionService
        {
            internal int CleanupCalls { get; private set; }
            public void SetActive(string pluginId, bool active)
            {
                if (!active) CleanupCalls++;
            }
        }

        private sealed class RecordingUndoService : IUndoService
        {
            internal int CleanupCalls { get; private set; }
            public void RegisterStateHandler(string pluginId, Action<string> restoreState) { }
            public void UnregisterStateHandler(string pluginId) => CleanupCalls++;
            public bool CommitState(string pluginId, string beforeState, string afterState) => false;
        }

        private sealed class RecordingDocumentService : IWhiteboardDocumentService
        {
            internal int CleanupCalls { get; private set; }
            public WhiteboardPageInfo CurrentPage => null;
            public event EventHandler<WhiteboardPageChangingEventArgs> PageChanging { add { } remove { } }
            public event EventHandler<WhiteboardPageChangedEventArgs> PageChanged { add { } remove { } }
            public event EventHandler<WhiteboardPageRemovedEventArgs> PageRemoved { add { } remove { } }
            public event EventHandler PageClearing { add { } remove { } }
            public bool TryBeginMutation(string action) => true;
            public void RegisterPageStateProvider(string pluginId, IWhiteboardPageStateProvider provider) { }
            public void RegisterLegacyStateImporter(string pluginId, IWhiteboardLegacyStateImporter importer) { }
            public void UnregisterPageStateProvider(string pluginId) => CleanupCalls++;
        }
    }
}
