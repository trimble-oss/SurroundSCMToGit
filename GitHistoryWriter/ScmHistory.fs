module ScmHistory
open System
open System.Text
open System.IO
open System.Reflection
open System.Text.RegularExpressions

/// One checkin action from SCM
type Person() =
    member val UserName = "" with get, set
    member val FullName = "" with get, set
    member val Email = "" with get, set

type PeopleNames() =

    member val UsersList = Map.empty with get, set

    member x.getPerson person =
        let GenerateUserList() = 
            // get sscm users
            let data = List.toArray (ProcessUtil.getOutputOfCommandEncoding "" Constants.sscm (sprintf "lsuser -y+ -e -f") (Encoding.GetEncoding("ISO-8859-1")))
            let Users = Map.empty
            for index in 1 .. data.Length-1 do
                if data.[index].Contains("User name:") then
                    let user = new Person()
                    user.UserName <- data.[index].Split(":".ToCharArray()).[1].Trim()
                    user.FullName <- data.[index+1].Split(":".ToCharArray()).[1].Trim()
                    user.Email <- data.[index+3].Split(":".ToCharArray()).[1].Trim()
                    x.UsersList <- x.UsersList.Add(user.UserName, user)

        // populate users if empty
        if x.UsersList.IsEmpty then
            GenerateUserList()

        if x.UsersList.ContainsKey(person) then
            x.UsersList.[person]
        else
            let user = new Person()
            user.UserName <- person
            user.Email <- person + "@tekla.com>"
            user.FullName <- person
            user

    member x.getEmailLine person =

        let GenerateUserList() = 
            // get sscm users
            let data = List.toArray (ProcessUtil.getOutputOfCommandEncoding "" Constants.sscm (sprintf "lsuser -y+ -e -f") (Encoding.GetEncoding("ISO-8859-1")))
            let Users = Map.empty
            for index in 1 .. data.Length-1 do
                if data.[index].Contains("User name:") then
                    let user = new Person()
                    user.UserName <- data.[index].Split(":".ToCharArray()).[1].Trim()
                    user.FullName <- data.[index+1].Split(":".ToCharArray()).[1].Trim()
                    user.Email <- data.[index+3].Split(":".ToCharArray()).[1].Trim()
                    x.UsersList <- x.UsersList.Add(user.UserName, user)

        // populate users if empty
        if x.UsersList.IsEmpty then
            GenerateUserList()

        if x.UsersList.ContainsKey(person) then
            x.UsersList.[person].FullName + " " + "<" + x.UsersList.[person].Email + ">"
            
        else
            person + " " + "<" + person + "@tekla.com>"



/// One checkin action from SCM
type CheckinItem = {
    /// The time the checking happened
    date : System.DateTime;
    /// The TestTrack defect identifier
    defectnumber : int;
    /// The repository
    repository : string;
    /// List of changed files and their revisions within this checkin
    changedfiles : (string * string * int) list;
    /// List of removed files
    removedfiles : string list;
    /// The person's account who checked in this item
    person : string
    /// The checkin comment
    mutable comment : string
    /// Header of the git commit, with author and comment
    mutable gitcommitheader : string
    /// Complete user information
    user : Person
    }
with static member Empty = 
                        {
                            date = DateTime.Now
                            defectnumber = -1
                            repository = ""
                            changedfiles = List.empty
                            removedfiles = List.empty
                            person = ""
                            comment = ""
                            gitcommitheader = ""
                            user = new Person()
                        }

// Simple type wrapping CSV data
type ScmHistory2(param, dirPrefix) =
    let mutable multilineComment = ""
    let ppl = PeopleNames()
    let (filename, repositoryname) = param
    let removeRepositoryName (filename:string) =
        let len = (repositoryname |> String.length) + 1
        filename.Substring(len)
    let fileToDiskFileName (_file:string) rev =
        let file = _file.Replace('/', '\\')
        let splt = file.Split '\\'
        (sprintf @"%s%s\%s_%d\%s" dirPrefix repositoryname (file.Replace('\\', '_')) rev (splt.[splt.Length-1]))
       
    let UTF8toASCII(text : string) = 
        let utf8 = System.Text.Encoding.UTF8
        let encodedBytes = utf8.GetBytes(text)
        let convertedBytes = Encoding.Convert(Encoding.UTF8, Encoding.ASCII, encodedBytes)
        let ascii = System.Text.Encoding.ASCII
 
        ascii.GetString(convertedBytes);

    let readLines (filePath:string) = seq {
        use sr = new StreamReader (filePath)
        while not sr.EndOfStream do
            yield UTF8toASCII(sr.ReadLine ())
    }

 
    /// Convert SCM exported HTML page to CheckinItem list
    let processLines (lines:string list) =
        
        let processLinesRec (state:CheckinItem list) (currentLine:string) =
            let csv = currentLine.Split([|'\t'|])
            let (success, parsedDate) = DateTime.TryParse(csv.[0])
            let (currentCheckin, tail) = state.Head, state.Tail
            if csv.Length < 5 || not(success) || String.IsNullOrEmpty(csv.[2]) then
                if csv.Length > 4 && String.IsNullOrEmpty(csv.[2]) then
                    state
                else
                    if String.IsNullOrEmpty(currentLine) || not(state.Head.comment.Contains(currentLine)) then
                        state.Head.comment <- state.Head.comment + "\r" + currentLine

                    state.Head.gitcommitheader <- 
                        let getDeletions =
                            state.Head.removedfiles |> List.fold (fun acc cur -> (sprintf "D %s\x0a" cur)+acc) ""
                        let author = ppl.getEmailLine state.Head.person
                        sprintf "%s%s%s%s"
                                "commit refs/heads/master\x0a"
                                (sprintf "committer %s %s\x0a" author (state.Head.date.ToString("r")))
                                (sprintf "data %d\n%s\x0a" state.Head.comment.Length (state.Head.comment))
                                getDeletions

                    state
            // This belongs to same checkin as the previous one, append changes there
            else if parsedDate.Equals(currentCheckin.date) && not(String.IsNullOrEmpty(csv.[2])) then
                multilineComment <- ""
                let file,rev = removeRepositoryName csv.[1], int csv.[2]
                let fileOnDisk = fileToDiskFileName file rev
                let changedfiles = if not(csv.[4].Equals("Remove")) then (file,fileOnDisk,rev) :: currentCheckin.changedfiles else currentCheckin.changedfiles
                let removedfiles = if csv.[4].Equals("Remove") then (removeRepositoryName csv.[1]) :: currentCheckin.removedfiles else currentCheckin.removedfiles
                let newState = {currentCheckin with changedfiles = changedfiles; removedfiles = removedfiles}
                newState :: tail
            // This creates a new checkin
            else
                let file,rev = removeRepositoryName csv.[1], int csv.[2]
                let fileOnDisk = fileToDiskFileName file rev
                let changedfiles = if not(csv.[4].Equals("Remove")) then [(file, fileOnDisk, rev)] else List.empty
                let removedfiles = if csv.[4].Equals("Remove") then [(removeRepositoryName csv.[1])] else List.empty
                let person = csv.[3].ToLower()
                let comment = csv.[5]
                let getSSCMUser =
                    let getDeletions =
                        removedfiles |> List.fold (fun acc cur -> (sprintf "D %s\x0a" cur)+acc) ""
                    ppl.getPerson person

                let getGitCommitHeader =
                    let getDeletions =
                        removedfiles |> List.fold (fun acc cur -> (sprintf "D %s\x0a" cur)+acc) ""
                    let author = ppl.getEmailLine person
                    sprintf "%s%s%s%s"
                            "commit refs/heads/master\x0a"
                            (sprintf "committer %s %s\x0a" author (parsedDate.ToString("r")))
                            (sprintf "data %d\n%s\x0a" comment.Length comment)
                            getDeletions
                let item = {date = parsedDate;
                            defectnumber = 0;
                            repository = repositoryname
                            changedfiles = changedfiles
                            removedfiles = removedfiles
                            person = person
                            comment = comment
                            gitcommitheader = getGitCommitHeader
                            user = getSSCMUser}
                (item::state)
        //processLinesRec lines CheckinItem.Empty List.empty
        lines |> List.fold processLinesRec [CheckinItem.Empty;CheckinItem.Empty;]

    let lines = Seq.toArray (readLines(Path.Combine(dirPrefix, filename)))
    // Cache the sequence of all data lines (all lines but the first)    

    let data = processLines (lines |> List.ofArray) |> Seq.cache
    member __.Data = data
