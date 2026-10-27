using Ink_Canvas.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using ProcessProtectionManager = Ink_Canvas.Helpers.ProcessProtectionManager;
namespace Ink_Canvas.Windows.SettingsViews.Helpers
{
    public static class SettingsManager
    {
        public static Settings Settings { get; set; } = new Settings();

        public static string SettingsFileName { get; } = Path.Combine("Configs", "Settings.json");
        private static DispatcherCoalescedAction _pendingSave;
        private static int _initialSavePosted;

        public static bool ReadEnableWindowChromeRendering()
        {
            try
            {
                var path = Path.Combine(App.RootPath, SettingsFileName);
                if (!File.Exists(path)) return Settings?.Startup?.EnableWindowChromeRendering ?? false;

                var json = File.ReadAllText(path);
                var obj = JObject.Parse(json);
                return obj.SelectToken("startup.enableWindowChromeRendering")?.Value<bool>() ?? false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                return Settings?.Startup?.EnableWindowChromeRendering ?? false;
            }
        }

        public static void SaveSettingsToFile()
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted)
            {
                SaveSettingsSnapshot();
                return;
            }
            if (!dispatcher.CheckAccess())
            {
                // Reuse the existing scheduler once initialized; no callback per slider tick.
                var pending = System.Threading.Volatile.Read(ref _pendingSave);
                if (pending != null) pending.Request();
                else if (System.Threading.Interlocked.Exchange(ref _initialSavePosted, 1) == 0)
                    _ = dispatcher.BeginInvoke(new Action(() =>
                    {
                        System.Threading.Interlocked.Exchange(ref _initialSavePosted, 0);
                        SaveSettingsToFile();
                    }));
                return;
            }
            _pendingSave ??= new DispatcherCoalescedAction(dispatcher, TimeSpan.FromMilliseconds(150), SaveSettingsSnapshot);
            _pendingSave.Request();
        }

        internal static void FlushPendingSettings()
        {
            if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == true)
            {
                if (System.Threading.Interlocked.Exchange(ref _initialSavePosted, 0) != 0)
                    SaveSettingsSnapshot();
                _pendingSave?.Flush();
            }
        }

        private static void SaveSettingsSnapshot()
        {
            try
            {
                // Capture on the caller's owning thread; only immutable JSON reaches disk I/O.
                var text = JsonConvert.SerializeObject(Settings, Formatting.Indented);
                AtomicFileWriter.WriteLatest(Path.Combine(App.RootPath, SettingsFileName), text);
                App.UpdateCachedSettingsJson(text);
            }
            catch (Exception ex) { LogHelper.WriteLogToFile($"[Settings save] {ex}", LogHelper.LogType.Error); }
        }

        public static bool TrySaveSettingsToFile()
        {
            var text = JsonConvert.SerializeObject(Settings, Formatting.Indented);
            return AtomicFileWriter.WriteLatestAsync(Path.Combine(App.RootPath, SettingsFileName), text)
                .GetAwaiter().GetResult();
        }

        private static readonly System.Threading.SemaphoreSlim CalibrationGate = new(1, 1);

        public static async System.Threading.Tasks.Task<bool> TrySaveCalibrationAsync(Ink.TouchCalibrationRecord record, int? nibWidth = null,
            Ink.PalmEraserOptions palmOptions = null)
        {
            if (record != null && !record.IsValid()) return false;
            if (!await CalibrationGate.WaitAsync(0)) return false;
            var previous = Settings.Advanced.TouchCalibration;
            var previousNib = Settings.Advanced.NibModeBoundsWidth;
            var previousOptions = Settings.Advanced.PalmEraser;
            try
            {
                Settings.Advanced.TouchCalibration = record;
                if (palmOptions != null) Settings.Advanced.PalmEraser = palmOptions.Sanitize();
                if (nibWidth.HasValue) Settings.Advanced.NibModeBoundsWidth = Math.Clamp(nibWidth.Value, 1, 50);
                var text = JsonConvert.SerializeObject(Settings, Formatting.Indented);
                if (await AtomicFileWriter.WriteLatestAsync(Path.Combine(App.RootPath, SettingsFileName), text))
                {
                    App.UpdateCachedSettingsJson(text);
                    (System.Windows.Application.Current.MainWindow as MainWindow)?.ResetTouchCalibrationInput();
                    SettingsActionHub.OnNibModeBoundsWidthChanged();
                    return true;
                }
                Settings.Advanced.TouchCalibration = previous;
                Settings.Advanced.PalmEraser = previousOptions;
                Settings.Advanced.NibModeBoundsWidth = previousNib;
                SaveSettingsToFile();
                return false;
            }
            catch (Exception ex)
            {
                Settings.Advanced.TouchCalibration = previous;
                Settings.Advanced.PalmEraser = previousOptions;
                Settings.Advanced.NibModeBoundsWidth = previousNib;
                SaveSettingsToFile();
                LogHelper.WriteLogToFile($"[Calibration save] {ex}", LogHelper.LogType.Error);
                return false;
            }
            finally { CalibrationGate.Release(); }
        }
        public static void MigrateChickenSoupSettings()
        {
            if (Settings?.Appearance == null) return;

            var appearance = Settings.Appearance;
            if ((appearance.EnabledPresetTipsSources == null || appearance.EnabledPresetTipsSources.Count == 0)
                && appearance.ChickenSoupSource >= 0)
            {
                string presetId = null;
                switch (appearance.ChickenSoupSource)
                {
                    case 0: presetId = "osu"; break;
                    case 1: presetId = "mottos"; break;
                    case 2: presetId = "gaokao"; break;
                    case 3: presetId = "hitokoto"; break;
                    case 4: presetId = "phigros"; break;
                }
                if (presetId != null)
                {
                    appearance.EnabledPresetTipsSources = new List<string> { presetId };
                }
            }
        }

        public static void MigrateVideoBoothSettings()
        {
            if (Settings?.Canvas == null) return;

            if (Settings.Canvas.LaunchSeewoVideoShowcaseForWhiteboardBooth && Settings.Canvas.VideoBoothMode == VideoBoothMode.BuiltIn)
            {
                Settings.Canvas.VideoBoothMode = VideoBoothMode.Seewo;
            }
        }
    }
}
