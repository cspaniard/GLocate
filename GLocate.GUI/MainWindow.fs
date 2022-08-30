namespace GLocate.GUI

open System
open GLib        // Necesario. Ambiguedad entre System.Object y GLib.Object
open Gdk
open Gtk
open Motsoft.Binder
open Motsoft.Binder.BindingProperties

type MainWindow(WindowIdName : string) as this =
    inherit BaseWindow(WindowIdName)

    [<Literal>]
    let VERSION = "2.0.0"


    //----------------------------------------------------------------------------------------------------
    // Referencias a controles
    //----------------------------------------------------------------------------------------------------

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
    //----------------------------------------------------------------------------------------------------

    let VM = MainWindowVM()
    let binder = Binder(VM)

    do
        //------------------------------------------------------------------------------------------------
        // Prepara los bindings.
        //------------------------------------------------------------------------------------------------
        let boolNegate (v : System.Object) _ = (v :?> bool) |> not :> System.Object

        binder
            .AddBinding(MainLabel, "label", nameof VM.MainMessage, OneWay)
            .AddBinding(FileToSearchEntry, "text", nameof VM.FileToSearch)
            .AddBinding(FileToSearchEntry, "sensitive", nameof VM.IsSearching, OneWay, boolNegate)
            .AddBinding(FileToSearchEntry, "sensitive", nameof VM.IsUpdatingDb, OneWay, boolNegate)
            .AddBinding(IgnoreCaseCheckButton, "active", nameof VM.IgnoreCase)
            .AddBinding(IgnoreAccentsCheckButton, "active", nameof VM.IgnoreAccents)
            .AddBinding(RegularExpressionsCheckButton, "active", nameof VM.RegularExpressions)
            .AddBinding(BaseNameOnlyCheckButton, "active", nameof VM.BaseNameOnly)
            .AddBinding(SearchButton, "sensitive", nameof VM.IsSearching, OneWay, boolNegate)
            .AddBinding(SearchButton, "sensitive", nameof VM.IsUpdatingDb, OneWay, boolNegate)
            .AddBinding(UpdateDbButton, "sensitive", nameof VM.IsSearching, OneWay, boolNegate)
            .AddBinding(UpdateDbButton, "sensitive", nameof VM.IsUpdatingDb, OneWay, boolNegate)
        |> ignore

        //------------------------------------------------------------------------------------------------
        // Prepara las columnas del FileListTree.
        //------------------------------------------------------------------------------------------------
        let renderer = new CellRendererText()
        let column = new TreeViewColumn("Senda", renderer, ([|"text" ; 0|] : obj[]))
        FileListTree.AppendColumn(column) |> ignore
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
        this.ThisWindow.Title <- $"{this.ThisWindow.Title} - {VERSION}"
        this.ThisWindow.Maximize()
        this.EnableCtrlQ()

        FileToSearchEntry.GrabFocus()
        this.ThisWindow.Show()
    //----------------------------------------------------------------------------------------------------

    //----------------------------------------------------------------------------------------------------
    // Funcionalidad
    //----------------------------------------------------------------------------------------------------

    member _.GetFoundItemsCount() =
        //------------------------------------------------------------------------------------------------
        // Devuelve el número de elementos en FileListStore.
        //------------------------------------------------------------------------------------------------

        FileListStore.IterNChildren()
    //----------------------------------------------------------------------------------------------------


    //----------------------------------------------------------------------------------------------------
    // Eventos.
    //----------------------------------------------------------------------------------------------------

    //------------------------------------------------------------------------------------------------
    // Responde al cierre de la ventana.
    // Como es la ventana principal, también cierra la aplicación.
    //------------------------------------------------------------------------------------------------
    member _.OnMainWindowDelete (_ : System.Object) (args : DeleteEventArgs) =

        args.RetVal <- true
        Application.Quit()
    //------------------------------------------------------------------------------------------------

    //------------------------------------------------------------------------------------------------
    // Responde al evento de Copiar de menú contextual de FileListTree.
    // Copia la línea seleccionada al portapapeles.
    //------------------------------------------------------------------------------------------------
    member _.FileListTreePopup_Copy (_ : System.Object) (_ : EventArgs) =

        let mutable myIter : TreeIter = TreeIter()

        let mySelectedPath = FileListTree.Selection.GetSelectedRows().[0]

        if FileListStore.GetIter(&myIter, mySelectedPath) then
            let myFullPath = FileListStore.GetValue(myIter, 0).ToString()
            Clipboard.GetDefault(Display.Default).Text <- myFullPath
    //------------------------------------------------------------------------------------------------

    //------------------------------------------------------------------------------------------------
    // Doble-Click en un línea de FileListTree.
    // Normalmente, invoca dbus-send para abrir la carpeta contenedora con el fichero seleccionado.
    //------------------------------------------------------------------------------------------------
    member _.FileListTreeRowActivated (_ : System.Object) (args : RowActivatedArgs) =

        try
            let mutable treeIter = TreeIter()

            match FileListStore.GetIter(&treeIter, args.Path) with
            | true -> FileListStore.GetValue(treeIter, 0).ToString() |> VM.OpenContainingFolderTry
            | false -> failwith "No se puede determinar el valor del elemento seleccionado."

        with e -> this.ErrorDialogBox e.Message
    //------------------------------------------------------------------------------------------------

    //------------------------------------------------------------------------------------------------
    // Al pulsar Intro en FileToSearchEntry invoca el click de SearchButton.
    //------------------------------------------------------------------------------------------------
    member _.FileToSearchEntryActivate (_ : System.Object) (_ : EventArgs) =

        Signal.Emit(SearchButton, "clicked") |> ignore
    //------------------------------------------------------------------------------------------------

    //------------------------------------------------------------------------------------------------
    // Actualiza la base de datos invocando updatedb en modo elevado.
    //------------------------------------------------------------------------------------------------
    member _.UpdateDbButtonClicked (_ : System.Object) (_ : EventArgs) =

        task {
            try
                FileListStore.Clear()
                do! VM.UpdateDbAsyncTry()
                FileToSearchEntry.GrabFocus()
            with e -> this.ErrorDialogBox e.Message
        }
        |> ignore
    //------------------------------------------------------------------------------------------------

    //------------------------------------------------------------------------------------------------
    // Click en botón SearchButton.
    // Establece: el preparado anterior, la ejecución de la búsqueda y el preparado posterior.
    //------------------------------------------------------------------------------------------------
    member _.SearchButtonClicked (_ : System.Object) (_ : EventArgs) =

        let addStringToList (string : string) =
            match string with
            | null -> ()
            | _ -> FileListStore.AppendValues([|string|]) |> ignore

        task {
            try
                FileListStore.Clear()
                do! VM.RunSearchAsyncTry addStringToList
                FileToSearchEntry.GrabFocus()
            with e -> this.ErrorDialogBox e.Message
        }
        |> ignore

    //------------------------------------------------------------------------------------------------

    //------------------------------------------------------------------------------------------------
    // Click Botón Derecho sobre FileListTree.
    // Muestra el menú contextual.
    //------------------------------------------------------------------------------------------------
    member _.FileListTreeButtonReleaseEvent (_ : System.Object) (args : ButtonReleaseEventArgs) =

        let mySelectedRows = FileListTree.Selection.GetSelectedRows()

        if mySelectedRows.Length > 0 && args.Event.Button = 3u then
            FileListTreePopup.ShowAll()
            FileListTreePopup.Popup()
    //----------------------------------------------------------------------------------------------------
