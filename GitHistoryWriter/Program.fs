module main
open System
open System.IO
open System.Text.RegularExpressions

open System.Security.Cryptography
open System.Text
open MSBuild.Tekla.Tasks.Executor
open Microsoft.Build.Utilities
open System.Diagnostics

let mutable SSCMServer = "surroundscm:4900"

let ignoredBinaryFiles = [".7z";".avi";".bin";".bmp";".bsc";".cache";".chm";".db1";".dll";".doc";".docx";".dwg";".exe";".ico";".ifc";".ilk";".ipch";".lib";
    ".mov";".msi";".odt";".OLD";".OLD1";".OLD2";".OLD3";".pal";".pch";".pdb";".pdf";".pfx";".png";".ppt";".pptx";".pyc";".rar";".res";".rfa";".rvt";".sdf";
    ".snk";".suo";".sym";".tlb";".tsfodat";".uel";".vsix";".xls";".xlsx";".yaa";".zip"]

type Message =
    | GitCommit of ScmHistory.CheckinItem
    | FinalItem of AsyncReplyChannel<unit>

type GitOutput(dirPrefix) =

    let StreamCopy(dest : Stream, src : Stream) =
        let buffer : byte array = Array.zeroCreate (4 * 1024)
        let mutable n = 1
        while n > 0 do
            n <- src.Read(buffer, 0, buffer.Length)
            dest.Write(buffer, 0, n)

        dest.Flush()

    let writeStringAsBytestToStream(txt : string) = 
        let data = new MemoryStream(Encoding.UTF8.GetBytes(txt));
        let console = Console.OpenStandardOutput()
        StreamCopy(console, data)

    let writeBytesToStream(bytes : byte []) = 
        let data = new MemoryStream(bytes);
        let console = Console.OpenStandardOutput()
        StreamCopy(console, data)

    let inline_file fileOnDisk fileInGit dir = 
        let text = File.ReadAllBytes(fileOnDisk)
        Console.Write("M 100644 inline "  + fileInGit + "\x0a")
        Console.Write("data " + string text.Length + "\x0a")
        writeBytesToStream(text)
        Console.Write("\x0a")
        text.Length
        
    let writeSingleFile (fileOnDisk:string) fileInGit =
        let isBinary = ignoredBinaryFiles |> List.exists (fun f -> fileOnDisk.EndsWith(f))
        if isBinary then 0
        else
            inline_file fileOnDisk fileInGit dirPrefix

    let deleteSingleFile(fileInGit:string) =
        let isBinary = ignoredBinaryFiles |> List.exists (fun f -> fileInGit.EndsWith(f))
        if not(isBinary) then
            Console.Write("D "  + fileInGit + "\x0a")

    let loop =
        new MailboxProcessor<Message>(fun inbox ->
            let rec Loop() =
                async {
                    let! message = inbox.Receive()
                    match message with
                    | GitCommit(checkin) ->
                        writeStringAsBytestToStream(checkin.gitcommitheader)
                        let bytes = checkin.removedfiles |> List.sumBy(fun x ->
                                deleteSingleFile x
                                0
                        )
                        Constants.writer.WriteLine("{1} -> delete {0} files {3} bytes (queue:{2})", checkin.removedfiles.Length, checkin.date.ToString(), inbox.CurrentQueueLength, bytes) |> ignore
                        Constants.writer.Flush()
                        let bytes = checkin.changedfiles |> List.sumBy(fun (fileInGit, fileOnDisk, _) ->
                            // Some files may not exist because they have been removed!
                            if File.Exists(fileOnDisk) then
                                writeSingleFile fileOnDisk fileInGit
                            else
                                Constants.writer.WriteLine("    Couldn't locate file {0} / {1}", fileOnDisk, fileInGit) |> ignore
                                0
                        )
                        Constants.writer.WriteLine("{1} -> writing {0} files {3} bytes (queue:{2})", checkin.changedfiles.Length, checkin.date.ToString(), inbox.CurrentQueueLength, bytes) |> ignore
                        Constants.writer.Flush()
                        do! Loop()
                    | FinalItem(reply) -> reply.Reply()
                }
            Loop())

    member x.gitWriterLoop() = loop

type GitReplayCommitsOnExistingBranch(dirPrefix) =
    let executor : CommandExecutor = new CommandExecutor(null, int64(1500000))
    let inline_text fileOnDisk fileInGit dir = 
        let text = File.ReadAllText(fileOnDisk)
        let repositoryDir = Environment.CurrentDirectory
        let OkPath = Path.Combine(repositoryDir, fileInGit)

        if not(File.Exists(OkPath)) then
            let parten = Directory.GetParent(OkPath).ToString()
            Directory.CreateDirectory(parten) |> ignore
            File.Create(OkPath).Close() |> ignore

        File.WriteAllText(OkPath, text)
        text.Length
        
    let inline_binary fileOnDisk fileInGit dir =
        let bytes = File.ReadAllBytes(fileOnDisk) |> Array.map char
        let a1 = ("M 755 inline "  + fileInGit + "\x0a" + 
                  "data " + string bytes.LongLength + "\x0a").ToCharArray()
        let a2 = "\x0a".ToCharArray()
        Array.append (Array.append a1 bytes) a2
        
    let writeSingleFile (fileOnDisk:string) fileInGit =
        let isBinary = ignoredBinaryFiles |> List.exists (fun f -> fileOnDisk.EndsWith(f))
        if isBinary then 0
        else
            inline_text fileOnDisk fileInGit dirPrefix

    let loop =
        new MailboxProcessor<Message>(fun inbox ->
            let rec Loop() =
                async {
                    let! message = inbox.Receive()
                    match message with
                    | GitCommit(checkin) ->
                        let bytes = checkin.changedfiles |> List.sumBy(fun (fileInGit, fileOnDisk, _) ->
                            // Some files may not exist because they have been removed!
                            if File.Exists(fileOnDisk) then
                                writeSingleFile fileOnDisk fileInGit
                            else
                                Constants.writer.WriteLine("    Couldn't locate file {0} / {1}", fileOnDisk, fileInGit) |> ignore
                                0
                        )
                        Constants.writer.WriteLine("{1} -> writing {0} files {3} bytes (queue:{2})", checkin.changedfiles.Length, checkin.date.ToString(), inbox.CurrentQueueLength, bytes) |> ignore
                        Constants.writer.Flush()

                        let generateCommandLineArgs =
                            let builder = new CommandLineBuilder()

                            builder.AppendSwitch("commit --allow-empty -a")
                            if String.IsNullOrEmpty(checkin.comment) then
                                builder.AppendSwitch("-m \"No Comment\"")
                            else
                                builder.AppendSwitch("-m \"" + checkin.comment + "\"")
                            builder.AppendSwitch("--author " + "\"" + checkin.user.FullName + " <" + checkin.user.Email + ">\"")
                            builder.ToString()

                        let ProcessOutputDataReceived(e : DataReceivedEventArgs) = 
                            if not(e = null) then
                                let message = e.Data
                                async {
                                    Constants.writer.WriteLine(message)
                                } |> ignore

                        let date = checkin.date.ToString("r")
                        let env = Map.ofList [("GIT_COMMITTER_DATE", date);("GIT_AUTHOR_DATE", date)]
                        let repositoryDir = Environment.CurrentDirectory
                        let returncode = (executor :> ICommandExecutor).ExecuteCommand(Constants.git, "config user.name " + checkin.user.FullName, env, ProcessOutputDataReceived, ProcessOutputDataReceived, repositoryDir)
                        let returncode = (executor :> ICommandExecutor).ExecuteCommand(Constants.git, "config user.email " + checkin.user.Email, env, ProcessOutputDataReceived, ProcessOutputDataReceived, repositoryDir)
                        let returncode = (executor :> ICommandExecutor).ExecuteCommand(Constants.git, "add .", env, ProcessOutputDataReceived, ProcessOutputDataReceived, repositoryDir)
                        let returncode = (executor :> ICommandExecutor).ExecuteCommand(Constants.git, generateCommandLineArgs, env, ProcessOutputDataReceived, ProcessOutputDataReceived, repositoryDir)


                        do! Loop()
                    | FinalItem(reply) -> reply.Reply()
                }
            Loop())

    member x.gitWriterLoop() = loop


type HgImport(dirPrefix) = 
    let transformFileName repository (file:string, rev) =
        sprintf @"%s%s\%s_%d" dirPrefix repository (file.Replace('\\', '_').Replace('/', '_')) rev

    let restoreFile branch repository (file:string, fileOnDisk, rev) =
        let lastindex = file.LastIndexOf('/')
        if not(lastindex = -1) then // Do nothing for top-level files, we don't want them
            let targetFile = (transformFileName repository (file,rev))
            if not(Directory.Exists(targetFile)) then
                let repositoryDir = Environment.CurrentDirectory

                let executor : CommandExecutor = new CommandExecutor(null, int64(1500000))
                let ProcessOutputDataReceived(e : DataReceivedEventArgs) = 
                    if not(e = null) then
                        let message = e.Data
                        ()

                let returncode = (executor :> ICommandExecutor).ExecuteCommand(Constants.sscm, (sprintf "get %s -b%s -p%s -wreplace -z%s -v%d -d%s" file branch repository SSCMServer rev targetFile), Map.empty, ProcessOutputDataReceived, ProcessOutputDataReceived, Environment.CurrentDirectory)
                // creates a dummy folder since if we run this again it will not call sscm command that takes loads of time
                if not(Directory.Exists(targetFile)) then
                    try
                        Directory.CreateDirectory(targetFile) |> ignore
                    with
                    | ex -> ()

    // For testing only...
    let exportEmptyFiles repository (checkin:ScmHistory.CheckinItem) =
        let println (txt:string) =
            Console.Write(txt.Replace("\x0d\x0a", "\x0a")) // unix style
            Console.Write("\x0a")
        Console.Write(checkin.gitcommitheader)
        for fileInGit,fileOnDisk,rev in checkin.changedfiles do
            println (sprintf "M 644 inline %s" fileInGit)
            println "data 0"

    member x.exportNormalFiles repository branch (checkin:ScmHistory.CheckinItem) =
        checkin.changedfiles |> Seq.iter (restoreFile branch checkin.repository)
        GitCommit(checkin)

module main =
    [<EntryPoint>]
    let exec_main args =
        let stopWatch = Stopwatch.StartNew()
        stopWatch.Restart()

        let dirPrefix = args.[4]
        let git = GitOutput(dirPrefix)
        git.gitWriterLoop().Start()
        let repositoryDir = Environment.CurrentDirectory
        let gitReplay = GitReplayCommitsOnExistingBranch(dirPrefix)
        gitReplay.gitWriterLoop().Start()

        Environment.CurrentDirectory <- dirPrefix

        // args[0] : --createSCMHistory or --writeSCMHistoryToGit or --replaySCMHistoryToGit
        // args[1] : file with sscm history
        // args[2] : Repository name, it will extract the first path from sscm history. Example ExampleRepo/UI.Wpf/Themes/Generic/Theme.xaml needs to be ExampleRepo
        // args[3] : Branch name
        // args[4] : working directory
        // args[5] : server
        SSCMServer <- args.[5]

        let d2 = ScmHistory.ScmHistory2((args.[1], args.[2]), dirPrefix)
        let checkins2 = d2.Data |> List.ofSeq |> List.rev |> Seq.skip 2 |> Array.ofSeq

        if args.[0].Equals("--createSCMHistory") then
            let import = HgImport(dirPrefix)
            (checkins2) |> Array.Parallel.map (import.exportNormalFiles args.[2] args.[3]) |> ignore
            printfn "Took: %f" stopWatch.Elapsed.TotalMilliseconds

        if args.[0].Equals("--writeSCMHistoryToGit") then
            Constants.writer.WriteLine("RUN FOLDER:   "+ Directory.GetCurrentDirectory())
            let d2 = ScmHistory.ScmHistory2((args.[1], args.[2]), dirPrefix)
            let checkins2 = d2.Data |> List.ofSeq |> List.rev |> Seq.skip 2 |> Array.ofSeq

            checkins2 |> Array.map GitCommit |> Array.iter (git.gitWriterLoop().Post)
            let itm = fun f -> FinalItem(f)
            git.gitWriterLoop().PostAndReply(itm)

        if args.[0].Equals("--replaySCMHistoryToGit") then
            Constants.writer.WriteLine("RUN FOLDER:   "+ Directory.GetCurrentDirectory())
            let d2 = ScmHistory.ScmHistory2((args.[1], args.[2]), dirPrefix)
            let checkins2 = d2.Data |> List.ofSeq |> List.rev |> Seq.skip 2 |> Array.ofSeq

            checkins2 |> Array.map GitCommit |> Array.iter (gitReplay.gitWriterLoop().Post)
            let itm = fun f -> FinalItem(f)
            Environment.CurrentDirectory <- repositoryDir
            gitReplay.gitWriterLoop().PostAndReply(itm)
            printfn "Took: %f" stopWatch.Elapsed.TotalMilliseconds

        0 // return an integer exit code
