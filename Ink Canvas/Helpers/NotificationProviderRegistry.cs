using Ink_Canvas.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ink_Canvas.Helpers
{
    internal static class NotificationProviderRegistry
    {
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<string, NotificationProviderStatus> Providers = new Dictionary<string, NotificationProviderStatus>(StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyList<NotificationProviderStatus> GetProviders()
        {
            lock (SyncRoot)
            {
                return Providers.Values
                    .Select(Clone)
                    .OrderBy(x => x.ProviderId)
                    .ToList();
            }
        }

        public static void RegisterOrUpdate(NotificationProviderStatus status)
        {
            if (status == null || string.IsNullOrWhiteSpace(status.ProviderId)) return;

            lock (SyncRoot)
            {
                status.LastUpdatedAt = DateTime.Now;
                Providers[status.ProviderId] = Clone(status);
            }
        }

        public static void SetRunning(string providerId, bool isRunning, string status)
        {
            if (string.IsNullOrWhiteSpace(providerId)) return;

            lock (SyncRoot)
            {
                if (!Providers.TryGetValue(providerId, out var provider))
                {
                    provider = new NotificationProviderStatus
                    {
                        ProviderId = providerId,
                        DisplayName = providerId
                    };
                }

                provider.IsRunning = isRunning;
                provider.Status = status ?? string.Empty;
                provider.LastUpdatedAt = DateTime.Now;
                Providers[providerId] = provider;
            }
        }

        private static NotificationProviderStatus Clone(NotificationProviderStatus source)
        {
            return new NotificationProviderStatus
            {
                ProviderId = source.ProviderId,
                DisplayName = source.DisplayName,
                Description = source.Description,
                IsEnabled = source.IsEnabled,
                IsRunning = source.IsRunning,
                Status = source.Status,
                LastUpdatedAt = source.LastUpdatedAt
            };
        }
    }
}
