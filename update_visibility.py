import os
import re

files = [
    "Ink Canvas/MainWindow_cs/MW_AutoFold.cs",
    "Ink Canvas/MainWindow_cs/MW_FloatingBarIcons.cs",
    "Ink Canvas/MainWindow_cs/MW_Settings.cs"
]

for file in files:
    with open(file, "r", encoding="utf-8") as f:
        content = f.read()
    
    new_content = content.replace("BlackBoardWaterMark.Visibility = ", "BlackBoardWaterMarkContainer.Visibility = ")
    
    if new_content != content:
        with open(file, "w", encoding="utf-8") as f:
            f.write(new_content)
        print(f"Updated {file}")
