namespace Ev2.Gen

[<AutoOpen>]
module Common =
    let internal nullString   = null:string
    type IValue = interface end
    type IStruct = interface end
    type IArray = interface end
    type IFBInstance = interface end


    type PouType =
        | PouProgram
        | PouFunction
        | PouFB // Fuction Block

    /// Programming Language
    type PouLanguage =
        | LD   // Ladder Diagram
        | ST   // Structured Text
        | IL   // Instruction List
        //| SFC  // Sequential Function Chart (확장용.  FB에서만 유효)
