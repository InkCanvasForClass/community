using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace InkCanvasForClass.IccInkCanvas.Settings {
    public class BoardSettings {
        public BoardSettings() {}

        private double _NibWidth { get; set; } = 4.00;

        /// <summary>
        /// 笔尖长度
        /// </summary>
        public double NibWidth {
            get => _NibWidth;
            set {
                if (Math.Abs(_NibWidth - value) < 0.0001) return;
                _NibWidth = value;
                NibWidthChanged?.Invoke(this,EventArgs.Empty);
            }
        }

        private double _NibHeight { get; set; } = 4.00;

        /// <summary>
        /// 笔尖高度
        /// </summary>
        public double NibHeight { 
            get => _NibWidth;
            set {
                if (Math.Abs(_NibHeight - value) < 0.0001) return;
                _NibHeight = value;
                NibHeightChanged?.Invoke(this,EventArgs.Empty);
            }
        }

        private NibType _NibType { get; set; } = NibType.Default;

        public NibType NibType {
            get => _NibType;
            set {
                if (_NibType == value) return;
                _NibType = value;
                NibTypeChanged?.Invoke(this,EventArgs.Empty);
            }
        }

        private Color _NibColor { get; set; } = Colors.Black;

        /// <summary>
        /// 笔尖颜色
        /// </summary>
        public Color NibColor {
            get => _NibColor;
            set {
                if (_NibColor.Equals(value)) return;
                _NibColor = value;
                NibColorChanged?.Invoke(this,EventArgs.Empty);
            }
        }

        private StrokeNibStyle _StrokeNibStyle { get; set; } = StrokeNibStyle.Beautiful;

        /// <summary>
        /// 笔锋样式类型，默认有笔锋
        /// </summary>
        public StrokeNibStyle StrokeNibStyle {
            get => _StrokeNibStyle;
            set {
                if (_StrokeNibStyle == value) return;
                _StrokeNibStyle = value;
                StrokeNibStyleChanged?.Invoke(this,EventArgs.Empty);
            }
        }

        private bool _IsForceIgnoreStylusPressure { get; set; } = false;

        /// <summary>
        /// 强制忽略支持压力传感的输入设备返回的真实压力值（比如支持压感的手写笔）
        /// </summary>
        public bool IsForceIgnoreStylusPressure {
            get => _IsForceIgnoreStylusPressure;
            set {
                if (_IsForceIgnoreStylusPressure == value) return;
                _IsForceIgnoreStylusPressure = value;
                IsForceIgnoreStylusPressureChanged?.Invoke(this,EventArgs.Empty);
            }
        }

        private EraserType _EraserType { get; set; } = EraserType.Rectangle;

        /// <summary>
        /// 指定橡皮擦的形状，支持矩形和圆形
        /// </summary>
        public EraserType EraserType {
            get => _EraserType;
            set {
                if (_EraserType == value) return;
                _EraserType = value;
                EraserTypeChanged?.Invoke(this,EventArgs.Empty);
            }
        }

        private double _EraserSize { get; set; } = 32D;

        /// <summary>
        /// 指定橡皮擦的大小，矩形橡皮擦为宽度（高度自动确定），圆形橡皮擦为直径
        /// </summary>
        public double EraserSize {
            get => _EraserSize;
            set {
                if (_EraserSize == value) return;
                _EraserSize = value;
                EraserSizeChanged?.Invoke(this,EventArgs.Empty);
            }
        }


        #region Events

        public event EventHandler<EventArgs> NibWidthChanged;
        public event EventHandler<EventArgs> NibHeightChanged;
        public event EventHandler<EventArgs> NibColorChanged;
        public event EventHandler<EventArgs> NibTypeChanged;
        public event EventHandler<EventArgs> StrokeNibStyleChanged;
        public event EventHandler<EventArgs> IsForceIgnoreStylusPressureChanged;
        public event EventHandler<EventArgs> EraserTypeChanged;
        public event EventHandler<EventArgs> EraserSizeChanged;

        #endregion
    }
}
