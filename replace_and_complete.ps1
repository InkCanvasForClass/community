$ErrorActionPreference = "Stop"
$projectDir = "c:\Users\wdsyzx\Documents\GitHub\community\Ink Canvas"
$propertiesDir = Join-Path $projectDir "Properties"

Write-Host "=== Step 1: Build reverse mapping ==="

$keyMappingJson = [System.IO.File]::ReadAllText(
    (Join-Path $propertiesDir "key_mapping.json"),
    [System.Text.Encoding]::UTF8)
$keyMapping = ConvertFrom-Json $keyMappingJson

[xml]$oldResx = [System.IO.File]::ReadAllText(
    (Join-Path $propertiesDir "Strings.resx"),
    [System.Text.Encoding]::UTF8)

$oldKeyToValue = @{}
foreach ($data in $oldResx.root.data) {
    $oldKeyToValue[$data.name] = $data.value
}

$valueToEntries = @{}
$allEntries = @{}

foreach ($prop in $keyMapping.PSObject.Properties) {
    $oldKey = $prop.Name
    $newKey = $prop.Value.NewKey
    $group = $prop.Value.Group
    $chineseValue = $oldKeyToValue[$oldKey]
    if ($chineseValue -and $chineseValue.Trim()) {
        $entry = @{ OldKey = $oldKey; NewKey = $newKey; Group = $group; Value = $chineseValue }
        if (-not $valueToEntries.ContainsKey($chineseValue)) {
            $valueToEntries[$chineseValue] = [System.Collections.ArrayList]::new()
        }
        [void]$valueToEntries[$chineseValue].Add($entry)
        $allEntries[$oldKey] = $entry
    }
}

$crashWindowOldKeys = @("CrashWindowTitle","CrashWindowHeader","CrashWindowDescription","CrashWindowFooter","CrashWindowCopy","CrashWindowClose","CrashWindowNoDetails")
foreach ($ck in $crashWindowOldKeys) {
    $val = $oldKeyToValue[$ck]
    if ($val) {
        $entry = @{ OldKey = $ck; NewKey = $ck; Group = "CrashStrings"; Value = $val }
        if (-not $valueToEntries.ContainsKey($val)) {
            $valueToEntries[$val] = [System.Collections.ArrayList]::new()
        }
        [void]$valueToEntries[$val].Add($entry)
        $allEntries[$ck] = $entry
    }
}

Write-Host "  Total old keys mapped: $($allEntries.Count)"

$fileGroupPrefs = @{}
$fileGroupPrefs["CanvasPage"] = @("CanvasStrings", "CommonStrings")
$fileGroupPrefs["PowerPointPage"] = @("PPTStrings", "CommonStrings")
$fileGroupPrefs["StartupPage"] = @("StartupStrings", "CommonStrings")
$fileGroupPrefs["AboutPage"] = @("AboutStrings", "CommonStrings")
$fileGroupPrefs["AdvancedPage"] = @("AdvancedStrings", "CommonStrings")
$fileGroupPrefs["CrashActionPage"] = @("CrashStrings", "CommonStrings")
$fileGroupPrefs["GesturePage"] = @("GestureStrings", "CommonStrings")
$fileGroupPrefs["AutomationPage"] = @("AutomationStrings", "CommonStrings")
$fileGroupPrefs["RandomWindowPage"] = @("RandomStrings", "CommonStrings")
$fileGroupPrefs["ThemePage"] = @("ThemeStrings", "CommonStrings")
$fileGroupPrefs["SecurityPage"] = @("SecurityStrings", "CommonStrings")
$fileGroupPrefs["NotificationPage"] = @("NotificationStrings", "CommonStrings")
$fileGroupPrefs["FloatingBarPage"] = @("FloatingBarStrings", "CommonStrings")
$fileGroupPrefs["StoragePage"] = @("StorageStrings", "CommonStrings")
$fileGroupPrefs["UpdatePage"] = @("UpdateStrings", "CommonStrings")
$fileGroupPrefs["UpdateCenterPanel"] = @("UpdateCenterPanelStrings", "CommonStrings")
$fileGroupPrefs["AnnouncementCenterPage"] = @("AnnouncementStrings", "CommonStrings")
$fileGroupPrefs["FriendlyLinksPage"] = @("FriendlyLinksStrings", "CommonStrings")
$fileGroupPrefs["TimerWindow"] = @("TimerStrings", "CommonStrings")
$fileGroupPrefs["BoothWindow"] = @("BoothStrings", "CommonStrings")
$fileGroupPrefs["CrashWindow"] = @("CrashStrings", "CommonStrings")
$fileGroupPrefs["MainWindow"] = @("WindowStrings", "FloatingBarStrings", "CommonStrings")
$fileGroupPrefs["App"] = @("CommonStrings", "WindowStrings")

function GetBestEntry([string]$chineseValue, [string]$fileBaseName) {
    if (-not $valueToEntries.ContainsKey($chineseValue)) { return $null }
    $entries = $valueToEntries[$chineseValue]
    if ($entries.Count -eq 1) { return $entries[0] }
    $prefs = $fileGroupPrefs[$fileBaseName]
    if ($prefs) {
        foreach ($pref in $prefs) {
            $match = $entries | Where-Object { $_.Group -eq $pref } | Select-Object -First 1
            if ($match) { return $match }
        }
    }
    return $entries[0]
}

function SanitizeKey([string]$key) {
    $sb = [System.Text.StringBuilder]::new()
    foreach ($c in $key.ToCharArray()) {
        if ([char]::IsLetterOrDigit($c) -or $c -eq '_') {
            [void]$sb.Append($c)
        } else {
            [void]$sb.Append('_')
        }
    }
    $result = $sb.ToString()
    if ($result.Length -gt 0 -and [char]::IsDigit($result[0])) {
        $result = "_" + $result
    }
    return $result
}

Write-Host ""
Write-Host "=== Step 2: Replace hardcoded Chinese in XAML files ==="

$xamlFiles = Get-ChildItem -Path $projectDir -Filter "*.xaml" -Recurse |
    Where-Object { $_.FullName -notmatch '\\bin\\' -and $_.FullName -notmatch '\\obj\\' }

$xamlReplaced = 0

$i18nAttrs = @("Content","Header","Description","Title","Text","ToolTip","Label",
               "PlaceholderText","Watermark","Tag")

foreach ($xamlFile in $xamlFiles) {
    $fileBaseName = [System.IO.Path]::GetFileNameWithoutExtension($xamlFile.Name)
    $content = [System.IO.File]::ReadAllText($xamlFile.FullName, [System.Text.Encoding]::UTF8)
    $originalContent = $content
    $fileReplaced = 0

    foreach ($attr in $i18nAttrs) {
        $attrPattern = [regex]::Escape($attr) + '="([^"]*[\u4e00-\u9fff][^"]*)"'
        $regex = [regex]::new($attrPattern)
        $matchResult = $regex.Match($content)
        while ($matchResult.Success) {
            $fullMatch = $matchResult.Value
            $chineseValue = $matchResult.Groups[1].Value
            $entry = GetBestEntry $chineseValue $fileBaseName
            if ($entry) {
                $sanKey = SanitizeKey $entry.NewKey
                $newAttr = $attr + '="{x:Static props:' + $entry.Group + '.' + $sanKey + '}"'
                $content = $content.Substring(0, $matchResult.Index) + $newAttr + $content.Substring($matchResult.Index + $matchResult.Length)
                $xamlReplaced++
                $fileReplaced++
                $matchResult = $regex.Match($content, $matchResult.Index + $newAttr.Length)
            } else {
                $matchResult = $matchResult.NextMatch()
            }
        }
    }

    if ($content -ne $originalContent) {
        if (-not ($content -match 'xmlns:props=')) {
            $firstElemMatch = [regex]::Match($content, '(<\w[\w:.]*)[\s\n]')
            if ($firstElemMatch.Success) {
                $insertPos = $firstElemMatch.Index + $firstElemMatch.Groups[1].Length
                $xmlns = " xmlns:props=`"clr-namespace:Ink_Canvas.Properties`""
                $content = $content.Substring(0, $insertPos) + $xmlns + $content.Substring($insertPos)
            }
        }
        $utf8NoBom = [System.Text.UTF8Encoding]::new($false)
        [System.IO.File]::WriteAllText($xamlFile.FullName, $content, $utf8NoBom)
        Write-Host "  Updated: $($xamlFile.Name) ($fileReplaced replacements)"
    }
}

Write-Host "  XAML total replacements: $xamlReplaced"

Write-Host ""
Write-Host "=== Step 3: Replace hardcoded Chinese in C# files ==="

$csFiles = Get-ChildItem -Path $projectDir -Filter "*.cs" -Recurse |
    Where-Object { $_.FullName -notmatch '\\bin\\' -and $_.FullName -notmatch '\\obj\\' -and
                   $_.Name -notmatch 'Strings\.Designer\.cs$' -and
                   $_.Name -notmatch 'Strings\.cs$' }

$csReplaced = 0

$csPattern = [regex]::new('"([^"]*[\u4e00-\u9fff][^"]*)"')

foreach ($csFile in $csFiles) {
    $fileBaseName = [System.IO.Path]::GetFileNameWithoutExtension($csFile.Name)
    $content = [System.IO.File]::ReadAllText($csFile.FullName, [System.Text.Encoding]::UTF8)
    $originalContent = $content
    $fileReplaced = 0

    $matchResult = $csPattern.Match($content)
    while ($matchResult.Success) {
        $chineseValue = $matchResult.Groups[1].Value
        $entry = GetBestEntry $chineseValue $fileBaseName
        if ($entry) {
            $sanKey = SanitizeKey $entry.NewKey
            $replacement = $entry.Group + "." + $sanKey
            $fullMatch = $matchResult.Value
            $content = $content.Substring(0, $matchResult.Index) + $replacement + $content.Substring($matchResult.Index + $fullMatch.Length)
            $csReplaced++
            $fileReplaced++
            $matchResult = $csPattern.Match($content, $matchResult.Index + $replacement.Length)
        } else {
            $matchResult = $matchResult.NextMatch()
        }
    }

    if ($content -ne $originalContent) {
        if (-not ($content -match 'using Ink_Canvas\.Properties;')) {
            $content = "using Ink_Canvas.Properties;`r`n" + $content
        }
        $utf8NoBom = [System.Text.UTF8Encoding]::new($false)
        [System.IO.File]::WriteAllText($csFile.FullName, $content, $utf8NoBom)
        Write-Host "  Updated: $($csFile.Name) ($fileReplaced replacements)"
    }
}

Write-Host "  CSharp total replacements: $csReplaced"

Write-Host ""
Write-Host "=== Step 4: Create new centralized Strings class ==="

$allGroupNames = @()
foreach ($entry in $allEntries.Values) {
    if ($allGroupNames -notcontains $entry.Group) { $allGroupNames += $entry.Group }
}
$allGroupNames = $allGroupNames | Sort-Object

$keyLookupLines = [System.Collections.Generic.List[string]]::new()
$seenKeys = @{}
foreach ($entry in $allEntries.Values) {
    $sanKey = SanitizeKey $entry.NewKey
    $dictKey = $entry.OldKey
    if (-not $seenKeys.ContainsKey($dictKey)) {
        $seenKeys[$dictKey] = $true
        $keyLookupLines.Add("            { `"$dictKey`", (`"$($entry.Group)`", `"$sanKey`") },")
    }
}
$keyLookupLines.Sort()

$cultureSetLines = @()
foreach ($g in $allGroupNames) {
    $cultureSetLines += "                $g.Culture = value;"
}

$lookupLines = @()
foreach ($g in $allGroupNames) {
    $lookupLines += "                `"$g`" => $g.GetString(key),"
}

$loadLines = @()
foreach ($g in $allGroupNames) {
    $loadLines += "            LoadGroup($g.ResourceManager, resources);"
}

$stringsCs = @"
//------------------------------------------------------------------------------
// Centralized i18n string lookup - delegates to split resx groups.
//------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Resources;

namespace Ink_Canvas.Properties
{
    public static class Strings
    {
        private static readonly Dictionary<string, (string Group, string Key)> KeyDict = BuildKeyDict();
        private static CultureInfo _resourceCulture;

        private static Dictionary<string, (string Group, string Key)> BuildKeyDict()
        {
            var dict = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
$($keyLookupLines -join "`r`n")
            return dict;
        }

        public static CultureInfo Culture
        {
            get => _resourceCulture;
            set
            {
                _resourceCulture = value;
$($cultureSetLines -join "`r`n")
            }
        }

        public static ResourceManager ResourceManager => CommonStrings.ResourceManager;

        public static string GetString(string key)
        {
            if (KeyDict.TryGetValue(key, out var mapping))
            {
                return Lookup(mapping.Group, mapping.Key);
            }
            return "#key:" + key;
        }

        private static string Lookup(string group, string key)
        {
            return group switch
            {
$($lookupLines -join "`r`n")
                _ => "#" + group + "." + key
            };
        }

        public static void LoadAllToResources(System.Windows.ResourceDictionary resources)
        {
$($loadLines -join "`r`n")
        }

        private static void LoadGroup(ResourceManager rm, System.Windows.ResourceDictionary resources)
        {
            var culture = _resourceCulture ?? CultureInfo.CurrentUICulture;
            using var rs = rm.GetResourceSet(culture, true, true);
            if (rs == null) return;
            foreach (System.Collections.DictionaryEntry entry in rs)
            {
                if (entry.Key is string k && entry.Value is string v)
                    resources[k] = v;
            }
        }
    }
}
"@

$utf8NoBom2 = [System.Text.UTF8Encoding]::new($false)
[System.IO.File]::WriteAllText((Join-Path $propertiesDir "Strings.cs"), $stringsCs, $utf8NoBom2)
Write-Host "  Created: Strings.cs"

Write-Host ""
Write-Host "=== Step 5: Update infrastructure code ==="

$locHelperPath = Join-Path $projectDir "Helpers\LocalizationHelper.cs"
$locHelperContent = @"
using Ink_Canvas.Properties;
using System.Globalization;
using System.Threading;

namespace Ink_Canvas.Helpers
{
    public static class LocalizationHelper
    {
        public static CultureInfo CurrentCulture
        {
            get => Thread.CurrentThread.CurrentUICulture;
            set
            {
                if (value == null) return;
                Thread.CurrentThread.CurrentUICulture = value;
                Strings.Culture = value;
            }
        }

        public static bool TrySetCulture(string cultureName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(cultureName))
                {
                    CurrentCulture = CultureInfo.InstalledUICulture;
                    return true;
                }
                var culture = CultureInfo.GetCultureInfo(cultureName);
                CurrentCulture = culture;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string GetString(string key)
        {
            return Strings.GetString(key);
        }
    }
}
"@
[System.IO.File]::WriteAllText($locHelperPath, $locHelperContent, $utf8NoBom2)
Write-Host "  Updated: LocalizationHelper.cs"

$i18nPath = Join-Path $projectDir "MarkupExtensions\I18nExtension.cs"
$i18nContent = @"
using Ink_Canvas.Properties;
using System;
using System.Windows.Markup;

namespace Ink_Canvas.MarkupExtensions
{
    public class I18nExtension : MarkupExtension
    {
        public string Key { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return string.IsNullOrEmpty(Key) ? string.Empty : (Strings.GetString(Key) ?? ("#" + Key));
        }
    }
}
"@
[System.IO.File]::WriteAllText($i18nPath, $i18nContent, $utf8NoBom2)
Write-Host "  Updated: I18nExtension.cs"

Write-Host ""
Write-Host "=== Step 6: Update App.xaml.cs ==="

$appCsPath = Join-Path $projectDir "App.xaml.cs"
$appContent = [System.IO.File]::ReadAllText($appCsPath, [System.Text.Encoding]::UTF8)

$appContent = $appContent.Replace(
    "_pendingLocalizedResourceSet = Strings.ResourceManager.GetResourceSet(CultureInfo.CurrentUICulture, true, true);",
    "Strings.LoadAllToResources(Current.Resources);")

$appContent = $appContent.Replace(
    "private static System.Resources.ResourceSet _pendingLocalizedResourceSet;",
    "// _pendingLocalizedResourceSet removed - using Strings.LoadAllToResources")

$oldBlock = @'
                if (_pendingLocalizedResourceSet != null)
                {
                    LoadLocalizedResources(_pendingLocalizedResourceSet);
                    _pendingLocalizedResourceSet = null;
                }
'@
$newBlock = "                // Resource loading now handled by Strings.LoadAllToResources in OnStartup"
$appContent = $appContent.Replace($oldBlock, $newBlock)

[System.IO.File]::WriteAllText($appCsPath, $appContent, $utf8NoBom2)
Write-Host "  Updated: App.xaml.cs"

Write-Host ""
Write-Host "=== Step 7: Delete old resx files ==="

$filesToDelete = @(
    (Join-Path $propertiesDir "Strings.resx"),
    (Join-Path $propertiesDir "Strings.en-US.resx"),
    (Join-Path $propertiesDir "Strings.Designer.cs")
)

foreach ($f in $filesToDelete) {
    if (Test-Path $f) {
        Remove-Item $f -Force
        Write-Host "  Deleted: $([System.IO.Path]::GetFileName($f))"
    }
}

Write-Host ""
Write-Host "=== COMPLETE ==="
Write-Host "XAML replacements: $xamlReplaced"
Write-Host "CSharp replacements: $csReplaced"
