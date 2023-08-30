# Export history from Surround SCM
 . Tools > Reports > AllChangesReport 
    . Make Duplicate of AllChangesReport for each repository, rename to Repo that is ebeing extracted
    . Double click first item, and choose the repository to extract. There is a extract all branches that needs to be set for branches. Needs to be investigated,
    . Double click second item, choose Add, Checking, Remove, Rename from the selected actions list
    
    Run report and Save the report to disk
    
# Recreate SSCM history into disk
    This will recreate a hard copy of the files, one per version. Incrementally, so the process can be stopped and restarted when needed.
    This is the General command:
        e:\SSCMHistory\githistorywriter\bin\GitHistoryWriter.exe --createSCMHistory LogFromSSCM.txt Repository Branch e:\MasterFolder\ serveraddress:port
    
    For example for BimPlatform
        Run command e:\SSCMHistory\githistorywriter\bin\GitHistoryWriter.exe --createSCMHistory MasterRepoHistory.txt ExampleRepo ExampleRepo E:\SSCMHistory\BimPlatform\Master\ surroundscm:4900
        
    This will create a E:\SSCMHistory\BimPlatform\Master\ExampleRepo with all files and all versions


# Recreate Git History into an empty Repository
    Run Git init in some folder. 
    Run the import Command:
        e:\SSCMHistory\githistorywriter\bin\GitHistoryWriter.exe --writeSCMHistoryToGit MasterRepoHistory.txt ExampleRepo ExampleRepo E:\SSCMHistory\BimPlatform\Master\  | "C:\Program Files (x86)\Git\libexec\git-core\git-fast-import.exe" --date-format=rfc2822
        
    Repack history
        git repack --window=50
        git checkout master
        
# Working with Branches
    Branchs work in a slightly different way, the method replay the commits on top of one existing branch.
    . Create a branch at some point in history, that diverges from main or from any branch
    . From the repository folder run:
        e:\SSCMHistory\githistorywriter\bin\GitHistoryWriter.exe --replaySCMHistoryToGit MasterRepoHistory.txt ExampleRepo 1.5 E:\SSCMHistory\BimPlatform\Branch1_5\
        
    That's all

# Fixing names of authors
    git filter-branch -f --prune-empty --index-filter 'git rm --cached -r -q -- . ; git reset -q $GIT_COMMIT -- MSBuild References Test Tools' -- --all

# Merging a repository (B) into a new folder (dir-B) of another (A)
    Clone both projects
    In the clone of B (/path/to/B):
    git filter-branch --index-filter 'git ls-files -s | sed "s-\t-&dir-B/-"|GIT_INDEX_FILE=$GIT_INDEX_FILE.new git update-index --index-info && mv $GIT_INDEX_FILE.new $GIT_INDEX_FILE' -- --all
    In the clone of A:
    git remote add -f Bproject
    For each branch that exists in both repos:
        git checkout -b branch
        git merge Bproject/branch
    (other branches from B can simply be pushed to A's origin)

    More info:
    http://stackoverflow.com/questions/277029/combining-multiple-git-repositories/618113#618113
    https://www.vlent.nl/weblog/2013/11/02/merge-a-separate-git-repository-into-an-existing-one/

# Taking a folder into a separate repository
    Clone source repo
    In the clone:
    git filter-branch --subdirectory-filter Folder -- --all
    N.B. --prune-empty removes all empty comments (e.g. commits which in SSCM touched only binary files in Folder) This may or may not be wanted

	track all main branches:
		git checkout --track -b 15.0 origin/15.0
		git checkout --track -b 15.0.x origin/15.0.x
		git checkout --track -b 16.0 origin/16.0
		git checkout --track -b 16.0.x origin/16.0.x
		git checkout --track -b 16.1 origin/16.1
		git checkout --track -b 16.1.x origin/16.1.x

	set remote 
		git remote set-url origin xxxx
	push all
		git push --all