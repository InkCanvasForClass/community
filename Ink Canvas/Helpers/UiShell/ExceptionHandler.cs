using System;
using System.Threading.Tasks;

namespace Ink_Canvas.Helpers
{
    internal static class ExceptionHandler
    {
        public static bool HandleException(
            Exception exception,
            string context,
            LogHelper.LogType logLevel = LogHelper.LogType.Error)
        {
            if (exception == null)
                return true;

            var logMessage = $"{context}: {exception.Message}";

            if (exception.InnerException != null)
            {
                logMessage += $"\nInner Exception: {exception.InnerException.Message}";
            }

            LogHelper.WriteLogToFile(logMessage, logLevel);

            return ShouldContinueExecution(exception);
        }

        private static bool ShouldContinueExecution(Exception exception)
        {
            if (exception is OutOfMemoryException ||
                exception is AccessViolationException)
            {
                return false;
            }

            return true;
        }

        public static void TryExecute(
            Action action,
            string context,
            bool continueOnError = true)
        {
            try
            {
                action?.Invoke();
            }
            catch (Exception ex)
            {
                var shouldContinue = HandleException(ex, context);
                if (!shouldContinue && !continueOnError)
                {
                    throw;
                }
            }
        }

        public static async Task TryExecuteAsync(
            Func<Task> action,
            string context,
            bool continueOnError = true)
        {
            try
            {
                if (action != null)
                {
                    await action();
                }
            }
            catch (Exception ex)
            {
                var shouldContinue = HandleException(ex, context);
                if (!shouldContinue && !continueOnError)
                {
                    throw;
                }
            }
        }
    }
}
