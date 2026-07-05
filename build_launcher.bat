@echo off
echo Compiling Stash Launcher...
"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /target:winexe /out:Stash.exe /win32icon:icon.ico /r:System.dll,System.Windows.Forms.dll,System.Drawing.dll,System.IO.Compression.dll,System.IO.Compression.FileSystem.dll launcher.cs
if %errorlevel% equ 0 (
    echo Compilation successful! Stash.exe created.
) else (
    echo Compilation failed with error code %errorlevel%.
)
pause
