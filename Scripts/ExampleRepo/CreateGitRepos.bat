cd ExampleRepo
mkdir repository
cd repository
git init
<LOCALTIONTOWRITTER>\GitHistoryWriter.exe --writeSCMHistoryToGit MasterRepoHistory.txt ExampleRepo ExampleRepo E:\SSCMHistory\ExampleRepo\ExampleRepo\ surroundscm:4900 | "C:\Program Files (x86)\Git\libexec\git-core\git-fast-import.exe" --date-format=rfc2822
git repack --window=50
git checkout master
cd ..
cd ..

