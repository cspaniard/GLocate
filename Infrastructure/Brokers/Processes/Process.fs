namespace Brokers.Processes.Process

open System
open System.Diagnostics

type Broker () =

    //----------------------------------------------------------------------------------------------------
    static member startProcessTry processName arguments =

        Process.Start((processName : string), (arguments : string))
    //----------------------------------------------------------------------------------------------------

    //----------------------------------------------------------------------------------------------------
    static member startProcessRedirectedTry redirStdIn redirStdOut redirStdErr
                                            processName arguments =

        let proc = new Process()

        proc.StartInfo.UseShellExecute <- false
        proc.StartInfo.RedirectStandardInput <- redirStdIn
        proc.StartInfo.RedirectStandardOutput <- redirStdOut
        proc.StartInfo.RedirectStandardError <- redirStdErr
        proc.StartInfo.FileName <- processName
        proc.StartInfo.Arguments <- arguments
        proc.Start() |> ignore

        proc
    //----------------------------------------------------------------------------------------------------

    //----------------------------------------------------------------------------------------------------
    static member startAndWaitForProcessAsyncTry processName arguments =

        task {
            let proc = Broker.startProcessTry processName arguments
            do! proc.WaitForExitAsync()

            return proc
        }
    //----------------------------------------------------------------------------------------------------

    //----------------------------------------------------------------------------------------------------
    static member startProcessAndReadAllLinesAsyncTry processName arguments =

        task {
            let startInfo = ProcessStartInfo()
            startInfo.RedirectStandardOutput <- true
            startInfo.FileName <- processName
            startInfo.Arguments <- arguments

            let proc = Process.Start startInfo
            let! content = proc.StandardOutput.ReadToEndAsync()
            do! proc.WaitForExitAsync()

            return content.Split("\n\r", StringSplitOptions.RemoveEmptyEntries)
        }
    //----------------------------------------------------------------------------------------------------
