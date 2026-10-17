CD ..\..\
IF EXIST "Program\Content\Web" RMDIR "Program\Content\Web" /S /Q
IF NOT EXIST "Program\Content\Web" MKDIR "Program\Content\Web"
XCOPY "Source\RunActivity\Viewer3D\WebServices\Web" "Program\Content\Web" /S /Y
DEL "Program\RunActivityLAA.*"

REM Effects compilation

dotnet tool restore

REM Delayed expansion is needed here: inside a FOR block %ERRORLEVEL% is expanded when the block is
REM parsed, so "|| exit /b %ERRORLEVEL%" always exits 0 and a failed shader compile goes unnoticed.
REM !ERRORLEVEL! is expanded when the line runs, so the real code reaches MSBuild.
setlocal enabledelayedexpansion

FOR %%i IN (Source\RunActivity\Content\*.fx) DO (
    echo Compiling Source\RunActivity\Content\%%~ni.mgfx
	dotnet tool run mgfxc Source\RunActivity\Content\%%~nxi Program\Content\%%~ni.mgfx /Profile:DirectX_11 || exit /b !ERRORLEVEL!
)
