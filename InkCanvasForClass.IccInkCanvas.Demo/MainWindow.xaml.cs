using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using InkCanvasForClass.IccInkCanvas.Settings;

namespace InkCanvasForClass.IccInkCanvas.Demo {
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window {
        private TimeMachineV2 tm = new TimeMachineV2();

        public MainWindow() {
            InitializeComponent();
            RegisterEventsForBoardSettings();
            UpdateBoardSettingsTextBlock();

            IccBoard.CurrentPageChanged += (sender, args) => {
                Trace.WriteLine("Page Counter Updated");
                BoardPageTextBlock.Text = $"{IccBoard.CurrentPage+1}/{IccBoard.PagesCount}  IsLast:{IccBoard.IsCurrentLastPage}  IsFirst:{IccBoard.IsCurrentFirstPage}";
            };
            IccBoard.UndoRedoStateChanged += (sender, args) => {
                TimeMachineTextBlock.Text = $"CurrentPageHistoriesCount:{IccBoard.CurrentPageHistoriesCount}   CanUndo:{IccBoard.CanUndo}   CanRedo:{IccBoard.CanRedo}";
            };

            TimeMachineV2Status.Text = $"Current: {tm.CurrentIndex}   Count: {tm.ActionsCount}";
            tm.UndoRedoStateChanged += (_, __) => {
                TimeMachineV2Status.Text = $"Current: {tm.CurrentIndex}   Count: {tm.ActionsCount}";
            };
        }

        private void UpdateBoardSettingsTextBlock() {
            BoardSettingsTextBlock.Text =
                $@"Width:{IccBoard.BoardSettings.NibWidth}  Height:{IccBoard.BoardSettings.NibHeight}  Color:{IccBoard.BoardSettings.NibColor.ToString()}";
        }

        private void RegisterEventsForBoardSettings() {
            IccBoard.BoardSettings.NibWidthChanged += (s, e) => UpdateBoardSettingsTextBlock();
            IccBoard.BoardSettings.NibHeightChanged += (s, e) => UpdateBoardSettingsTextBlock();
            IccBoard.BoardSettings.NibColorChanged += (s, e) => UpdateBoardSettingsTextBlock();
        }

        private void EditingModeChangeToNone_ButtonClick(object sender, RoutedEventArgs e) {
            IccBoard.EditingMode = EditingMode.NoneWithHitTest;
        }
        private void EditingModeChangeToWriting_ButtonClick(object sender, RoutedEventArgs e) {
            IccBoard.EditingMode = EditingMode.Writing;
        }
        private void EditingModeChangeToGeometryErasing_ButtonClick(object sender, RoutedEventArgs e) {
            IccBoard.EditingMode = EditingMode.GeometryErasing;
        }
        private void EditingModeChangeToAreaErasing_ButtonClick(object sender, RoutedEventArgs e) {
            IccBoard.EditingMode = EditingMode.AreaErasing;
        }
        private void EditingModeChangeToRoamingMode_ButtonClick(object sender, RoutedEventArgs e) {
            IccBoard.EditingMode = EditingMode.RoamingMode;
        }
        private void BoardSettingsNibSizeChangeTo2_ButtonClick(object sender, RoutedEventArgs e) {
            IccBoard.BoardSettings.NibWidth = 2;
            IccBoard.BoardSettings.NibHeight = 2;
        }
        private void BoardSettingsNibSizeChangeTo4_ButtonClick(object sender, RoutedEventArgs e) {
            IccBoard.BoardSettings.NibWidth = 4;
            IccBoard.BoardSettings.NibHeight = 4;
        }
        private void BoardSettingsNibSizeChangeTo6_ButtonClick(object sender, RoutedEventArgs e) {
            IccBoard.BoardSettings.NibWidth = 6;
            IccBoard.BoardSettings.NibHeight = 6;
        }
        private void BoardSettingsNibColorChangeToRed_ButtonClick(object sender, RoutedEventArgs e) {
            IccBoard.BoardSettings.NibColor = Color.FromRgb(220, 38, 38);
        }
        private void BoardSettingsNibColorChangeToBlue_ButtonClick(object sender, RoutedEventArgs e) {
            IccBoard.BoardSettings.NibColor = Color.FromRgb(37, 99, 235);
        }
        private void BoardSettingsNibColorChangeToGreen_ButtonClick(object sender, RoutedEventArgs e) {
            IccBoard.BoardSettings.NibColor = Color.FromRgb(34, 197, 94);
        }
        private void BoardSettingsNibColorChangeToBlack_ButtonClick(object sender, RoutedEventArgs e) {
            IccBoard.BoardSettings.NibColor = Colors.Black;
        }
        private void BoardSettingsStrokeNibStyleChangeToSolid_ButtonClick(object sender, RoutedEventArgs e) {
            IccBoard.BoardSettings.StrokeNibStyle = StrokeNibStyle.Solid;
        }
        private void BoardSettingsStrokeNibStyleChangeToBeautiful_ButtonClick(object sender, RoutedEventArgs e) {
            IccBoard.BoardSettings.StrokeNibStyle = StrokeNibStyle.Beautiful;
        }
        private void AddPage_ButtonClick(object sender, RoutedEventArgs e) {
            IccBoard.AddPage();
        }
        private void SwitchToPreviousPage_ButtonClick(object sender, RoutedEventArgs e) {
            IccBoard.GoToPreviousPage();
        }
        private void SwitchToNextPage_ButtonClick(object sender, RoutedEventArgs e) {
            IccBoard.GoToNextPage();
        }
        private async void RemoveCurrentPage_ButtonClick(object sender, RoutedEventArgs e) {
            await IccBoard.RemovePage();
        }
        private void BoardSettingsEraserSizeChangeTo16_ButtonClick(object sender, RoutedEventArgs e) {
            IccBoard.BoardSettings.EraserSize = 16;
        }
        private void BoardSettingsEraserSizeChangeTo32_ButtonClick(object sender, RoutedEventArgs e) {
            IccBoard.BoardSettings.EraserSize = 32;
        }
        private void BoardSettingsEraserSizeChangeTo48_ButtonClick(object sender, RoutedEventArgs e) {
            IccBoard.BoardSettings.EraserSize = 48;
        }
        private void BoardSettingsEraserSizeChangeTo64_ButtonClick(object sender, RoutedEventArgs e) {
            IccBoard.BoardSettings.EraserSize = 64;
        }
        private void BoardSettingsEraserTypeChangeToRectangle_ButtonClick(object sender, RoutedEventArgs e) {
            IccBoard.BoardSettings.EraserType = EraserType.Rectangle;
        }
        private void BoardSettingsEraserTypeChangeToEllipse_ButtonClick(object sender, RoutedEventArgs e) {
            IccBoard.BoardSettings.EraserType = EraserType.Ellipse;
        }

        private void Undo_ButtonClick(object sender, RoutedEventArgs e) {
            IccBoard.Undo();
        }
        private void Redo_ButtonClick(object sender, RoutedEventArgs e) {
            IccBoard.Redo();
        }
        private void Undo2_ButtonClick(object sender, RoutedEventArgs e) {
            IccBoard.Undo(2);
        }
        private void Redo2_ButtonClick(object sender, RoutedEventArgs e) {
            IccBoard.Redo(2);
        }

        private void PushTestAction_TimeMachineV2_ButtonClick(object sender, RoutedEventArgs e) {
            tm.PushTestAction();
        }

        private void Undo_TimeMachineV2_ButtonClick(object sender, RoutedEventArgs e) {
            tm.Undo(false);
        }
        private void Redo_TimeMachineV2_ButtonClick(object sender, RoutedEventArgs e) {
            tm.Redo();
        }
    }
}
