namespace GLocate.GUI

open System
open System.IO
open GLib        // Necesario. Ambiguedad entre System.Object y GLib.Object
open Gdk
open Gtk
open Motsoft.Binder
open Motsoft.Binder.BindingProperties

type MainWindow(WindowIdName : string) as this =
    inherit BaseWindow(WindowIdName)

    //----------------------------------------------------------------------------------------------------

    // Referencias a controles

    let MainLabel = this.Gui.GetObject("MainLabel") :?> Label
    let FileToSearchEntry = this.Gui.GetObject("FileToSearchEntry") :?> SearchEntry
    let FileListStore = this.Gui.GetObject("FileListStore") :?> ListStore
    let FileListTree = this.Gui.GetObject("FileListTree") :?> TreeView
    let SearchButton = this.Gui.GetObject("SearchButton") :?> Button
    let UpdateDbButton = this.Gui.GetObject("UpdateDbButton") :?> Button

    let IgnoreCaseCheckButton = this.Gui.GetObject("IgnoreCaseCheckButton") :?> CheckButton
    let IgnoreAccentsCheckButton = this.Gui.GetObject("IgnoreAccentsCheckButton") :?> CheckButton
    let RegularExpressionsCheckButton = this.Gui.GetObject("RegularExpressionsCheckButton") :?> CheckButton
    let BaseNameOnlyCheckButton = this.Gui.GetObject("BaseNameOnlyCheckButton") :?> CheckButton

    let FileListTreePopup = new Menu()

    let setProgramIsBusy (isBusy : bool) =
        let newVal = not isBusy

        FileToSearchEntry.Sensitive <- newVal
        SearchButton.Sensitive <- newVal
        UpdateDbButton.Sensitive <- newVal

    let VM = MainWindowVM()
    let binder = Binder(VM)

    do
        //------------------------------------------------------------------------------------------------
        // Prepara las columnas del FileListTree.
        //------------------------------------------------------------------------------------------------

        binder
            .AddBinding(MainLabel, "label", nameof VM.MainMessage, OneWay)
            .AddBinding(FileToSearchEntry, "text", nameof VM.FileToSearch)
            .AddBinding(IgnoreCaseCheckButton, "active", nameof VM.IgnoreCase)
            .AddBinding(IgnoreAccentsCheckButton, "active", nameof VM.IgnoreAccents)
            .AddBinding(RegularExpressionsCheckButton, "active", nameof VM.RegularExpressions)
            .AddBinding(BaseNameOnlyCheckButton, "active", nameof VM.BaseNameOnly)
        |> ignore

        //------------------------------------------------------------------------------------------------
        // Prepara las columnas del FileListTree.
        //------------------------------------------------------------------------------------------------
        let myRenderer = new CellRendererText()
        let myColumn = new TreeViewColumn("Senda", myRenderer, ([|"text" ; 0|] : obj[]))
        FileListTree.AppendColumn(myColumn) |> ignore
        //------------------------------------------------------------------------------------------------

        //------------------------------------------------------------------------------------------------
        // Configura menú contextual del FileListTree.
        //------------------------------------------------------------------------------------------------
        let FileListTreePopupItem_Copy = new MenuItem("Copiar")
        FileListTreePopupItem_Copy.Activated.AddHandler(EventHandler(this.FileListTreePopup_Copy))
        FileListTreePopup.Append(FileListTreePopupItem_Copy)
        //------------------------------------------------------------------------------------------------

        //------------------------------------------------------------------------------------------------
        // Prepara y muestra la ventana.
        //------------------------------------------------------------------------------------------------
        this.ThisWindow.Maximize()
        this.EnableCtrlQ()

        FileToSearchEntry.GrabFocus()
        this.ThisWindow.Show()
    //----------------------------------------------------------------------------------------------------

    //----------------------------------------------------------------------------------------------------
    // Funcionalidad
    //----------------------------------------------------------------------------------------------------

    member _.RunSearch searchString (processLine : string -> unit) =
        //------------------------------------------------------------------------------------------------
        // Ejecuta el comando de búsqueda en segundo plano y procesa las líneas devueltas una a una.
        // El procesado es agnóstico, lo realiza la función processLine que es pasada como parámetro.
        //------------------------------------------------------------------------------------------------

        task {
            let myPossibleOptions = [(IgnoreCaseCheckButton, "-i")
                                     (IgnoreAccentsCheckButton, "-t")
                                     (RegularExpressionsCheckButton, "-r")
                                     (BaseNameOnlyCheckButton, "-b")]

            let mySelectedOptions =
                myPossibleOptions
                |> List.filter(fun (cb, _) -> cb.Active)
                |> List.map snd
                |> String.concat " "

            use myProcess = new Diagnostics.Process()

            myProcess.StartInfo.UseShellExecute <- false
            myProcess.StartInfo.RedirectStandardOutput <- true
            myProcess.StartInfo.FileName <- "/usr/bin/locate"
            myProcess.StartInfo.Arguments <- $"%s{mySelectedOptions} %s{searchString}"
            myProcess.Start() |> ignore

            while not myProcess.StandardOutput.EndOfStream do
                let! myLine = myProcess.StandardOutput.ReadLineAsync()
                processLine myLine
        }


    member _.GetFoundItemsCount() =
        //------------------------------------------------------------------------------------------------
        // Devuelve el número de elementos en FileListStore.
        //------------------------------------------------------------------------------------------------

        FileListStore.IterNChildren()
    //----------------------------------------------------------------------------------------------------


    //----------------------------------------------------------------------------------------------------
    // Eventos.
    //----------------------------------------------------------------------------------------------------

    member _.OnMainWindowDelete (_ : System.Object) (args : DeleteEventArgs) =
        //------------------------------------------------------------------------------------------------
        // Responde al cierre de la ventana.
        // Como es la ventana principal, también cierra la aplicación.
        //------------------------------------------------------------------------------------------------

        args.RetVal <- true
        Application.Quit()


    member _.FileListTreePopup_Copy (_ : System.Object) (_ : EventArgs) =
        //------------------------------------------------------------------------------------------------
        // Responde al evento de Copiar de menú contextual de FileListTree.
        // Copia la línea seleccionada al portapapeles.
        //------------------------------------------------------------------------------------------------

        let mutable myIter : TreeIter = TreeIter()

        let mySelectedPath = FileListTree.Selection.GetSelectedRows().[0]

        if FileListStore.GetIter(&myIter, mySelectedPath) then
            let myFullPath = FileListStore.GetValue(myIter, 0).ToString()
            Clipboard.GetDefault(Display.Default).Text <- myFullPath


    member _.FileListTreeRowActivated (_ : System.Object) (args : RowActivatedArgs) =
        //------------------------------------------------------------------------------------------------
        // Doble-Click en un línea de FileListTree.
        // Normalmente, invoca dbus-send para abrir la carpeta contenedora con el fichero seleccionado.
        //------------------------------------------------------------------------------------------------

        let mutable myIter = TreeIter()

        if FileListStore.GetIter(&myIter, args.Path) then
            let myFullPath = FileListStore.GetValue(myIter, 0).ToString()

            // dbus no puede abrir sendas/ficheros con comas.
            // En ese caso abrimos la carpeta con xdg-open pero el fichero no queda seleccionado.

            if myFullPath.Contains(",") then
                let myPathToOpen = if File.Exists(myFullPath) then $"\"{myFullPath}\"/.." else myFullPath

                Diagnostics.Process.Start("xdg-open", myPathToOpen) |> ignore
            else
                let myArgs = ["--dest=org.freedesktop.FileManager1"; "--type=method_call"
                              "/org/freedesktop/FileManager1"; "org.freedesktop.FileManager1.ShowItems";
                              $"array:string:\"//{myFullPath}\""; "string:\"\""]
                             |> String.concat " "

                Console.WriteLine myArgs
                Diagnostics.Process.Start("dbus-send", myArgs) |> ignore
        else
            this.ErrorDialogBox "No se puede determinar el valor del elemento seleccionado."


    member _.FileToSearchEntryActivate (_ : System.Object) (_ : EventArgs) =
        //------------------------------------------------------------------------------------------------
        // Al pulsar Intro en FileToSearchEntry invoca el click de SearchButton.
        //------------------------------------------------------------------------------------------------

        Signal.Emit(SearchButton, "clicked") |> ignore

    member _.UpdateDbButtonClicked (_ : System.Object) (_ : EventArgs) =
        //------------------------------------------------------------------------------------------------
        // Actualiza la base de datos invocando updatedb en modo elevado.
        //------------------------------------------------------------------------------------------------

        task {
            MainLabel.Text <- "Actualizando..."
            FileListStore.Clear()
            setProgramIsBusy true

            let myProcess = Diagnostics.Process.Start("pkexec", "updatedb")
            do! myProcess.WaitForExitAsync()

            MainLabel.Text <- "Base de Datos Actualizada"
            setProgramIsBusy false
            FileToSearchEntry.GrabFocus()
        } |> ignore

    member _.SearchButtonClicked (_ : System.Object) (_ : EventArgs) =
        //------------------------------------------------------------------------------------------------
        // Click en botón SearchButton.
        // Establece: el preparado anterior, la ejecución de la búsqueda y el preparado posterior.
        //------------------------------------------------------------------------------------------------

        let addStringToList (string : string) =
            match string with
            | null -> ()
            | _ -> FileListStore.AppendValues([|string|]) |> ignore

        let runSearch() =

            task {
                MainLabel.Text <- "Buscando..."
                setProgramIsBusy true
                FileListStore.Clear()

                do! System.Threading.Tasks.Task.Delay 100
                do! this.RunSearch FileToSearchEntry.Text addStringToList

                MainLabel.Text <- $"{this.GetFoundItemsCount()} elementos encontrados"
                setProgramIsBusy false
                FileToSearchEntry.GrabFocus()
            } |> ignore

        match FileToSearchEntry.Text.Length with
        | 0 -> this.ErrorDialogBox "Debe de especificar qué quiere buscar."
        | _ -> runSearch()


    member _.FileListTreeButtonReleaseEvent (_ : System.Object) (args : ButtonReleaseEventArgs) =
        //------------------------------------------------------------------------------------------------
        // Click Botón Derecho sobre FileListTree.
        // Muestra el menú contextual.
        //------------------------------------------------------------------------------------------------

        let mySelectedRows = FileListTree.Selection.GetSelectedRows()

        if mySelectedRows.Length > 0 && args.Event.Button = 3u then
            FileListTreePopup.ShowAll()
            FileListTreePopup.Popup()

    //----------------------------------------------------------------------------------------------------
