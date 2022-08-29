namespace GLocate.GUI

open System
open System.IO
open Motsoft.Binder.NotifyObject
open Motsoft.Util

type IProcessBroker = Infrastructure.DI.ProcessesDI.IProcessBroker


type MainWindowVM() as this =
    inherit NotifyObject()

    let startProcessRedirected = IProcessBroker.startProcessRedirectedTry false true false

    //------------------------------------------------------------------------------------------------
    // Propiedades.
    //------------------------------------------------------------------------------------------------
    let mutable isUpdatingDb = false
    let mutable isSearching = false

    let mutable mainMessage = "Especifique qué buscar"
    let mutable fileToSearch = ""

    let mutable ignoreCase = true
    let mutable ignoreAccents = true
    let mutable regularExpressions = false
    let mutable baseNameOnly = false

    member _.IsUpdatingDb
        with get() = isUpdatingDb
        and set newValue  = if isUpdatingDb <> newValue then isUpdatingDb <- newValue ; this.NotifyPropertyChanged()

    member _.IsSearching
        with get() = isSearching
        and set newValue  = if isSearching <> newValue then isSearching <- newValue ; this.NotifyPropertyChanged()

    member _.MainMessage
        with get() = mainMessage
        and set newValue  = if mainMessage <> newValue then mainMessage <- newValue ; this.NotifyPropertyChanged()

    member _.FileToSearch
        with get() = fileToSearch
        and set newValue  = if fileToSearch <> newValue then fileToSearch <- newValue
                            this.NotifyPropertyChanged()

    member _.IgnoreCase
        with get() = ignoreCase
        and set newValue  = if ignoreCase <> newValue then ignoreCase <- newValue ; this.NotifyPropertyChanged()

    member _.IgnoreAccents
        with get() = ignoreAccents
        and set newValue  = if ignoreAccents <> newValue then ignoreAccents <- newValue
                            this.NotifyPropertyChanged()

    member _.RegularExpressions
        with get() = regularExpressions
        and set newValue  = if regularExpressions <> newValue then regularExpressions <- newValue
                            this.NotifyPropertyChanged()

    member _.BaseNameOnly
        with get() = baseNameOnly
        and set newValue  = if baseNameOnly <> newValue then baseNameOnly <- newValue
                            this.NotifyPropertyChanged()
    //------------------------------------------------------------------------------------------------

    //------------------------------------------------------------------------------------------------
    // Ejecuta el comando de búsqueda en segundo plano y procesa las líneas devueltas una a una.
    // El procesado es agnóstico, lo realiza la función processLine que es pasada como parámetro.
    //------------------------------------------------------------------------------------------------
    member _.RunSearchAsyncTry (processLine : string -> unit) =

        let runSearchAsync () =
            task {
                let selectedOptions =
                    [(ignoreCase, "-i") ; (ignoreAccents, "-t") ; (regularExpressions, "-r") ; (baseNameOnly, "-b")]
                    |> List.filter fst
                    |> List.map snd
                    |> String.concat " "

                use myProcess = startProcessRedirected "/usr/bin/locate" $"%s{selectedOptions} %s{fileToSearch}"

                let mutable counter = 0
                while not myProcess.StandardOutput.EndOfStream do
                    let! myLine = myProcess.StandardOutput.ReadLineAsync()
                    processLine myLine
                    counter <- counter + 1

                return counter
            }

        task {
            try
                try
                    this.FileToSearch |> String.IsNullOrWhiteSpace |> failWithIfTrue "Debe de especificar qué quiere buscar."

                    this.MainMessage <- "Buscando..."
                    this.IsSearching <- true

                    do! System.Threading.Tasks.Task.Delay 200
                    let! counter = runSearchAsync()

                    this.MainMessage <- $"{counter} elementos encontrados"

                with e ->
                    this.MainMessage <- "Error en la búsqueda."
                    raise e
            finally
                this.IsSearching <- false
        }

    //------------------------------------------------------------------------------------------------
    // Actualiza la base de datos invocando updatedb en modo elevado.
    //------------------------------------------------------------------------------------------------
    member _.UpdateDbAsyncTry() =

        task {
            this.MainMessage <- "Actualizando..."
            this.IsUpdatingDb <- true

            try
                try
                    use! p = IProcessBroker.startAndWaitForProcessAsyncTry "pkexec" "updatedb"
                    p |> ignore

                    this.MainMessage <- "Base de Datos Actualizada"
                with e -> this.MainMessage <- "Error actualizando."
                          raise e
            finally
                this.IsUpdatingDb <- false
        }

    //------------------------------------------------------------------------------------------------
    // Abre la carpeta contenedora del elemento.
    //------------------------------------------------------------------------------------------------
    member _.OpenContainingFolderTry (fullPath : string) =
        // dbus no puede abrir sendas/ficheros con comas.
        // En ese caso abrimos la carpeta con xdg-open pero el fichero no queda seleccionado.

        if fullPath.Contains(",") then
            let pathToOpen = if File.Exists(fullPath) then $"\"{fullPath}\"/.." else fullPath

            IProcessBroker.startProcessTry "xdg-open" pathToOpen |> ignore
        else
            let args = ["--dest=org.freedesktop.FileManager1"; "--type=method_call"
                        "/org/freedesktop/FileManager1"; "org.freedesktop.FileManager1.ShowItems";
                        $"array:string:\"//{fullPath}\""; "string:\"\""]
                       |> String.concat " "

            IProcessBroker.startProcessTry "dbus-send" args |> ignore
    //------------------------------------------------------------------------------------------------
