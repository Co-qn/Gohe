﻿module Xsd

open System.Xml
open System.Xml.Schema
open FParsec

open Xdef
open XsdInternal
open XsdBuildinNodeGenerators

let rec private fromComplexType ns { Particle = particle; Nodes = nodes } = 
  let cType (xparticle:XmlSchemaGroupBase) =
    let cType = XmlSchemaComplexType()
    cType.Particle <- xparticle

    match particle with
    | Sequence occurs | Choice occurs ->
        setOccurrence occurs xparticle
    | All ->
        ()    
    for node in nodes do
      match node with
      | Element _
      | NodeGeneratorInvoke _ -> xparticle.Items.Add(fromNode ns node) |> ignore
      | Attribute _ -> cType.Attributes.Add(fromNode ns node) |> ignore
    cType

  match particle with
  | Sequence _ -> cType (XmlSchemaSequence())
  | Choice _ -> cType (XmlSchemaChoice())
  | All -> cType (XmlSchemaAll())

and fromElement ns ({ Name = name; Occurrence = occurs; Type = eType; Comment = comm } : Element) = 
  let result = XmlSchemaElement()
  result.Name <- name
  setOccurrence occurs result
  comm |> Option.iter(fun comm -> setDoc comm result) 
  
  match eType with
  | Simple(sType, []) ->
      setSimpleType ns sType result
  | Simple(sType, attrs) ->
      let typ = XmlSchemaComplexType()
      result.SchemaType <- typ

      let contentModel = XmlSchemaSimpleContent()
      typ.ContentModel <- contentModel

      let ext = XmlSchemaSimpleContentExtension()
      contentModel.Content <- ext
      for attr in attrs do
        ext.Attributes.Add(fromAttribute ns attr) |> ignore

      setBaseSimpleType ns sType ext result 
  | Complex cType ->
      result.SchemaType <- fromComplexType ns cType

  result

and fromAttribute ns ({ Name = name; Occurrence = occurs; Type = sType; Comment = comm } : Attribute) =
  let result = XmlSchemaAttribute()
  result.Name <- name
  setOccurrenceForAttr occurs result 
  setSimpleType ns sType result
  comm |> Option.iter(fun comm -> setDoc comm result) 
  result

and fromNodeGeneratorInvoke ns invoke = 
  let builtinNodeGenerators = builtinNodeGenerators ns (fromNode ns)

  match lookupElementGenerator builtinNodeGenerators invoke with
  | Some invoker -> 
      let result = invoker invoke
      invoke.Comment |> Option.iter(fun comm -> setDoc comm (result :?> XmlSchemaAnnotated)) 
      result
  | _ -> 
      failwith "未定義のNodeGeneratorが指定されました。"

and fromTypeDef ns ({ Name = name; Type = eType; Comment = comm } : TypeDefine) = 

  match eType with
  | Simple(sType, []) ->
      let result = getSimpleType ns sType 
      result.Name <- name
      comm |> Option.iter(fun comm -> setDoc comm result) 
      result :> XmlSchemaType
  | Simple(sType, attrs) ->
      failwith "属性付きのSimpleTypeは定義できません。"
  | Complex cType ->
      let result = fromComplexType ns cType
      result.Name <- name
      comm |> Option.iter(fun comm -> setDoc comm result) 
      result :> _

and fromNode ns node = 
  match node with
  | Element element -> fromElement ns element :> XmlSchemaObject
  | Attribute attr -> fromAttribute ns attr :> _
  | NodeGeneratorInvoke nodeGeneratorInvoke -> fromNodeGeneratorInvoke ns nodeGeneratorInvoke

let fromRoot ns element = 
  let schema = XmlSchema()
  let root = fromElement ns element
  schema.Items.Add(root) |> ignore
  let schemaSet = XmlSchemaSet()
  schemaSet.Add(schema) |> ignore
  schemaSet.Compile()
  schema

let fromSchema { Nodes = nodes } = 
  let schema = XmlSchema()
  let nodeF = 
    function
    | Element elm ->
        let root = fromElement schema.TargetNamespace elm
        schema.Items.Add(root) |> ignore
    | NodeGeneratorInvoke ({Name = "targetNamespace"; Parameters = [FixedString ns]} as invoke) ->
        schema.TargetNamespace <- ns
        schema.Namespaces.Add("", schema.TargetNamespace)
        schema.ElementFormDefault <- XmlSchemaForm.Qualified
    | NodeGeneratorInvoke ({Name = "include"} as invoke) ->
        let invoke = fromNodeGeneratorInvoke schema.TargetNamespace invoke
        schema.Includes.Add(invoke) |> ignore
    | TypeDefine typeDef ->
        let typeDef = fromTypeDef schema.TargetNamespace typeDef
        schema.Items.Add(typeDef) |> ignore
    | unsupported -> failwithf "このノードは、このスキーマ階層ではサポートされません。:%A" unsupported

  nodes |> List.iter nodeF

  let schemaSet = XmlSchemaSet()
  schemaSet.Add(schema) |> ignore
  schemaSet.Compile()
  schema