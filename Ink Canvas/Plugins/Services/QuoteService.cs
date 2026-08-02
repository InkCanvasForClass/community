using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// <see cref="IQuoteService"/> 的宿主实现：包装 <see cref="Ink_Canvas.ChickenSoup"/> 与
    /// MainWindow 的白板名言刷新方法。
    /// </summary>
    internal sealed class QuoteService : IQuoteService
    {
        private readonly MainWindow _mainWindow;

        public QuoteService(MainWindow mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        }

        public IReadOnlyList<PluginQuoteScheme> GetPresetSchemes()
        {
            try
            {
                return Ink_Canvas.ChickenSoup.GetPresetSchemes()
                    .Select(s => new PluginQuoteScheme
                    {
                        PresetId = s.PresetId ?? "",
                        Name = s.Name ?? "",
                        IsPreset = s.IsPreset,
                        IsEnabled = s.IsEnabled,
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"QuoteService.GetPresetSchemes failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
                return new List<PluginQuoteScheme>();
            }
        }

        public string[] GetTipsFromPreset(string presetId)
        {
            try { return Ink_Canvas.ChickenSoup.GetTipsFromPreset(presetId); }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"QuoteService.GetTipsFromPreset failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
                return null;
            }
        }

        public Task RefreshAsync()
        {
            try { return _mainWindow.UpdateChickenSoupTextAsync(); }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"QuoteService.RefreshAsync failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
                return Task.CompletedTask;
            }
        }
    }
}
