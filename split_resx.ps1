[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$ErrorActionPreference = "Stop"

$projectRoot = "c:\Users\wdsyzx\Documents\GitHub\community\Ink Canvas"
$resxPath = Join-Path $projectRoot "Properties\Strings.resx"
$enResxPath = Join-Path $projectRoot "Properties\Strings.en-US.resx"
$outputDir = Join-Path $projectRoot "Properties"

# Parse both resx files
$zhXml = [xml](Get-Content $resxPath -Encoding UTF8)
$enXml = [xml](Get-Content $enResxPath -Encoding UTF8)

$zhMap = @{}
foreach ($node in $zhXml.root.data) {
    if ($node.name) { $zhMap[$node.name] = $node.value }
}
$enMap = @{}
foreach ($node in $enXml.root.data) {
    if ($node.name) { $enMap[$node.name] = $node.value }
}

# Define grouping: resx file name -> list of key prefixes
$groups = [ordered]@{
    "CommonStrings" = @("Common", "Yes", "No", "Size", "Time", "Mode", "Hotkey", "ShowNavSidebar")
    "NavStrings" = @("Nav", "Settings")
    "StartupStrings" = @("Startup", "SilentUpdate", "ManualUpdate", "Rollback", "VersionFix")
    "WindowStrings" = @("Window", "Tray")
    "UpdateStrings" = @("Update", "Header", "Msg")
    "StorageStrings" = @("Storage")
    "CanvasStrings" = @("Canvas", "InkRecog", "InkRecognitionPanel")
    "PPTStrings" = @("PPT", "PowerPointPanel")
    "NotificationStrings" = @("Notification")
    "ThemeStrings" = @("Theme", "ThemePanel")
    "GestureStrings" = @("Gesture", "GesturesPanel")
    "AutomationStrings" = @("Automation", "AutomationPanel", "FoldMode")
    "RandomStrings" = @("Random", "LuckyRandomPanel")
    "GeometryStrings" = @("Geometry")
    "TimerStrings" = @("Timer", "TimerPanel", "TimeRange")
    "FloatingBarStrings" = @("FloatingBar", "QuickPanel", "OldUI", "Tools", "SnapshotPanel")
    "FriendlyLinksStrings" = @("FriendlyLinks")
    "HomeStrings" = @("Home", "Splash")
    "SecurityStrings" = @("SecurityPanel", "SettingsBaseView", "Tooltip")
}

# Assign each key to a group
$keyToGroup = @{}
$groupKeys = @{}
foreach ($gname in $groups.Keys) {
    $groupKeys[$gname] = [System.Collections.ArrayList]::new()
}

foreach ($key in $zhMap.Keys) {
    $prefix = ($key -split '_')[0]
    $assigned = $false
    foreach ($gname in $groups.Keys) {
        foreach ($p in $groups[$gname]) {
            if ($prefix -eq $p -or $key -eq $p) {
                $keyToGroup[$key] = $gname
                [void]$groupKeys[$gname].Add($key)
                $assigned = $true
                break
            }
        }
        if ($assigned) { break }
    }
    if (-not $assigned) {
        Write-Host "UNASSIGNED KEY: $key" -ForegroundColor Red
    }
}

# Function to generate resx XML content
function GenerateResx($keys, $zhMap, $enMap, $stripPrefix) {
    $sb = [System.Text.StringBuilder]::new()
    [void]$sb.AppendLine('<?xml version="1.0" encoding="utf-8"?>')
    [void]$sb.AppendLine('<root>')
    [void]$sb.AppendLine('  <xsd:schema id="root" xmlns="" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:msdata="urn:schemas-microsoft-com:xml-msdata">')
    [void]$sb.AppendLine('    <xsd:import namespace="http://www.w3.org/XML/1998/namespace" />')
    [void]$sb.AppendLine('    <xsd:element name="root" msdata:IsDataSet="true">')
    [void]$sb.AppendLine('      <xsd:complexType>')
    [void]$sb.AppendLine('        <xsd:choice maxOccurs="unbounded">')
    [void]$sb.AppendLine('          <xsd:element name="metadata">')
    [void]$sb.AppendLine('            <xsd:complexType>')
    [void]$sb.AppendLine('              <xsd:sequence>')
    [void]$sb.AppendLine('                <xsd:element name="value" type="xsd:string" minOccurs="0" />')
    [void]$sb.AppendLine('              </xsd:sequence>')
    [void]$sb.AppendLine('              <xsd:attribute name="name" use="required" type="xsd:string" />')
    [void]$sb.AppendLine('              <xsd:attribute name="type" type="xsd:string" />')
    [void]$sb.AppendLine('              <xsd:attribute name="mimetype" type="xsd:string" />')
    [void]$sb.AppendLine('              <xsd:attribute ref="xml:space" />')
    [void]$sb.AppendLine('            </xsd:complexType>')
    [void]$sb.AppendLine('          </xsd:element>')
    [void]$sb.AppendLine('          <xsd:element name="assembly">')
    [void]$sb.AppendLine('            <xsd:complexType>')
    [void]$sb.AppendLine('              <xsd:attribute name="alias" type="xsd:string" />')
    [void]$sb.AppendLine('              <xsd:attribute name="name" type="xsd:string" />')
    [void]$sb.AppendLine('            </xsd:complexType>')
    [void]$sb.AppendLine('          </xsd:element>')
    [void]$sb.AppendLine('          <xsd:element name="data">')
    [void]$sb.AppendLine('            <xsd:complexType>')
    [void]$sb.AppendLine('              <xsd:sequence>')
    [void]$sb.AppendLine('                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />')
    [void]$sb.AppendLine('                <xsd:element name="comment" type="xsd:string" minOccurs="0" msdata:Ordinal="2" />')
    [void]$sb.AppendLine('              </xsd:sequence>')
    [void]$sb.AppendLine('              <xsd:attribute name="name" type="xsd:string" use="required" msdata:Ordinal="1" />')
    [void]$sb.AppendLine('              <xsd:attribute name="type" type="xsd:string" msdata:Ordinal="3" />')
    [void]$sb.AppendLine('              <xsd:attribute name="mimetype" type="xsd:string" msdata:Ordinal="4" />')
    [void]$sb.AppendLine('              <xsd:attribute ref="xml:space" />')
    [void]$sb.AppendLine('            </xsd:complexType>')
    [void]$sb.AppendLine('          </xsd:element>')
    [void]$sb.AppendLine('        </xsd:choice>')
    [void]$sb.AppendLine('      </xsd:complexType>')
    [void]$sb.AppendLine('    </xsd:element>')
    [void]$sb.AppendLine('  </xsd:schema>')
    [void]$sb.AppendLine('  <resheader name="resmimetype">')
    [void]$sb.AppendLine('    <value>text/microsoft-resx</value>')
    [void]$sb.AppendLine('  </resheader>')
    [void]$sb.AppendLine('  <resheader name="version">')
    [void]$sb.AppendLine('    <value>2.0</value>')
    [void]$sb.AppendLine('  </resheader>')
    [void]$sb.AppendLine('  <resheader name="reader">')
    [void]$sb.AppendLine('    <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>')
    [void]$sb.AppendLine('  </resheader>')
    [void]$sb.AppendLine('  <resheader name="writer">')
    [void]$sb.AppendLine('    <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>')
    [void]$sb.AppendLine('  </resheader>')
    
    foreach ($key in ($keys | Sort-Object)) {
        $newKey = $key
        if ($stripPrefix -and $key.StartsWith($stripPrefix + "_")) {
            $newKey = $key.Substring($stripPrefix.Length + 1)
        }
        $val = $zhMap[$key]
        if ($val -eq $null) { $val = "" }
        $escaped = [System.Security.SecurityElement]::Escape($val)
        [void]$sb.AppendLine("  <data name=`"$newKey`" xml:space=`"preserve`">")
        [void]$sb.AppendLine("    <value>$escaped</value>")
        [void]$sb.AppendLine("  </data>")
    }
    [void]$sb.AppendLine('</root>')
    return $sb.ToString()
}

# Generate each resx file
Write-Host "=== Generating resx files ===" -ForegroundColor Cyan

$stripPrefixMap = @{
    "CommonStrings" = ""
    "NavStrings" = ""
    "StartupStrings" = "Startup"
    "WindowStrings" = ""
    "UpdateStrings" = ""
    "StorageStrings" = "Storage"
    "CanvasStrings" = ""
    "PPTStrings" = "PPT"
    "NotificationStrings" = "Notification"
    "ThemeStrings" = "Theme"
    "GestureStrings" = ""
    "AutomationStrings" = ""
    "RandomStrings" = ""
    "GeometryStrings" = "Geometry"
    "TimerStrings" = ""
    "FloatingBarStrings" = ""
    "FriendlyLinksStrings" = ""
    "HomeStrings" = ""
    "SecurityStrings" = ""
}

foreach ($gname in $groups.Keys) {
    $keys = $groupKeys[$gname]
    if ($keys.Count -eq 0) { continue }
    
    $stripPrefix = $stripPrefixMap[$gname]
    
    # Generate Chinese resx
    $zhContent = GenerateResx $keys $zhMap $enMap $stripPrefix
    $zhPath = Join-Path $outputDir "$gname.resx"
    [System.IO.File]::WriteAllText($zhPath, $zhContent, [System.Text.UTF8Encoding]::new($false))
    Write-Host "  Created $gname.resx ($($keys.Count) keys)" -ForegroundColor Green
    
    # Generate English resx
    $enContent = GenerateResx $keys $enMap $enMap $stripPrefix
    $enPath = Join-Path $outputDir "$gname.en-US.resx"
    [System.IO.File]::WriteAllText($enPath, $enContent, [System.Text.UTF8Encoding]::new($false))
    Write-Host "  Created $gname.en-US.resx" -ForegroundColor Green
}

# Save key mapping for later use in code update
$mappingPath = Join-Path $outputDir "key_mapping.json"
$mapping = @{}
foreach ($key in $keyToGroup.Keys) {
    $gname = $keyToGroup[$key]
    $stripPrefix = $stripPrefixMap[$gname]
    $newKey = $key
    if ($stripPrefix -and $key.StartsWith($stripPrefix + "_")) {
        $newKey = $key.Substring($stripPrefix.Length + 1)
    }
    $mapping[$key] = @{Group=$gname; NewKey=$newKey}
}
$mapping | ConvertTo-Json -Depth 3 | Set-Content $mappingPath -Encoding UTF8
Write-Host "`nKey mapping saved to $mappingPath" -ForegroundColor Cyan
Write-Host "`nDone! Now run the code update script." -ForegroundColor Green
