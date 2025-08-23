using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using Windows.UI.ApplicationSettings;

namespace Ink_Canvas.Windows.SettingsViews {
    public partial class AppearancePanel : UserControl {
        public AppearancePanel() {
            InitializeComponent();
            BaseView.SettingsPanels.Add(new SettingsViewPanel() {
                Title = "新版设置测试",
                Items = new ObservableCollection<SettingsItem>(new SettingsItem[] {
                    new SettingsItem() {
                        Title = "默认ToggleSwitch",
                        Description = "这是测试文本，这是测试文本",
                        Type = SettingsItemType.SingleToggleSwtich,
                        IsSeparatorVisible = true
                    },
                    new SettingsItem() {
                        Title = "默认开启的ToggleSwitch",
                        Description = "这是测试文本，这是测试文本324234324",
                        Type = SettingsItemType.SingleToggleSwtich,
                        IsSeparatorVisible = true,
                        ToggleSwitchToggled = true,
                    },
                    new SettingsItem() {
                        Title = "默认关闭的ToggleSwitch",
                        Description = "这是测试文本，这是测试文本fsdsdffsd",
                        Type = SettingsItemType.SingleToggleSwtich,
                        IsSeparatorVisible = true,
                        ToggleSwitchToggled = false,
                    },
                    new SettingsItem() {
                        Title = "绿色的ToggleSwitch",
                        Description = "这是测试文本，这是测试文本fs大风刮过4sd",
                        Type = SettingsItemType.SingleToggleSwtich,
                        IsSeparatorVisible = true,
                        ToggleSwitchToggled = true,
                        ToggleSwitchBackground = new SolidColorBrush(Color.FromRgb(51, 209, 122)),
                    },
                    new SettingsItem() {
                        Title = "默认禁用的的ToggleSwitch",
                        Description = "这是测试文本",
                        Type = SettingsItemType.SingleToggleSwtich,
                        IsSeparatorVisible = true,
                        ToggleSwitchToggled = true,
                        ToggleSwitchEnabled = false,
                        ToggleSwitchBackground = new SolidColorBrush(Color.FromRgb(51, 209, 122)),
                    },
                    new SettingsItem() {
                        Title = "控制上面的ToggleSwitch是否启用",
                        Description = "12423432452312322335",
                        Type = SettingsItemType.SingleToggleSwtich,
                        IsSeparatorVisible = true,
                        ToggleSwitchToggled = false,
                    },
                })
            });
            BaseView.SettingsPanels[0].Items[5].OnToggleSwitchToggled += (sender, args) => {
                var item = (SettingsItem)sender;
                BaseView.SettingsPanels[0].Items[4].ToggleSwitchEnabled = item.ToggleSwitchToggled;
            };
        }
    }
}
