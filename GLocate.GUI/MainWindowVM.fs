namespace GLocate.GUI

open Motsoft.Binder.NotifyObject

type MainWindowVM() as this =
    inherit NotifyObject()

    let mutable mainMessage = ""
    let mutable fileToSearch = ""

    let mutable ignoreCase = true
    let mutable ignoreAccents = true
    let mutable regularExpressions = false
    let mutable baseNameOnly = false

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
