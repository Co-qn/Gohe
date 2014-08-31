﻿module Test.``Xdef Test``

open NUnit.Framework
open FsUnit
open FParsec

let parse p input = 
  match runParserOnString p 0 "" input with
  | Success (r, s, p) -> Some  r
  | Failure (msg, err, s) -> None

[<Test>]
let ``XdefAttributeをパースできる`` () =  
    parse Ast.pXdefAttribute "@Name : String"
    |> should equal (Some <| Ast.xdefAttribute "Name" Ast.Type.String None)

[<Test>]
let ``XdefValueElementをパースできる`` () =  
    parse Ast.pXdefValueElement "Name : String" 
    |> should equal (Some <| Ast.xdefValueElement "Name" None Ast.Type.String None)

[<Test>]
let ``制約付XdefValueElementをパースできる`` () =  
    parse Ast.pXdefValueElement "Name? : String" 
    |> should equal (Some <| Ast.xdefValueElement "Name" (Some Ast.XdefRestriction.Option) Ast.Type.String None)

    parse Ast.pXdefValueElement "Name* : String" 
    |> should equal (Some <| Ast.xdefValueElement "Name" (Some Ast.XdefRestriction.Many) Ast.Type.String None)

    parse Ast.pXdefValueElement "Name| : String" 
    |> should equal (Some <| Ast.xdefValueElement "Name" (Some Ast.XdefRestriction.Choice) Ast.Type.String None)

[<Test>]
let ``XdefElementをパースできる`` () =  
    parse Ast.pXdefElement "Root"
    |> should equal (Some <| Ast.xdefElement "Root" None None [])

[<Test>]
let ``子要素持ちのXdefElementをパースできる`` () =  
    let xdef = "Root\n  @Name : String\n  Description : String"

    let expected = 
      Ast.xdefElement "Root" None None [
        Ast.Attribute <| Ast.xdefAttribute "Name" Ast.String None 
        Ast.ValueElement <| Ast.xdefValueElement "Description" None Ast.String None
        ]

    parse Ast.pXdefElement xdef
    |> should equal (Some <| expected)