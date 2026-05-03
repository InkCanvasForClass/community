---
name: "add-contributor"
description: "Adds a new contributor to the AboutPage contributors list. Invoke when user wants to add a contributor with avatar and name to the Ink Canvas project."
---

# Add Contributor to AboutPage

This skill automates adding a new contributor to the Ink Canvas AboutPage, including all necessary file modifications.

## When to Use

- User wants to add a new contributor to the "Thanks Contributors" section
- User provides an avatar image path and display name for a contributor
- Any scenario requiring updating the contributors list in AboutPage

## Required Information

Before invoking this skill, you need:
1. **Avatar file path**: Full path to the PNG/JPG image (e.g., `C:\...\Resources\DeveloperAvatars\username.png`)
2. **Display name**: The name to show under the avatar (e.g., "zhaishis(Super-Yyt)")

## Workflow

### Step 1: Verify Avatar File Exists

Check that the avatar image exists at the specified path:
```bash
# Verify file existence
Test-Path "<avatar-file-path>"
```

If the file doesn't exist, ask the user to provide it or confirm the correct path.

### Step 2: Add Contributor to Code

Edit `Windows/SettingsViews/Pages/AboutPage.xaml.cs`:

**Location**: Find the `contributors` ObservableCollection initialization (around line 42-57)

**Action**: Add a new `AvatarItem` entry at the end of the collection, before the closing `};`:

```csharp
new AvatarItem { AvatarPath = "/Resources/DeveloperAvatars/<filename>", Name = "<display-name>" }
```

**Important**: 
- Use relative path starting with `/Resources/DeveloperAvatars/`
- Keep the filename extension (.png/.jpg)
- Ensure proper comma placement (comma if not last item, no comma if last item)

### Step 3: Register Resource in Project File

Edit `InkCanvasForClass.csproj`:

**Location**: Find the DeveloperAvatars resource entries (around line 191-254)

**Action**: Add a new `<Resource>` entry in the same `<ItemGroup>` as other DeveloperAvatars:

```xml
<Resource Include="Resources\DeveloperAvatars\<filename>" />
```

**Placement**: Add it after the last DeveloperAvatars entry (currently after PANDA-JSR.jpg around line 254), before the closing `</ItemGroup>` tag.

### Step 4: Verify Changes

Confirm both files have been updated correctly:
1. Check `AboutPage.xaml.cs` - new AvatarItem added to contributors list
2. Check `InkCanvasForClass.csproj` - new Resource entry registered

## Example Usage

**User Request**: "添加贡献者 zhaishis(Super-Yyt)，头像在 Resources\DeveloperAvatars\Super-Yyt.png"

**Execution**:
1. ✅ Verify `Super-Yyt.png` exists in `Resources/DeveloperAvatars/`
2. ✅ Edit `AboutPage.xaml.cs`: Add `new AvatarItem { AvatarPath = "/Resources/DeveloperAvatars/Super-Yyt.png", Name = "zhaishis(Super-Yyt)" }`
3. ✅ Edit `InkCanvasForClass.csproj`: Add `<Resource Include="Resources\DeveloperAvatars\Super-Yyt.png" />`
4. ✅ Confirm both changes are complete

## File Locations

| File | Purpose |
|------|---------|
| `Windows/SettingsViews/Pages/AboutPage.xaml.cs` | Contains contributor data (line ~42-57) |
| `InkCanvasForClass.csproj` | Resource registration (line ~191-254) |
| `Resources/DeveloperAvatars/` | Avatar images storage |

## Notes

- **Contributors vs Developers**: This skill adds to the *contributors* list (smaller avatars, 48x48), NOT the developers list (larger avatars, 96x96)
- **File Format**: Supports both `.png` and `.jpg` formats
- **Naming Convention**: Use the exact filename from the filesystem (case-sensitive)
- **No XAML Changes Needed**: The XAML template (`ContributorAvatarTemplate`) already handles rendering automatically

## Troubleshooting

**Issue**: Image doesn't display at runtime
- **Solution**: Verify the resource is registered in `.csproj` and the path in code matches exactly

**Issue**: Build error after adding resource
- **Solution**: Check that the file actually exists at the specified path and the XML syntax is correct

**Issue**: Contributor not showing in list
- **Solution**: Ensure the `AvatarItem` was added to the `contributors` collection, not `developers`
