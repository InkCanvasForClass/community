using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Ink_Canvas.Resources.ICCConfiguration;
using Tomlyn;
using Tomlyn.Model;

namespace Ink_Canvas.Helpers {
    public static class ConfigurationHelper {
        public static ICCConfiguration ReadConfiguration() {
            try {
                if (File.Exists(App.RootPath + "icc.toml")) {
                    try {
                        string text = File.ReadAllText(App.RootPath + "icc.toml");
                        var tomlTable = Toml.ToModel(text);
                        var conf = new ICCConfiguration();

                        // FloatingBar
                        var fb = tomlTable["FloatingBar"] as TomlTable;
                        if (fb != null) {
                            if (fb["SemiTransparent"] is bool) conf.FloatingBar.SemiTransparent = (bool)fb["SemiTransparent"];
                            if (fb["NearSnap"] is bool) conf.FloatingBar.NearSnap = (bool)fb["NearSnap"];
                            InitialPositionTypes _InitialPositionType;
                            ElementCornerRadiusTypes _ElementCornerRadiusType;
                            if (fb["InitialPosition"] is TomlArray) {
                                var arr = ((TomlArray)fb["InitialPosition"]);
                                conf.FloatingBar.InitialPosition = InitialPositionTypes.Custom;
                                if ((arr[0] is double || arr[0] is long) &&
                                    (arr[1] is double || arr[1] is long))
                                    conf.FloatingBar.InitialPositionPoint = new Point(Math.Min(Math.Max(0,Convert.ToDouble(arr[0])),65535), 
                                        Math.Min(Math.Max(0,Convert.ToDouble(arr[1])),65535));
                            } else if (fb["InitialPosition"] is string &&
                                       Enum.TryParse<InitialPositionTypes>((string)fb["InitialPosition"],
                                           out _InitialPositionType)) {
                                conf.FloatingBar.InitialPosition = _InitialPositionType;
                            }
                            if (fb["ElementCornerRadius"] is double || fb["ElementCornerRadius"] is long) {
                                conf.FloatingBar.ElementCornerRadiusType = ElementCornerRadiusTypes.Custom;
                                conf.FloatingBar.ElementCornerRadiusValue = Math.Min(Math.Max(0, Convert.ToDouble(fb["ElementCornerRadius"])),24);
                            } else if (fb["ElementCornerRadius"] is string &&
                                       Enum.TryParse<ElementCornerRadiusTypes>((string)fb["ElementCornerRadius"],
                                           out _ElementCornerRadiusType)) {
                                conf.FloatingBar.ElementCornerRadiusType = _ElementCornerRadiusType;
                            }
                            if (fb["ParallaxEffect"] is bool) conf.FloatingBar.ParallaxEffect = (bool)fb["ParallaxEffect"];
                            if (fb["MiniMode"] is bool) conf.FloatingBar.MiniMode = (bool)fb["MiniMode"];
                            if (fb["ClearButtonColor"] is TomlArray) {
                                var arr = (TomlArray)fb["ClearButtonColor"];
                                conf.FloatingBar.ClearButtonColor = Color.FromRgb(Convert.ToByte(arr[0]), Convert.ToByte(arr[1]), Convert.ToByte(arr[2]));
                            }
                            if (fb["ClearButtonPressColor"] is TomlArray) {
                                var arr = (TomlArray)fb["ClearButtonPressColor"];
                                conf.FloatingBar.ClearButtonPressColor = Color.FromRgb(Convert.ToByte(arr[0]), Convert.ToByte(arr[1]), Convert.ToByte(arr[2]));
                            }
                            if (fb["ToolButtonSelectedBgColor"] is TomlArray) {
                                var arr = (TomlArray)fb["ToolButtonSelectedBgColor"];
                                conf.FloatingBar.ToolButtonSelectedBgColor = Color.FromRgb(Convert.ToByte(arr[0]), Convert.ToByte(arr[1]), Convert.ToByte(arr[2]));
                            }
                            if (fb["MovingLimitationNoSnap"] is long || fb["MovingLimitationNoSnap"] is double)
                                conf.FloatingBar.MovingLimitationNoSnap = Math.Min(Math.Max(0, Convert.ToDouble(fb["MovingLimitationNoSnap"])),32);
                            if (fb["MovingLimitationSnapped"] is long || fb["MovingLimitationSnapped"] is double)
                                conf.FloatingBar.MovingLimitationSnapped = Math.Min(Math.Max(0, Convert.ToDouble(fb["MovingLimitationSnapped"])),32);
                            if (fb["NearSnapAreaSize"] is TomlTable) {
                                var _tb = fb["NearSnapAreaSize"] as TomlTable;
                                if (_tb["TopLeft"] is TomlArray) {
                                    var _arr = _tb["TopLeft"] as TomlArray;
                                    conf.FloatingBar.NearSnapAreaSize.TopLeft = new []{
                                        Math.Min(Math.Max(0,Convert.ToDouble(_arr[0])),64), Math.Min(Math.Max(0,Convert.ToDouble(_arr[1])),64)
                                    };
                                }
                                if (_tb["TopRight"] is TomlArray) {
                                    var _arr = _tb["TopRight"] as TomlArray;
                                    conf.FloatingBar.NearSnapAreaSize.TopRight = new []{
                                        Math.Min(Math.Max(0,Convert.ToDouble(_arr[0])),64), Math.Min(Math.Max(0,Convert.ToDouble(_arr[1])),64)
                                    };
                                }
                                if (_tb["BottomLeft"] is TomlArray) {
                                    var _arr = _tb["BottomLeft"] as TomlArray;
                                    conf.FloatingBar.NearSnapAreaSize.BottomLeft = new []{
                                        Math.Min(Math.Max(0,Convert.ToDouble(_arr[0])),64), Math.Min(Math.Max(0,Convert.ToDouble(_arr[1])),64)
                                    };
                                }
                                if (_tb["BottomRight"] is TomlArray) {
                                    var _arr = _tb["BottomRight"] as TomlArray;
                                    conf.FloatingBar.NearSnapAreaSize.BottomRight = new []{
                                        Math.Min(Math.Max(0,Convert.ToDouble(_arr[0])),64), Math.Min(Math.Max(0,Convert.ToDouble(_arr[1])),64)
                                    };
                                }
                                if (_tb["TopCenter"] is long || _tb["TopCenter"] is double)
                                    conf.FloatingBar.NearSnapAreaSize.TopCenter = Math.Min(Math.Max(0, Convert.ToDouble(_tb["TopCenter"])), 64);
                                if (_tb["BottomCenter"] is long || _tb["BottomCenter"] is double)
                                    conf.FloatingBar.NearSnapAreaSize.BottomCenter = Math.Min(Math.Max(0, Convert.ToDouble(_tb["BottomCenter"])), 64);
                            }
                            if (fb["ToolBarItems"] is TomlTable) {
                                var _tb = fb["ToolBarItems"] as TomlTable;
                                if (_tb["CursorMode"] is TomlArray) {
                                    conf.FloatingBar.ToolBarItemsInCursorMode =
                                        Array.ConvertAll(((TomlArray)_tb["CursorMode"]).ToArray(),p=>p.ToString());
                                }
                                if (_tb["MiniMode"] is TomlArray) {
                                    conf.FloatingBar.ToolBarItemsInMiniMode =
                                        Array.ConvertAll(((TomlArray)_tb["MiniMode"]).ToArray(),p=>p.ToString());
                                }
                                if (_tb["AnnotationMode"] is TomlArray) {
                                    conf.FloatingBar.ToolBarItemsInAnnotationMode =
                                        Array.ConvertAll(((TomlArray)_tb["AnnotationMode"]).ToArray(),p=>p.ToString());
                                }
                            }
                        }

                        return conf;
                    }
                    catch {
                        return new ICCConfiguration();
                    }
                } else {
                    return new ICCConfiguration();
                }
            }
            catch (Exception ex) {
                LogHelper.WriteLogToFile(ex.ToString(), LogHelper.LogType.Error);
                return new ICCConfiguration();
            }
        }
    }
}
