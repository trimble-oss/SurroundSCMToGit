@ECHO off
SETLOCAL ENABLEDELAYEDEXPANSION
SETLOCAL ENABLEEXTENSIONS

SET MAINLINE=%2
SET FOLDER=%3

SET SERVER=surroundscm:4900
SET GROUPS=User ReadOnly Autobuild admin

REM this only does the work branch, extend if necessary
FOR %%G IN (%GROUPS%) DO sscm securerepository -b%MAINLINE% -f-All -g%%G -p%MAINLINE%/%FOLDER% -r -y+ -z%SERVER% < yesyes.txt
sscm securerepository -b%MAINLINE% -f+RepoListVisible+Get+History -gadmin -p%MAINLINE%/%FOLDER% -r -y+ -z%SERVER% < yesyes.txt

:usage
ECHO.
ECHO usage: restrict <i(nternal)/e(xternal)> <Mainline> <Folder>
ECHO.

:end