using Ink_Canvas.Helpers;
using System.Collections.Generic;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Diagnostics;
using System.Windows.Media.Imaging;

namespace Ink_Canvas {
    public partial class MainWindow : PerformanceTransparentWin {
        private enum CommitReason {
            UserInput,
            CodeInput,
            ShapeDrawing,
            ShapeRecognition,
            ClearingCanvas,
            Manipulation
        }

        private CommitReason _currentCommitType = CommitReason.UserInput;
        private bool IsEraseByPoint => SelectedMode == ICCToolsEnum.EraseByGeometryMode;
        private StrokeCollection ReplacedStroke;
        private StrokeCollection AddedStroke;
        private StrokeCollection CuboidStrokeCollection;
        private Dictionary<Stroke, Tuple<StylusPointCollection, StylusPointCollection>> StrokeManipulationHistory;

        private Dictionary<Stroke, StylusPointCollection> StrokeInitialHistory =
            new Dictionary<Stroke, StylusPointCollection>();

        private Dictionary<Stroke, Tuple<DrawingAttributes, DrawingAttributes>> DrawingAttributesHistory =
            new Dictionary<Stroke, Tuple<DrawingAttributes, DrawingAttributes>>();

        private Dictionary<Guid, List<Stroke>> DrawingAttributesHistoryFlag = new Dictionary<Guid, List<Stroke>>() {
            { DrawingAttributeIds.Color, new List<Stroke>() },
            { DrawingAttributeIds.DrawingFlags, new List<Stroke>() },
            { DrawingAttributeIds.IsHighlighter, new List<Stroke>() },
            { DrawingAttributeIds.StylusHeight, new List<Stroke>() },
            { DrawingAttributeIds.StylusTip, new List<Stroke>() },
            { DrawingAttributeIds.StylusTipTransform, new List<Stroke>() },
            { DrawingAttributeIds.StylusWidth, new List<Stroke>() }
        };

        private TimeMachine timeMachine = new TimeMachine();

        private void ApplyHistoryToCanvas(TimeMachineHistory item, IccInkCanvas applyCanvas = null) {
            _currentCommitType = CommitReason.CodeInput;
            var canvas = inkCanvas;
            if (applyCanvas != null && applyCanvas is IccInkCanvas) {
                canvas = applyCanvas;
            }

            if (item.CommitType == TimeMachineHistoryType.UserInput) {
                if (!item.StrokeHasBeenCleared) {
                    foreach (var strokes in item.CurrentStroke)
                        if (!canvas.Strokes.Contains(strokes))
                            canvas.Strokes.Add(strokes);
                } else {
                    foreach (var strokes in item.CurrentStroke)
                        if (canvas.Strokes.Contains(strokes))
                            canvas.Strokes.Remove(strokes);
                }
            } else if (item.CommitType == TimeMachineHistoryType.ShapeRecognition) {
                if (item.StrokeHasBeenCleared) {
                    foreach (var strokes in item.CurrentStroke)
                        if (canvas.Strokes.Contains(strokes))
                            canvas.Strokes.Remove(strokes);

                    foreach (var strokes in item.ReplacedStroke)
                        if (!canvas.Strokes.Contains(strokes))
                            canvas.Strokes.Add(strokes);
                } else {
                    foreach (var strokes in item.CurrentStroke)
                        if (!canvas.Strokes.Contains(strokes))
                            canvas.Strokes.Add(strokes);

                    foreach (var strokes in item.ReplacedStroke)
                        if (canvas.Strokes.Contains(strokes))
                            canvas.Strokes.Remove(strokes);
                }
            } else if (item.CommitType == TimeMachineHistoryType.Manipulation) {
                if (!item.StrokeHasBeenCleared) {
                    foreach (var currentStroke in item.StylusPointDictionary) {
                        if (canvas.Strokes.Contains(currentStroke.Key)) {
                            currentStroke.Key.StylusPoints = currentStroke.Value.Item2;
                        }
                    }
                } else {
                    foreach (var currentStroke in item.StylusPointDictionary) {
                        if (canvas.Strokes.Contains(currentStroke.Key)) {
                            currentStroke.Key.StylusPoints = currentStroke.Value.Item1;
                        }
                    }
                }
            } else if (item.CommitType == TimeMachineHistoryType.DrawingAttributes) {
                if (!item.StrokeHasBeenCleared) {
                    foreach (var currentStroke in item.DrawingAttributes) {
                        if (canvas.Strokes.Contains(currentStroke.Key)) {
                            currentStroke.Key.DrawingAttributes = currentStroke.Value.Item2;
                        }
                    }
                } else {
                    foreach (var currentStroke in item.DrawingAttributes) {
                        if (canvas.Strokes.Contains(currentStroke.Key)) {
                            currentStroke.Key.DrawingAttributes = currentStroke.Value.Item1;
                        }
                    }
                }
            } else if (item.CommitType == TimeMachineHistoryType.Clear) {
                if (!item.StrokeHasBeenCleared) {
                    if (item.CurrentStroke != null)
                        foreach (var currentStroke in item.CurrentStroke)
                            if (!canvas.Strokes.Contains(currentStroke))
                                canvas.Strokes.Add(currentStroke);

                    if (item.ReplacedStroke != null)
                        foreach (var replacedStroke in item.ReplacedStroke)
                            if (canvas.Strokes.Contains(replacedStroke))
                                canvas.Strokes.Remove(replacedStroke);
                } else {
                    if (item.ReplacedStroke != null)
                        foreach (var replacedStroke in item.ReplacedStroke)
                            if (!canvas.Strokes.Contains(replacedStroke))
                                canvas.Strokes.Add(replacedStroke);

                    if (item.CurrentStroke != null)
                        foreach (var currentStroke in item.CurrentStroke)
                            if (canvas.Strokes.Contains(currentStroke))
                                canvas.Strokes.Remove(currentStroke);
                }
            }

            _currentCommitType = CommitReason.UserInput;
        }

        private StrokeCollection ApplyHistoriesToNewStrokeCollection(TimeMachineHistory[] items) {
            IccInkCanvas fakeInkCanv = new IccInkCanvas() {
                Width = inkCanvas.ActualWidth,
                Height = inkCanvas.ActualHeight,
                EditingMode = InkCanvasEditingMode.None,
            };

            if (items != null && items.Length > 0) {
                foreach (var timeMachineHistory in items) {
                    ApplyHistoryToCanvas(timeMachineHistory, fakeInkCanv);
                }
            }

            return fakeInkCanv.Strokes;
        }

        private void TimeMachine_OnUndoStateChanged(bool status) {
            SymbolIconUndo.IsEnabled = status;
        }

        private void TimeMachine_OnRedoStateChanged(bool status) {
            SymbolIconRedo.IsEnabled = status;
        }

        private bool _mouseGesturingPrevious = false;

        private void StrokesOnStrokesChanged(object sender, StrokeCollectionChangedEventArgs e) {
            if (!isHidingSubPanelsWhenInking) {
                isHidingSubPanelsWhenInking = true;
                HideSubPanels(); // 书写时自动隐藏二级菜单
            }


            foreach (var stroke in e?.Removed) {
                stroke.StylusPointsChanged -= Stroke_StylusPointsChanged;
                stroke.StylusPointsReplaced -= Stroke_StylusPointsReplaced;
                stroke.DrawingAttributesChanged -= Stroke_DrawingAttributesChanged;
                StrokeInitialHistory.Remove(stroke);
            }

            foreach (var stroke in e?.Added) {
                stroke.StylusPointsChanged += Stroke_StylusPointsChanged;
                stroke.StylusPointsReplaced += Stroke_StylusPointsReplaced;
                stroke.DrawingAttributesChanged += Stroke_DrawingAttributesChanged;
                StrokeInitialHistory[stroke] = stroke.StylusPoints.Clone();
            }

            if (_currentCommitType == CommitReason.CodeInput || _currentCommitType == CommitReason.ShapeDrawing) return;

            if ((e.Added.Count != 0 || e.Removed.Count != 0) && IsEraseByPoint) {
                if (AddedStroke == null) AddedStroke = new StrokeCollection();
                if (ReplacedStroke == null) ReplacedStroke = new StrokeCollection();
                AddedStroke.Add(e.Added);
                ReplacedStroke.Add(e.Removed);
                return;
            }

            if (e.Added.Count != 0) {
                if (_currentCommitType == CommitReason.ShapeRecognition) {
                    timeMachine.CommitStrokeShapeHistory(ReplacedStroke, e.Added);
                    ReplacedStroke = null;
                    return;
                } else {
                    timeMachine.CommitStrokeUserInputHistory(e.Added);
                    return;
                }
            }

            if (e.Removed.Count != 0) {
                if (_currentCommitType == CommitReason.ShapeRecognition) {
                    ReplacedStroke = e.Removed;
                    return;
                } else if (!IsEraseByPoint || _currentCommitType == CommitReason.ClearingCanvas) {
                    timeMachine.CommitStrokeEraseHistory(e.Removed);
                    return;
                }
            }
        }

        private void Stroke_DrawingAttributesChanged(object sender, PropertyDataChangedEventArgs e) {
            var key = sender as Stroke;
            var currentValue = key.DrawingAttributes.Clone();
            DrawingAttributesHistory.TryGetValue(key, out var previousTuple);
            var previousValue = previousTuple?.Item1 ?? currentValue.Clone();
            var needUpdateValue = !DrawingAttributesHistoryFlag[e.PropertyGuid].Contains(key);
            if (needUpdateValue) {
                DrawingAttributesHistoryFlag[e.PropertyGuid].Add(key);
                Debug.Write(e.PreviousValue.ToString());
            }

            if (e.PropertyGuid == DrawingAttributeIds.Color && needUpdateValue) {
                previousValue.Color = (Color)e.PreviousValue;
            }

            if (e.PropertyGuid == DrawingAttributeIds.IsHighlighter && needUpdateValue) {
                previousValue.IsHighlighter = (bool)e.PreviousValue;
            }

            if (e.PropertyGuid == DrawingAttributeIds.StylusHeight && needUpdateValue) {
                previousValue.Height = (double)e.PreviousValue;
            }

            if (e.PropertyGuid == DrawingAttributeIds.StylusWidth && needUpdateValue) {
                previousValue.Width = (double)e.PreviousValue;
            }

            if (e.PropertyGuid == DrawingAttributeIds.StylusTip && needUpdateValue) {
                previousValue.StylusTip = (StylusTip)e.PreviousValue;
            }

            if (e.PropertyGuid == DrawingAttributeIds.StylusTipTransform && needUpdateValue) {
                previousValue.StylusTipTransform = (Matrix)e.PreviousValue;
            }

            if (e.PropertyGuid == DrawingAttributeIds.DrawingFlags && needUpdateValue) {
                previousValue.IgnorePressure = (bool)e.PreviousValue;
            }

            DrawingAttributesHistory[key] =
                new Tuple<DrawingAttributes, DrawingAttributes>(previousValue, currentValue);
        }

        private void Stroke_StylusPointsReplaced(object sender, StylusPointsReplacedEventArgs e) {
            if (isMouseGesturing) return;
            StrokeInitialHistory[sender as Stroke] = e.NewStylusPoints.Clone();
        }

        private void Stroke_StylusPointsChanged(object sender, EventArgs e) {
            if (isMouseGesturing) return;
            var selectedStrokes = inkCanvas.GetSelectedStrokes();
            var count = selectedStrokes.Count;
            if (count == 0) count = inkCanvas.Strokes.Count;
            if (StrokeManipulationHistory == null) {
                StrokeManipulationHistory =
                    new Dictionary<Stroke, Tuple<StylusPointCollection, StylusPointCollection>>();
            }

            StrokeManipulationHistory[sender as Stroke] =
                new Tuple<StylusPointCollection, StylusPointCollection>(StrokeInitialHistory[sender as Stroke],
                    (sender as Stroke).StylusPoints.Clone());
            if ((StrokeManipulationHistory.Count == count || sender == null) && dec.Count == 0) {
                timeMachine.CommitStrokeManipulationHistory(StrokeManipulationHistory);
                foreach (var item in StrokeManipulationHistory) {
                    StrokeInitialHistory[item.Key] = item.Value.Item2;
                }

                StrokeManipulationHistory = null;
            }
        }
    }
}