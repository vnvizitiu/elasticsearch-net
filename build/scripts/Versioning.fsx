﻿#I @"../../packages/build/FAKE/tools"
#I @"../../packages/build/FSharp.Data/lib/net40"
#r @"FakeLib.dll"
#r @"FSharp.Data.dll"
#r @"System.Xml.Linq.dll"
#nowarn "0044" //TODO sort out FAKE 5

#load @"Projects.fsx"
#load @"Paths.fsx"
#load @"Commandline.fsx"

open System
open System.Diagnostics
open System.IO
open System.Xml
open System.Text.RegularExpressions
open FSharp.Data 

open Fake 
open AssemblyInfoFile
open SemVerHelper
open Paths
open Projects
open SemVerHelper
open Commandline

module Versioning = 
    // We used to rely on AssemblyInfo.cs from NEST to read and write the current version.
    // Since that file is now generated by the dotnet tooling and GitVersion and similar tooling all still have
    // active issues related to dotnet core, we now just burn this info in global.json

    //Versions in form of e.g 6.1.0 is inferred as datetime so we bake the json shape into the provider like this
    type private GlobalJson = JsonProvider<""" { "sdk": { "version":"x" }, "version": "x" } """ >
    let globalJson = GlobalJson.Load("../../global.json");
    let writeVersionIntoGlobalJson version = 
        let newGlobalJson = GlobalJson.Root (GlobalJson.Sdk(globalJson.Sdk.Version), version.ToString())
        use tw = new StreamWriter("global.json")
        newGlobalJson.JsonValue.WriteTo(tw, JsonSaveOptions.None)
        tracefn "Written (%s) to global.json as the current version will use this version from now on as current in the build" (version.ToString()) 

    let GlobalJsonVersion = parse(globalJson.Version)

    let CurrentVersion =
        Commandline.parse()
        let currentVersion = GlobalJsonVersion
        let bv = getBuildParam "version"
        let buildVersion = if (isNullOrEmpty bv) then None else Some(parse(bv)) 
        match (getBuildParam "target", buildVersion) with
        | ("release", None) -> failwithf "cannot run release because no explicit version number was passed on the command line"
        | ("release", Some v) -> 
            // Warn if version is same as current version
            if (currentVersion >= v) then traceImportant (sprintf "creating release %s when current version is already at %s" (v.ToString()) (currentVersion.ToString()))
            writeVersionIntoGlobalJson v
            v
        | ("canary", Some v) -> failwithf "cannot run canary release, expected no version number to specified but received %s" (v.ToString())
        | ("canary", None) -> 
            let timestampedVersion = (sprintf "ci%s" (DateTime.UtcNow.ToString("yyyyMMddTHHmmss")))
            tracefn "Canary suffix %s " timestampedVersion
            let canaryVersion = parse ((sprintf "%d.%d.0-%s" currentVersion.Major (currentVersion.Minor + 1) timestampedVersion).Trim())
            tracefn "Canary build increased currentVersion (%s) to (%s) " (currentVersion.ToString()) (canaryVersion.ToString())
            canaryVersion
        | _ -> 
            tracefn "Not running 'release' or 'canary' target so using version in global.json (%s) as current" (currentVersion.ToString())
            currentVersion
    
    let CurrentAssemblyVersion = parse (sprintf "%s.0.0" (CurrentVersion.Major.ToString()))
    let CurrentAssemblyFileVersion = parse (sprintf "%s.%s.%s.0" (CurrentVersion.Major.ToString()) (CurrentVersion.Minor.ToString()) (CurrentVersion.Patch.ToString()))

    let ValidateArtifacts() =
        let fileVersion = CurrentVersion
        let assemblyVersion = parse (sprintf "%i.0.0" fileVersion.Major)
        let tmp = "build/output/_packages/tmp"
        !! "build/output/_packages/*.nupkg"
        |> Seq.iter(fun f -> 
           Unzip tmp f
           !! (sprintf "%s/**/*.dll" tmp)
           |> Seq.iter(fun f -> 
                let fv = FileVersionInfo.GetVersionInfo(f)
                let a = GetAssemblyVersion f
                traceFAKE "Assembly: %A File: %s Product: %s => %s" a fv.FileVersion fv.ProductVersion f
                if (a.Minor > 0 || a.Revision > 0 || a.Build > 0) then failwith (sprintf "%s assembly version is not sticky to its major component" f)
                if (parse (fv.ProductVersion) <> fileVersion) then failwith (sprintf "Expected product info %s to match new version %s " fv.ProductVersion (fileVersion.ToString()))
           )
           DeleteDir tmp
        )