# 1> Export history from Surrond SCM
 . Tools > Reports > AllChangesReport 
    . Make Duplicate of AllChangesReport for each repository, rename to Repo that is ebeing extracted
    . Double click first item, and choose the repository to extract. There is a extract all branches that needs to be set for branches. Needs to be investigated,
    . Double click second item, choose Add, Checking, Remove, Rename from the selected actions list
    
    Run report and Save the report to disk
    
# 2> Recreate SSCM history into disk
    This will recreate a hard copy of the files, one per version. Incrementally, so the process can be stopped and restarted when needed.
    This is the General command:
        e:\SSCMHistory\githistorywriter\bin\GitHistoryWriter.exe --createSCMHistory LogFromSSCM.txt Repository Branch e:\MasterFolder\ ServerAddress
    
    For example for ExampleRepo
        Run command e:\SSCMHistory\githistorywriter\bin\GitHistoryWriter.exe --createSCMHistory MasterRepoHistory.txt ExampleRepo ExampleRepo E:\SSCMHistory\ExampleRepo\Master\ surroundscm:4900
        
    Please note the \ at then end of the last folder    
    This will create a E:\SSCMHistory\ExampleRepo\Master\ExampleRepo with all files and all versions

# 3> Recreate GIT History into a empty Repository
    Run Git init in some folder. 
    Run the import Command:
        e:\SSCMHistory\githistorywriter\bin\GitHistoryWriter.exe --writeSCMHistoryToGit MasterRepoHistory.txt ExampleRepo ExampleRepo E:\SSCMHistory\ExampleRepo\Master\ surroundscm:4900 | "C:\Program Files (x86)\Git\libexec\git-core\git-fast-import.exe" --date-format=rfc2822
        
    Repack history
        git repack --window=50
        git checkout master
        
# 4> Working with Branches.
    Branchs work in a slightly different way, the method replay the commits on top of one existing branch.
    . Create a branch at some point in history, that diverges from main or from any branch, dont forget to push the existing branch into a remote or it will be lost after reseting into a point in the past    
    . From the existing repository folder run:
        e:\SSCMHistory\githistorywriter\bin\GitHistoryWriter.exe --replaySCMHistoryToGit MasterRepoHistory.txt ExampleRepo 1.5 E:\SSCMHistory\ExampleRepo\Branch1_5\ surroundscm:4900
    The existing repository can be copied into the folder created in second step
        
# 5> Resycn binaries
    The last step of all previous methods is the resynch of the binaries in sscm. This will result in a huge sync commit. This needs to be done by getting latest from SSCM and push the changes to git 

Thats all