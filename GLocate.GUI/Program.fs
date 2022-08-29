open System
open System.IO
open Gdk
open Gtk
open GLocate.GUI

Application.Init()
let app = new Application("com.motsoft.GLocate.GUI", GLib.ApplicationFlags.None)
app.Register(GLib.Cancellable.Current) |> ignore

Directory.SetCurrentDirectory AppContext.BaseDirectory

// Carga el CSS para toda la aplicación.
let cssProvider = new CssProvider()
cssProvider.LoadFromPath("App.css") |> ignore
StyleContext.AddProviderForScreen(Screen.Default, cssProvider, StyleProviderPriority.User)

MainWindow("MainWindow") |> ignore
Application.Run()
