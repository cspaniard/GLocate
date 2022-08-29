namespace GLocate.GUI

open GLib
open Gdk
open Gtk

type BaseWindow(WindowIdName : string) as this =

    //----------------------------------------------------------------------------------------------------
    // Almacenamiento de Propiedades.
    //----------------------------------------------------------------------------------------------------
    let gui = new Builder()
    let mutable thisWindow = Unchecked.defaultof<ApplicationWindow>
    //----------------------------------------------------------------------------------------------------

    //----------------------------------------------------------------------------------------------------
    // Inicialización / Constructor.
    //----------------------------------------------------------------------------------------------------
    do
        gui.AddFromFile($"{WindowIdName}.glade") |> ignore
        gui.Autoconnect(this)
        thisWindow <- new ApplicationWindow(gui.GetObject(WindowIdName).Handle)
    //----------------------------------------------------------------------------------------------------

    //----------------------------------------------------------------------------------------------------
    // Propiedades.
    //----------------------------------------------------------------------------------------------------
    member public _.ThisWindow  with get() = thisWindow and set value = thisWindow <- value
    member public _.Gui with get() = gui
    //----------------------------------------------------------------------------------------------------

    //----------------------------------------------------------------------------------------------------
    // Miembros / Métodos.
    //----------------------------------------------------------------------------------------------------

    member _.EnableCtrlQ() =
        //------------------------------------------------------------------------------------------------
        // Habilita el uso de Ctrl+Q en la ventana para cerrarla.
        //------------------------------------------------------------------------------------------------

        let myAccelGroup = new AccelGroup()
        this.ThisWindow.AddAccelGroup(myAccelGroup)

        let QuitMenuItem = new MenuItem()
        QuitMenuItem.Activated.AddHandler(fun _ _ -> Signal.Emit(this.ThisWindow, "delete-event") |> ignore)

        let myCtrl_Q = AccelKey(Key.q, ModifierType.ControlMask, AccelFlags.Mask)
        QuitMenuItem.AddAccelerator("activate", myAccelGroup, myCtrl_Q)
        QuitMenuItem.ShowAll()

        (new Menu()).Append QuitMenuItem      // Parece que si el item no está en un menú, no se activa.


    member _.DoEvents() =
        //------------------------------------------------------------------------------------------------
        // Intenta forzar el proceso de los eventos pendientes en la cola.
        //------------------------------------------------------------------------------------------------

        while Application.EventsPending() do
            Application.RunIteration()


    member _.ShowDialogBox (message : string, messageType, title) =
        //------------------------------------------------------------------------------------------------
        // Base para la construcción de Cuadros de Diálogo.
        //------------------------------------------------------------------------------------------------

        use myDialog = new MessageDialog(this.ThisWindow, DialogFlags.Modal,
                                         messageType, ButtonsType.Ok, "", [])
        myDialog.Title <- title
        myDialog.Text <- $"\n{message}"

        myDialog.Run() |> ignore


    member _.InfoDialogBox infoMessage =
        //------------------------------------------------------------------------------------------------
        // Muestra un Cuadro de Diálogo informativo.
        //------------------------------------------------------------------------------------------------

        this.ShowDialogBox(infoMessage, MessageType.Info, title = "Información")


    member _.ErrorDialogBox errorMessage =
        //------------------------------------------------------------------------------------------------
        // Muestra un Cuadro de Diálogo de tipo Error.
        //------------------------------------------------------------------------------------------------

        this.ShowDialogBox(errorMessage, MessageType.Error, title = "Error")

    member _.ActionBuilder name delegateFunc =
        //------------------------------------------------------------------------------------------------
        // Contruye una SimpleAction y la asocia a la ventana actual.
        //------------------------------------------------------------------------------------------------

        let newAction = new SimpleAction(name, null)
        newAction.Activated.AddHandler(fun _ _ -> delegateFunc())
        this.ThisWindow.AddAction newAction
        newAction
