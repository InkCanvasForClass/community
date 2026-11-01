using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Ink_Canvas.Ink
{
    public enum PalmContactSource { None, Software, RawHid }
    public enum PalmSourceMode { Auto, RawHid, Software }
    public enum PalmSizeLimit { None, Floor, Cap }
    public enum PalmAspectSource { Contact, Trace, Custom }
    public enum PalmHidScale { Stretch, Isotropic }

    // Value snapshots can be exchanged between the WPF and independent ink threads.
    // Defaults and calculations follow TouchSuite.App at 719f06e0; see docs/palm-eraser.md.
    public sealed record PalmEraserOptions
    {
        public PalmSourceMode Source { get; init; } = PalmSourceMode.Auto;
        public string HidDevice { get; init; } = "";
        public PalmHidScale HidScale { get; init; }
        public bool FollowSize { get; init; } = true;
        public bool LockSize { get; init; }
        public bool Smooth { get; init; } = true;
        public PalmSizeLimit Limit { get; init; } = PalmSizeLimit.Floor;
        public bool PressureEnabled { get; init; } = true;
        public double PressureThreshold { get; init; } = 0.5;
        public double PressureMaximum { get; init; } = 2;
        public bool AreaEnabled { get; init; } = true;
        // Null means the arithmetic midpoint of the calibrated finger and palm areas.
        public double? AreaThresholdPx2 { get; init; }
        public double AreaTrim { get; init; } = 1;
        public PalmAspectSource Aspect { get; init; }
        public double CustomAspect { get; init; } = 1;
        public bool MultiTouch { get; init; } = true;
        public int MinimumContacts { get; init; } = 3;
        public int GroupWindowMs { get; init; } = 200;
        public double GroupTolerance { get; init; } = 1;

        public PalmEraserOptions Sanitize() => this with
        {
            Source = Enum.IsDefined(Source) ? Source : PalmSourceMode.Auto,
            HidDevice = HidDevice ?? "",
            HidScale = Enum.IsDefined(HidScale) ? HidScale : PalmHidScale.Stretch,
            Limit = Enum.IsDefined(Limit) ? Limit : PalmSizeLimit.Floor,
            Aspect = Enum.IsDefined(Aspect) ? Aspect : PalmAspectSource.Contact,
            PressureThreshold = FiniteClamp(PressureThreshold, 0, 1, 0.5),
            PressureMaximum = FiniteClamp(PressureMaximum, 1, 3, 2),
            AreaThresholdPx2 = AreaThresholdPx2 is double a && double.IsFinite(a) && a >= 0 ? a : null,
            AreaTrim = FiniteClamp(AreaTrim, 0.5, 2, 1),
            CustomAspect = double.IsFinite(CustomAspect) && CustomAspect > 0 ? CustomAspect : 1,
            MinimumContacts = Math.Clamp(MinimumContacts, 2, 64),
            GroupWindowMs = Math.Clamp(GroupWindowMs, 0, 1000),
            GroupTolerance = FiniteClamp(GroupTolerance, 0.1, 10, 1)
        };

        private static double FiniteClamp(double value, double min, double max, double fallback) =>
            double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
    }

    internal sealed record PalmEraserProfile
    {
        public PalmEraserOptions Options { get; init; } = new();
        public double DpiX { get; init; } = 1;
        public double DpiY { get; init; } = 1;
        public double PalmWidthPx { get; init; }
        public double PalmHeightPx { get; init; }
        public double PalmContactAreaPx2 { get; init; }
        public double FingerContactAreaPx2 { get; init; }
        public bool Ellipse { get; init; }
        public double ThresholdPx2 => Options.AreaThresholdPx2 ??
            (PalmContactAreaPx2 > 0 && FingerContactAreaPx2 > 0
                ? (PalmContactAreaPx2 + FingerContactAreaPx2) / 2 : Math.Max(0, PalmContactAreaPx2));
    }

    internal readonly record struct PalmEraserFrame(int Id, Point CenterPx, double WidthPx, double HeightPx, bool Group, int Revision);

    /// <summary>
    /// Single-threaded contact classifier and area calculator. All input, calibration and output
    /// use physical pixels. Adapters own capture, stroke cancellation, undo and DIP conversion.
    /// </summary>
    internal sealed class PalmEraserEngine
    {
        internal const int FreshMilliseconds = 300;
        private readonly Func<long> _clock;
        private readonly Dictionary<int, Contact> _contacts = new();
        private readonly Dictionary<PalmContactSource, (long Time, bool Valid)> _sources = new();
        private readonly Dictionary<PalmContactSource, long> _observed = new();
        private readonly List<int> _expired = new();
        private PalmEraserProfile _profile = new();
        private PalmContactSource _locked;
        private string _device = "";
        private long _lastContact, _pressureTime;
        private double? _pressure;
        private int _revision;

        private sealed class Contact
        {
            internal readonly List<Rect> History = new(5);
            internal long Down, Last;
            internal Rect Current;
            internal double PeakArea, LockedArea, ShownWidth, ShownHeight;
            internal bool Latched, Group, Decided;
        }

        public PalmEraserEngine(Func<long> clock = null) => _clock = clock ?? (() => Environment.TickCount64);
        public PalmContactSource ActiveSource { get; private set; }
        internal PalmContactSource LockedSource => _locked;
        internal int Count => _contacts.Count;
        internal PalmEraserProfile Profile => _profile;
        private PalmEraserOptions Options => _profile.Options;
        private bool LockActive => Options.LockSize && (Options.FollowSize || Options.PressureEnabled);
        private double PalmArea => _profile.PalmWidthPx * _profile.PalmHeightPx;
        internal IEnumerable<int> PalmIds => _contacts.Where(p => !IsWriting(p.Key)).Select(p => p.Key);

        public void Configure(PalmEraserProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (_profile == profile) return;
            _profile = profile;
            Clear();
        }

        public void Clear()
        {
            _contacts.Clear();
            _revision++;
            _sources.Clear();
            _observed.Clear();
            _locked = ActiveSource = PalmContactSource.None;
            _lastContact = _pressureTime = 0;
            _pressure = null;
            _device = "";
        }

        public bool AcceptDevice(string device)
        {
            device ??= "";
            if (Options.HidDevice.Length > 0)
                return string.Equals(device, Options.HidDevice, StringComparison.OrdinalIgnoreCase);
            if (device.Length == 0) return true;
            if (_device.Length == 0) _device = device;
            return string.Equals(device, _device, StringComparison.OrdinalIgnoreCase);
        }

        public void NotePressure(double? pressure)
        {
            if (pressure is not double value || !double.IsFinite(value)) return;
            value = Math.Clamp(value, 0, 1);
            if (Options.Smooth && _pressure is double previous)
            {
                value = previous + 0.20 * (value - previous);
                if (Math.Abs(value - previous) < 0.004) value = previous;
            }
            _pressure = value;
            _pressureTime = _clock();
        }

        internal double PressureGain
        {
            get
            {
                double p = Options.PressureEnabled
                    ? (_pressure.HasValue && _clock() - _pressureTime <= FreshMilliseconds ? _pressure.Value : 0)
                    : 0.5;
                double threshold = Options.PressureThreshold;
                return threshold >= 1 ? 1 : 1 + Math.Clamp((p - threshold) / (1 - threshold), 0, 1)
                    * (Math.Clamp(Options.PressureMaximum, 1, 3) - 1);
            }
        }

        // Returns true only when this source supplies the contact tracks. A rejected source
        // may refresh arbitration, but must never inject a second copy of a physical finger.
        public bool Submit(PalmContactSource source, int id, Rect pixels, bool threshold = true, string device = "")
        {
            if (source == PalmContactSource.None || !Valid(pixels)) return false;
            if (source == PalmContactSource.RawHid && !AcceptDevice(device)) return false;
            long now = _clock();
            bool eligible = !threshold || (pixels.Width >= 0.5 && pixels.Height >= 0.5);
            _sources[source] = (now, eligible);
            if (eligible)
            {
                _observed.TryAdd(source, now);
                if (now - _lastContact > FreshMilliseconds) _contacts.Clear();
                _lastContact = now;
            }
            else _observed.Remove(source);
            Expire(now);
            var effective = Resolve(now);
            if (effective == PalmContactSource.None || effective != source) return false;
            if (ActiveSource != effective)
            {
                _contacts.Clear();
                ActiveSource = effective;
            }
            if (eligible) Update(id, pixels, now);
            DecideGroups(now);
            return eligible;
        }

        private bool Fresh(PalmContactSource source, long now) =>
            _sources.TryGetValue(source, out var state) && state.Valid && now - state.Time <= FreshMilliseconds;

        private PalmContactSource Resolve(long now)
        {
            if (Options.Source != PalmSourceMode.Auto)
            {
                var selected = Options.Source == PalmSourceMode.RawHid ? PalmContactSource.RawHid : PalmContactSource.Software;
                return Fresh(selected, now) ? selected : PalmContactSource.None;
            }
            if (_locked != PalmContactSource.None) return _locked;
            if (Observed(PalmContactSource.RawHid, now)) return _locked = PalmContactSource.RawHid;
            if (Observed(PalmContactSource.Software, now)) return _locked = PalmContactSource.Software;
            return Fresh(PalmContactSource.RawHid, now) ? PalmContactSource.RawHid
                : Fresh(PalmContactSource.Software, now) ? PalmContactSource.Software : PalmContactSource.None;
        }

        private bool Observed(PalmContactSource source, long now) => Fresh(source, now)
            && _observed.TryGetValue(source, out long since) && now - since >= FreshMilliseconds;

        public void Tick()
        {
            long now = _clock();
            Expire(now);
            if (!Fresh(PalmContactSource.RawHid, now) && !Fresh(PalmContactSource.Software, now))
                ActiveSource = PalmContactSource.None;
        }

        private void Expire(long now)
        {
            _expired.Clear();
            foreach (var pair in _contacts)
                if (now - pair.Value.Last > FreshMilliseconds) _expired.Add(pair.Key);
            foreach (int id in _expired) { _contacts.Remove(id); _revision++; }
        }

        public void Remove(PalmContactSource source, int id)
        {
            if (source == ActiveSource && _contacts.Remove(id)) _revision++;
        }

        private static bool Valid(Rect r) => !r.IsEmpty && r.Width > 0 && r.Height > 0
            && double.IsFinite(r.X) && double.IsFinite(r.Y) && double.IsFinite(r.Width) && double.IsFinite(r.Height)
            && double.IsFinite(r.Width * r.Height);

        private void Update(int id, Rect rect, long now)
        {
            if (!_contacts.TryGetValue(id, out var contact))
            {
                _contacts.Add(id, contact = new Contact { Down = now });
                _revision++;
            }
            var history = contact.History;
            if (history.Count > 0)
            {
                Rect previous = history[^1];
                if (Math.Abs(previous.X - rect.X) < 0.01 && Math.Abs(previous.Y - rect.Y) < 0.01
                    && Math.Abs(previous.Width - rect.Width) < 0.01 && Math.Abs(previous.Height - rect.Height) < 0.01)
                    return;
            }
            if (history.Count == 5) history.RemoveAt(0);
            history.Add(rect);
            double width = Median(history, true), height = Median(history, false);
            contact.Current = new Rect(rect.X + (rect.Width - width) / 2, rect.Y + (rect.Height - height) / 2, width, height);
            contact.Last = now;
            if (LockActive)
            {
                contact.LockedArea = Math.Max(contact.LockedArea, RawArea(contact.Current));
                if (Options.AreaEnabled && _profile.ThresholdPx2 > 0 && width * height >= _profile.ThresholdPx2)
                    contact.Latched = true;
            }
            contact.PeakArea = Math.Max(contact.PeakArea, width * height);
        }

        private static double Median(List<Rect> history, bool width)
        {
            Span<double> values = stackalloc double[5];
            for (int i = 0; i < history.Count; i++) values[i] = width ? history[i].Width : history[i].Height;
            values = values[..history.Count];
            values.Sort();
            int middle = values.Length / 2;
            return values.Length % 2 == 0 ? (values[middle - 1] + values[middle]) / 2 : values[middle];
        }

        private void DecideGroups(long now)
        {
            if (!Options.MultiTouch)
            {
                foreach (var c in _contacts.Values) c.Group = c.Decided = false;
                return;
            }
            foreach (var anchor in _contacts.Values)
            {
                if (anchor.Decided) continue;
                var group = _contacts.Values.Where(c => Math.Abs(c.Down - anchor.Down) <= Options.GroupWindowMs).ToArray();
                if (group.Length < Options.MinimumContacts || now - anchor.Down < Options.GroupWindowMs) continue;
                double left = double.MaxValue, top = double.MaxValue, right = double.MinValue, bottom = double.MinValue;
                foreach (var c in group)
                    foreach (var r in c.History)
                    {
                        double x = r.X + r.Width / 2, y = r.Y + r.Height / 2;
                        left = Math.Min(left, x); right = Math.Max(right, x);
                        top = Math.Min(top, y); bottom = Math.Max(bottom, y);
                    }
                bool palm = _profile.PalmWidthPx <= 0 || _profile.PalmHeightPx <= 0
                    || (right - left <= _profile.PalmWidthPx * Options.GroupTolerance
                        && bottom - top <= _profile.PalmHeightPx * Options.GroupTolerance);
                foreach (var c in group) { c.Group = palm; c.Decided = true; }
            }
        }

        public bool IsWriting(int id)
        {
            if (!_contacts.TryGetValue(id, out var c)) return false;
            return !(Options.MultiTouch && c.Group)
                && !(Options.AreaEnabled && _profile.ThresholdPx2 > 0 && c.Current.Width * c.Current.Height >= _profile.ThresholdPx2)
                && !(LockActive && c.Latched);
        }

        internal double EffectiveK
        {
            get
            {
                double contact = _profile.PalmContactAreaPx2 > 0 ? _profile.PalmContactAreaPx2 : _contacts.Values.Sum(c => c.PeakArea);
                double k = PalmArea > 0 && contact > 0 ? PalmArea / contact : 1;
                return (double.IsFinite(k) && k > 0 ? k : 1) * Math.Clamp(Options.AreaTrim, 0.5, 2);
            }
        }

        private double RawArea(Rect c)
        {
            double floor = PalmArea * Math.Clamp(Options.AreaTrim, 0.5, 2);
            double scaled = c.Width * c.Height * EffectiveK;
            double area = !Options.FollowSize ? floor : Options.Limit switch
            {
                PalmSizeLimit.Floor => Math.Max(floor, scaled),
                PalmSizeLimit.Cap => Math.Min(floor, scaled),
                _ => scaled
            };
            return area * PressureGain * PressureGain;
        }

        internal (double Width, double Height) Size(int id)
        {
            if (!_contacts.TryGetValue(id, out var c)) return default;
            double area = LockActive && c.LockedArea > 0 ? c.LockedArea : RawArea(c.Current);
            var size = Dimensions(area, c.Current);
            if (Options.Smooth && c.ShownWidth > 0 && c.ShownHeight > 0
                && Math.Abs(size.Width - c.ShownWidth) <= c.ShownWidth * 0.015
                && Math.Abs(size.Height - c.ShownHeight) <= c.ShownHeight * 0.015)
                return (c.ShownWidth, c.ShownHeight);
            c.ShownWidth = size.Width; c.ShownHeight = size.Height;
            return size;
        }

        internal (double Width, double Height) GroupSize() =>
            Dimensions(PalmArea * Math.Clamp(Options.AreaTrim, 0.5, 2) * PressureGain * PressureGain, null);

        private (double Width, double Height) Dimensions(double area, Rect? contact)
        {
            double trace = _profile.PalmHeightPx > 0 ? _profile.PalmWidthPx / _profile.PalmHeightPx : 0;
            double aspect = Options.Aspect == PalmAspectSource.Custom ? Options.CustomAspect
                : Options.Aspect == PalmAspectSource.Trace && trace > 0 ? trace
                : contact is Rect r ? r.Width / r.Height : trace;
            if (!(aspect > 0) || !double.IsFinite(aspect)) aspect = 1;
            double height = Math.Sqrt(area / aspect * (_profile.Ellipse ? 4 / Math.PI : 1));
            double width = height * aspect;
            return width > 0 && height > 0 && double.IsFinite(width) && double.IsFinite(height) ? (width, height) : default;
        }

        public bool TryGetEraser(int id, out PalmEraserFrame frame)
        {
            frame = default;
            if (!_contacts.TryGetValue(id, out var c) || IsWriting(id)) return false;
            bool group = Options.MultiTouch && c.Group;
            var size = group ? GroupSize() : Size(id);
            if (size.Width <= 0 || size.Height <= 0) return false;
            Point center = Center(c.Current);
            if (group)
            {
                int count = 0; double x = 0, y = 0;
                foreach (var pair in _contacts)
                    if (pair.Value.Group)
                    {
                        Point p = Center(pair.Value.Current);
                        x += p.X; y += p.Y; count++;
                        id = Math.Min(id, pair.Key);
                    }
                center = new Point(x / count, y / count);
            }
            frame = new(id, center, size.Width, size.Height, group, group ? _revision : 0);
            return true;
        }

        internal bool TryGetNearestEraser(Point positionPx, out PalmEraserFrame frame)
        {
            frame = default;
            double distance = double.MaxValue;
            foreach (int id in _contacts.Keys)
                if (TryGetEraser(id, out var candidate))
                {
                    double d = (candidate.CenterPx - positionPx).LengthSquared;
                    if (d < distance) { distance = d; frame = candidate; }
                }
            return distance < double.MaxValue;
        }

        private static Point Center(Rect rect) => new(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);

        internal static (double Width, double Height) HidDimensions(double width, double height,
            int widthMaximum, int heightMaximum, int pixelWidth, int pixelHeight, PalmHidScale scale)
        {
            if (widthMaximum <= 0 || heightMaximum <= 0 || pixelWidth <= 0 || pixelHeight <= 0) return default;
            double x = pixelWidth / (double)widthMaximum, y = pixelHeight / (double)heightMaximum;
            if (scale == PalmHidScale.Isotropic) x = y = Math.Sqrt(x * y);
            return (width * x, height * y);
        }
    }
}
