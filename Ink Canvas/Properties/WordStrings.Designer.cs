using System.Globalization;
using System.Resources;
namespace Ink_Canvas.Properties
{
    public static class WordStrings
    {
        private static readonly ResourceManager Manager = new("Ink_Canvas.Properties.WordStrings", typeof(WordStrings).Assembly);
        public static string Get(string key) => Manager.GetString(key, CultureInfo.CurrentUICulture) ?? Manager.GetString("Unavailable", CultureInfo.CurrentUICulture);
        public static string Title => Get(nameof(Title));
        public static string Enable => Get(nameof(Enable));
        public static string EnableHint => Get(nameof(EnableHint));
        public static string ShowNavigation => Get(nameof(ShowNavigation));
        public static string ScopeHint => Get(nameof(ScopeHint));
        public static string StorageHint => Get(nameof(StorageHint));
        public static string Previous => Get(nameof(Previous));
        public static string Next => Get(nameof(Next));
        public static string ZoomOut => Get(nameof(ZoomOut));
        public static string ZoomIn => Get(nameof(ZoomIn));
        public static string DocumentZoom => Get(nameof(DocumentZoom));
        public static string NavigationAppearance => Get(nameof(NavigationAppearance));
        public static string Preview => Get(nameof(Preview));
        public static string UsePptAppearance => Get(nameof(UsePptAppearance));
        public static string UsePptAppearanceHint => Get(nameof(UsePptAppearanceHint));
        public static string ShowZoom => Get(nameof(ShowZoom));
        public static string FollowWindow => Get(nameof(FollowWindow));
        public static string FollowWindowHint => Get(nameof(FollowWindowHint));
        public static string ShowPageNumber => Get(nameof(ShowPageNumber));
        public static string DarkBackground => Get(nameof(DarkBackground));
        public static string NavigationScale => Get(nameof(NavigationScale));
        public static string NavigationOpacity => Get(nameof(NavigationOpacity));
        public static string NavigationShadow => Get(nameof(NavigationShadow));
        public static string ShadowSize => Get(nameof(ShadowSize));
        public static string HorizontalInset => Get(nameof(HorizontalInset));
        public static string BottomInset => Get(nameof(BottomInset));
        public static string ResetNavigation => Get(nameof(ResetNavigation));
        public static string NavigationLayout => Get(nameof(NavigationLayout));
        public static string CustomAppearance => Get(nameof(CustomAppearance));
        public static string FocusModeSettings => Get(nameof(FocusModeSettings));
        public static string AutoEnterFocusMode => Get(nameof(AutoEnterFocusMode));
        public static string AutoEnterFocusModeHint => Get(nameof(AutoEnterFocusModeHint));
        public static string FocusToolbarOpacity => Get(nameof(FocusToolbarOpacity));
        public static string ExitFocus => Get(nameof(ExitFocus));
        public static string MouseMode => Get(nameof(MouseMode));
        public static string Resume => Get(nameof(Resume));
        public static string PageFormat => Get(nameof(PageFormat));
        public static string Waiting => Get(nameof(Waiting));
        public static string Unavailable => Get(nameof(Unavailable));
        public static string Timeout => Get(nameof(Timeout));
        public static string Busy => Get(nameof(Busy));
        public static string HelperMissing => Get(nameof(HelperMissing));
        public static string HelperMismatch => Get(nameof(HelperMismatch));
        public static string PrintLayoutRequired => Get(nameof(PrintLayoutRequired));
        public static string Locating => Get(nameof(Locating));
        public static string Ready => Get(nameof(Ready));
        public static string Loading => Get(nameof(Loading));
        public static string LayoutChanged => Get(nameof(LayoutChanged));
        public static string LoadFailed => Get(nameof(LoadFailed));
        public static string SaveFailed => Get(nameof(SaveFailed));
    }
}
