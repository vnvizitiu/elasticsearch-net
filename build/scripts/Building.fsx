#I @"../../packages/build/FAKE/tools"
#r @"FakeLib.dll"
#load @"Paths.fsx"
open System
open Fake 
open Paths

let gitLink pdbDir projectName =
    let exe = Paths.Tool("gitlink/lib/net45/GitLink.exe")
    ExecProcess(fun p ->
     p.FileName <- exe
     p.Arguments <- sprintf @". -u %s -d %s -include %s" Paths.Repository pdbDir projectName
    ) (TimeSpan.FromMinutes 5.0) |> ignore



type Build() = 

    static let projects = [ 
        "Elasticsearch.Net"; 
        "Nest"; 
        "Tests" 
    ]

    static let compileCore projects =
        projects
        |> Seq.iter(fun project -> 
            let path = (Paths.Quote (Paths.ProjectJson project))
            Tooling.DotNet.Exec Tooling.DotNetRuntime.Core Build.BuildFailure project ["restore"; path; "--verbosity Warning"]
            Tooling.DotNet.Exec Tooling.DotNetRuntime.Core Build.BuildFailure project ["build"; path; "--configuration Release"]
           )

    static let compileDesktop projects =
        projects
        |> Seq.iter(fun project ->
            Tooling.MsBuild.Exec (Paths.Net45BinFolder project) "Rebuild" Tooling.DotNetFramework.Net45.Identifier [Paths.CsProj(project)]
            Tooling.MsBuild.Exec (Paths.Net46BinFolder project) "Rebuild" Tooling.DotNetFramework.Net46.Identifier [Paths.CsProj(project)]
           )

    static let copyToOutput projects =
        projects
        |> Seq.iter(fun project ->
            let projectName = (project |> directoryInfo).Name
            let outputFolder = Paths.Output(projectName)
            let binFolder = Paths.BinFolder(projectName)
            if not isMono then
                match projectName with
                | "Nest" 
                | "Elasticsearch.Net" ->
                    gitLink (Paths.Net45BinFolder projectName) projectName
                    gitLink (Paths.Net46BinFolder projectName) projectName
                    gitLink (Paths.NetStandard13BinFolder projectName) projectName
                | _  -> ()
            CopyDir outputFolder binFolder allFiles
        )
        
    static member BuildFailure errors =
        raise (BuildException("The project build failed.", errors |> List.ofSeq))

    static member QuickCompile() = 
        compileDesktop projects
        compileCore projects

    static member Compile() =
        compileDesktop projects
        copyToOutput projects
        compileCore projects
        copyToOutput projects
