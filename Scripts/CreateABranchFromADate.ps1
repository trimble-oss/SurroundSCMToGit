$finalbranch=$args[0]
$date=$args[1]
$startbranch=$args[2]
$origin=$args[3]
$repository=$args[4]
$server=$args[5]
$mainrepo=$args[6]

echo "robocopy /NDL /NFL /NP /S /E $mainrepo $finalbranch/repository"
robocopy /NDL /NFL /NP /S /E $mainrepo $finalbranch/repository
cd $finalbranch
cd repository

echo "git remote add origin $origin"
git remote add origin $origin
echo "git fetch origin"
git fetch origin
echo "git rev-list -n 1 --before=$date $startbranch"
$revision = (git rev-list -n 1 --before=$date $startbranch)
echo "git reset --hard $revision"
git reset --hard $revision
echo "git checkout -b $finalbranch"
git checkout -b $finalbranch
echo "<LocationOfWrite>\GitHistoryWriter.exe --replaySCMHistoryToGit MasterRepoHistory.txt $repository $finalbranch d:\SSCMHistory\$repository\$finalbranch\ $server"
<LocationOfWrite>\GitHistoryWriter.exe --replaySCMHistoryToGit MasterRepoHistory.txt $repository $finalbranch d:\SSCMHistory\$repository\$finalbranch\ $server
git push origin ${finalbranch}:${finalbranch}

dir
cd ..
Remove-Item .\repository -Force -Recurse
cd ..
