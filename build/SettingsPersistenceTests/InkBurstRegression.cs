using System.Diagnostics;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Threading;
using Ink_Canvas.Helpers;

internal static class InkBurstRegression
{
    internal static void Resampling(Action<bool, string> check)
    {
        var smoothing = new AsyncAdvancedBezierSmoothing(Dispatcher.CurrentDispatcher);
        try
        {
            var method = typeof(AsyncAdvancedBezierSmoothing).GetMethod("ResampleEquidistantOptimized",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            StylusPoint[] Sample(StylusPoint[] points, double interval, int limit) =>
                (StylusPoint[])method.Invoke(smoothing, new object[] { points, interval, limit });
            using var bytes = new System.IO.MemoryStream();
            using var writer = new System.IO.BinaryWriter(bytes);
            foreach (int count in new[] { 6, 16, 48, 128 })
            foreach (double spacing in new[] { .4, 4, 40, 400 })
            {
                var points = Enumerable.Range(0, count).Select(i => new StylusPoint(i * spacing,
                    Math.Sin(i * .2) * spacing, .1f + .8f * i / (count - 1))).ToArray();
                int limit = count * 3;
                var result = Sample(points, 2, limit);
                check(result.Length <= limit, "Resampling exceeded its output limit");
                check(result[0].X == points[0].X && Math.Abs(result[^1].X - points[^1].X) < .000001,
                    "Resampling discarded an endpoint");
                check(result.All(p => float.IsFinite(p.PressureFactor) && p.PressureFactor >= .1f && p.PressureFactor <= 1),
                    "Resampling produced invalid pressure");
                writer.Write(result.Length);
                foreach (var point in result) { writer.Write(point.X); writer.Write(point.Y); writer.Write(point.PressureFactor); }
            }
            writer.Flush();
            Console.WriteLine("RESAMPLING_FINGERPRINT: " + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes.ToArray())));
            var sparse = Enumerable.Range(0, 48).Select(i => new StylusPoint(i * 1000, Math.Sin(i), .5f)).ToArray();
            Sample(sparse, 2, 144);
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < 20; i++) Sample(sparse, 2, 144);
            Console.WriteLine($"Sparse resampling / 20 runs: {watch.Elapsed.TotalMilliseconds:F1}ms, {GC.GetAllocatedBytesForCurrentThread() - allocated:N0}B");
        }
        finally { smoothing.Dispose(); }
    }

    internal static void Run(Action<bool, string> check)
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        var smoothing = new AsyncAdvancedBezierSmoothing(dispatcher) { MaxConcurrentTasks = 2 };
        Stroke Make(int index) => new(new StylusPointCollection(Enumerable.Range(0, 120)
            .Select(i => new StylusPoint(i, index + Math.Sin(i * .1) * 20, .1f + i * .006f))));
        try
        {
            var order = new List<string>();
            var queued = WaitForCallback(() => smoothing.SmoothStrokeAsync(Make(0), (_, _) => order.Add("smoothing")), out var processing);
            var input = dispatcher.InvokeAsync(() => order.Add("input"), DispatcherPriority.Input);
            PumpUntil(() => processing.IsCompleted && input.Task.IsCompleted);
            processing.GetAwaiter().GetResult();
            Console.WriteLine($"Queued smoothing/input order: {string.Join(", ", order)}; callback priority={queued.Priority}");
            check(order.SequenceEqual(new[] { "input", "smoothing" }), "Completed smoothing overtook pending pen input");
            check(queued.Priority == DispatcherPriority.Background, "Postprocessing uses a priority above input/rendering");

            using var cancellation = new CancellationTokenSource();
            var source = Make(1); bool delivered = false;
            WaitForCallback(() => smoothing.SmoothStrokeAsync(source, (_, _) => delivered = true, cancellation.Token), out var canceled);
            cancellation.Cancel();
            PumpUntil(() => canceled.IsCompleted);
            check(!delivered && ReferenceEquals(canceled.GetAwaiter().GetResult(), source), "Canceled queued work still modified the canvas");

            int completed = 0, inputs = 0;
            var watch = Stopwatch.StartNew();
            var tasks = Enumerable.Range(0, 32).Select(i => smoothing.SmoothStrokeAsync(Make(i), (_, _) =>
            {
                dispatcher.VerifyAccess(); completed++;
            })).ToArray();
            for (int i = 0; i < 100; i++) dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => inputs++));
            var all = Task.WhenAll(tasks);
            PumpUntil(() => all.IsCompleted && inputs == 100);
            var results = all.GetAwaiter().GetResult();
            check(completed == 32 && results.Length == 32 && inputs == 100, "Burst input or smoothing completion was lost");
            check(results.All(s => s.StylusPoints.Count > 2 && s.StylusPoints.Select(p => p.PressureFactor).Distinct().Count() > 1),
                "Burst processing lost geometry or pressure variation");
            Console.WriteLine($"Burst: 32 strokes / 100 input operations completed in {watch.Elapsed.TotalMilliseconds:F1}ms");
            using var preCanceled = new CancellationTokenSource(); preCanceled.Cancel();
            var untouched = Make(2);
            check(ReferenceEquals(smoothing.SmoothStrokeAsync(untouched, (_, _) => throw new InvalidOperationException("Canceled callback"), preCanceled.Token)
                .GetAwaiter().GetResult(), untouched), "Pre-canceled processing did not preserve its original stroke");
        }
        finally { smoothing.Dispose(); }
    }

    private static DispatcherOperation WaitForCallback(Func<Task<Stroke>> start, out Task<Stroke> task)
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        DispatcherOperation queued = null;
        void Posted(object sender, DispatcherHookEventArgs e)
        {
            if (e.Operation.Priority is DispatcherPriority.Normal or DispatcherPriority.Background)
                Interlocked.CompareExchange(ref queued, e.Operation, null);
        }
        dispatcher.Hooks.OperationPosted += Posted;
        try
        {
            task = start();
            // Intentionally hold the dispatcher until the worker has queued its
            // completion, reproducing a busy UI without a timing-based assertion.
            if (!SpinWait.SpinUntil(() => Volatile.Read(ref queued) != null, TimeSpan.FromSeconds(10)))
                throw new TimeoutException("No smoothing callback was queued");
            return queued;
        }
        finally { dispatcher.Hooks.OperationPosted -= Posted; }
    }

    private static void PumpUntil(Func<bool> done)
    {
        var watch = Stopwatch.StartNew();
        while (!done())
        {
            if (watch.Elapsed.TotalSeconds > 15) throw new TimeoutException("Ink burst did not drain");
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle) { Interval = TimeSpan.FromMilliseconds(5) };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start(); Dispatcher.PushFrame(frame);
        }
    }
}
