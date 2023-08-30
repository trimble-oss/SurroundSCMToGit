module ProcessUtil
open System
open System.Text
open System.IO
open System.Diagnostics

let sscm = @"C:\Program Files (x86)\Seapine\Surround SCM\sscm"

let readTheStream (stream : StreamReader) (ret : Process) = 
    let rec readTheStreamInner (stream : StreamReader) (ret : Process) streamContent =
        match stream.EndOfStream && ret.HasExited with
            | true -> streamContent
            | false->
                let newHistory = stream.ReadLine() :: streamContent
                readTheStreamInner stream ret newHistory
    List.rev(readTheStreamInner stream ret [])

let getOutputOfCommand wd command param =
    //printfn "%s> %s %s" wd command param
    let startInfo = new ProcessStartInfo(command, param)
    startInfo.RedirectStandardOutput <- true
    startInfo.RedirectStandardError <- true
    startInfo.UseShellExecute <- false
    use procHandle = Process.Start(startInfo)
    procHandle.WaitForInputIdle |> ignore
    procHandle.BeginOutputReadLine |> ignore
    readTheStream procHandle.StandardOutput procHandle

let getOutputOfCommandEncoding wd command param encoding =
    let startInfo = new ProcessStartInfo(command, param)
    startInfo.RedirectStandardOutput <- true
    startInfo.RedirectStandardError <- true
    startInfo.UseShellExecute <- false
    startInfo.StandardOutputEncoding <- encoding
    use procHandle = Process.Start(startInfo)
    procHandle.WaitForInputIdle |> ignore
    procHandle.BeginOutputReadLine |> ignore
    readTheStream procHandle.StandardOutput procHandle