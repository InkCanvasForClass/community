# Highlighter opacity and overlap

Windows Ink stores the alpha component of `InkDrawingAttributes.Color` but ignores it
when rendering. WPF's built-in highlighter renderer similarly forces opaque RGB and
applies a fixed 50% opacity to each same-colour group. Synchronizing ARGB properties
alone therefore does not implement the opacity slider.

`HighlighterRendering` defines the displayed opacity as `0.5 * alpha / 255`. The
default alpha of 255 retains the conventional 50% highlighter appearance. With overlap
disabled, strokes sharing the same ARGB are drawn as opaque geometries inside one
opacity group; crossings do not darken. With overlap enabled, each stroke has its own
opacity group and crossings darken, including at the default slider value.

`HighlighterInkCanvas` supplies these rendering-only groups to WPF's protected
`InkPresenter` using its public `Strokes` property. `InkCanvas.Strokes` continues to
contain the original stroke instances for selection, point erasing, history, plugins
and persistence. Normal stroke visuals keep their identities when groups change;
ordinary stroke invalidations do not rebuild highlighter groups. Group geometry is
refreshed after source invalidation, removal or page replacement. The page export path
uses the same rendering policy.

New highlighter drawing attributes carry an ISF-persisted tool marker so overlap mode
does not lose tool identity when `IsHighlighter` is false. Existing ISF highlighters
with `IsHighlighter=true` also use this renderer. Old overlap-enabled strokes without
the marker cannot be distinguished reliably from ordinary rectangular ink and are
left unchanged.

The native wet layer applies alpha using a DirectComposition effect on a separate
parent visual. Windows Ink retains ownership of its original root visual. The normal
native brush used for overlap mode receives the full `0.5 * alpha / 255` multiplier;
the native highlighter brush receives `alpha / 255` on top of its own translucency.
The effect update and native drawing attributes are submitted on the ink thread.

## Preview limitation

Native wet ink and WPF dry ink are separate composited layers. With overlap disabled,
wet ink crossing existing dry ink can temporarily darken while the pointer is down.
After the native batch commits and its wet visual is retired, the merged dry rendering
removes that darkening. This change does not claim to merge those two layers during
the stroke. Touch-screen and GPU composition behavior still require device testing.

## Verification

`build/PenToolTests` renders real WPF pixels at alpha 0, 64, 128 and 255, for both
overlap modes on white, black and coloured backgrounds. It verifies single-stroke and
crossing colours, ISF roundtrips, opacity coefficients for the native layer, the
integrated InkCanvas, attribute invalidation, selection identity, removal, page
replacement and point erasing. Native composition coefficients are tested; these
tests do not capture the actual DirectComposition output on a touch device.
