@echo off
echo ========================================
echo  ArtaznIT Suite - Admin Launcher
echo ========================================
echo.
echo Launching as: PROCESS\admin.bnecessary-a
echo.
echo NOTE: First time will ask for password.
echo       Password will be saved securely by Windows.
echo.
runas /user:PROCESS\admin.bnecessary-a /savecred "C:\Users\brandon.necessary\source\repos\ArtaznIT\ArtaznIT\bin\Debug\ArtaznIT.exe"
