module Constants
open System.IO
open System

let sscm = @"C:\Program Files (x86)\Seapine\Surround SCM\sscm"
let git = @"c:\Program Files (x86)\Git\bin\git.exe"

let repositoryDir = Environment.CurrentDirectory
let LogFile = Path.Combine(repositoryDir, "log.txt")
let writer = File.AppendText(LogFile)
