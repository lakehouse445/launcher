dotnet publish -c Release
certutil -hashfile "Launcher\bin\Release\net8.0-windows7.0\win-x64\publish\launcher.exe" MD5
pause