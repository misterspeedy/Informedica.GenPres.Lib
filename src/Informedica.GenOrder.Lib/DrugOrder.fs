﻿namespace Informedica.GenOrder.Lib


// Creating a drug order
module DrugOrder =

    open MathNet.Numerics
    open Informedica.Utils.Lib
    open Informedica.GenUnits.Lib


    open Types


    module OrderType =

        let map (o : Order) =
            match o.Prescription with
            | Prescription.Process         -> ProcessOrder
            | Prescription.Discontinuous _ -> DiscontinuousOrder
            | Prescription.Continuous      -> ContinuousOrder
            | Prescription.Timed (_, _)    -> TimedOrder



    module RouteShape =

        let mapping = 
            [
                "rect", "supp", RectalSolid
                "rect", "zetpil", RectalSolid
                "rectaal", "supp", RectalSolid
                "rectaal", "zetpil", RectalSolid
                "or", "tablet", OralSolid
                "or", "pill", OralSolid
                "or", "pil", OralSolid
                "or", "capsule", OralSolid
                "oraal", "tablet", OralSolid
                "oraal", "pill", OralSolid
                "oraal", "pil", OralSolid
                "oraal", "capsule", OralSolid
                "or", "drink", OralFluid
                "or", "drank", OralFluid
                "iv", "infusion fluid", IntravenousFluid
            ]

        let map route shape =
            mapping
            |> List.find (fun (r, s, _) -> r = route && s = shape )
            |> fun (_, _, x) -> x


    module DrugConstraint =

        module Name = WrappedString.Name
        module Mapping = Order.Mapping
        module Props = Informedica.GenSolver.Lib.Props
        module Constraint = Informedica.GenSolver.Lib.Constraint

        open Informedica.GenSolver.Lib.Types
        open Types 

        let create n m p l rs ps = 
            {
                Name = n
                Mapping = m
                Property = p
                Limit = l
                RouteShape = rs
                OrderType = ps
            }


        let mapToConstraint o (dc : DrugConstraint) : Constraint = 
            {
                Name = 
                    Order.mapName dc.Name dc.Mapping o
                Property = dc.Property
                Limit = dc.Limit
            }


        let toString (c : DrugConstraint) =
            sprintf "%A %A %A %A" c.Name c.Mapping c.Property


        let constraints n =
            let dr =
                [(1N/10N)..(1N/10N)..10N] 
                |> List.append [11N..1N..100N]
                |> List.append [105N..5N..1000N]
                |> Set.ofList

            let c m p vs rs ps =
                create n m p vs rs ps
            // list of general orderable constraints
            [
                // ALL
                c OrderAdjustQty (MaxInclProp 650N) NoLimit 
                  AnyRouteShape AnyOrder
                c OrderAdjustQty (MinInclProp (250N/1000N)) NoLimit 
                  AnyRouteShape AnyOrder
                c OrderAdjustQty ((50N/1000N) |> Set.singleton |> IncrProp) NoLimit 
                  AnyRouteShape AnyOrder
                // == Oral Solid ==
                // == Discontinuous ==
                // give max 10 pieces each time
                c OrderableDoseQty (MaxInclProp 10N) NoLimit 
                  OralSolid DiscontinuousOrder
                // == Rectal Solid ==
                // == Discontinuous ==
                // Give 1 piece each time
                c OrderableDoseQty (MaxInclProp 1N) NoLimit 
                  RectalSolid DiscontinuousOrder
                // == Oral Fluid ==
                // == Discontinuous ==
                // give the total orderable quantity each time
                c OrderableDoseCount (1N |> Set.singleton |> ValsProp) NoLimit 
                    OralFluid DiscontinuousOrder
                // give max 500 ml each time
                c OrderableDoseQty (MaxInclProp 500N) NoLimit 
                  OralFluid DiscontinuousOrder
                // give max 10 ml/kg each time
                c OrderableDoseAdjustQtyAdjust (MaxInclProp 10N) NoLimit 
                  OralFluid DiscontinuousOrder
                // == Oral Fluid ==
                // == Timed ==
                // Give max 500 ml each time
                c OrderableDoseQty (MaxInclProp 500N) NoLimit
                  OralFluid TimedOrder
                // give max 10 ml/kg each time
                c OrderableDoseAdjustQtyAdjust (MaxInclProp 10N) NoLimit 
                  OralFluid TimedOrder
                // == Oral Fluid ==
                // == Continuous ==
                // Max dose rate is 200 ml/hour
                c OrderableDoseRate (MaxInclProp 200N) NoLimit
                  OralFluid ContinuousOrder
                // Max dose rate per kg is 5 ml/kg/hour
                c OrderableDoseRate (MaxInclProp 5N) NoLimit
                  OralFluid ContinuousOrder
                // Set dose rate values
                c OrderableDoseRate (ValsProp dr) NoLimit
                  OralFluid ContinuousOrder
                // == Intravenuous Fluid ==
                // == Discontinuous ==
                // Give max 1000 ml each time
                c OrderableDoseQty (MaxInclProp 1000N) NoLimit
                  IntravenousFluid DiscontinuousOrder
                // Give max 20 ml/kg each time
                c OrderableDoseAdjustQtyAdjust (MaxInclProp 20N) NoLimit
                  IntravenousFluid DiscontinuousOrder
                // == Intravenuous Fluid ==
                // == Timed ==
                // Give max 1000 ml each time
                c OrderableDoseQty (MaxInclProp 1000N) NoLimit
                  IntravenousFluid TimedOrder
                // Give max 20 ml/kg each time
                c OrderableDoseAdjustQtyAdjust (MaxInclProp 20N) NoLimit
                  IntravenousFluid TimedOrder
                // Select 1 possible value from dose rates
                c OrderableDoseRate (ValsProp dr) (MinLim 10)
                  IntravenousFluid TimedOrder
                // == Intravenuous Fluid ==
                // == Continuous ==
                // Max dose rate is 200 ml/hour
                c OrderableDoseRate (MaxInclProp 200N) NoLimit
                  IntravenousFluid ContinuousOrder
                // Max dose rate per kg is 5 ml/kg/hour
                c OrderableDoseRate (MaxInclProp 5N) NoLimit
                  IntravenousFluid ContinuousOrder
                // Set dose rate values
                c OrderableDoseRate (ValsProp dr) NoLimit
                  IntravenousFluid ContinuousOrder
            ]



        let apply log cs (o : Order) =
            let rs = RouteShape.map o.Route o.Orderable.Shape
            let ot = o |> OrderType.map

            let propHasVals = function
            | ValsProp vs
            | IncrProp vs -> vs |> Set.isEmpty |> not
            | _ -> true

            let filter cs =
                cs
                |> List.filter(fun c ->
                    (c.Property |> propHasVals) &&
                    (c.RouteShape = AnyRouteShape || c.RouteShape = rs) &&
                    (c.OrderType =  AnyOrder      || c.OrderType = ot)
                )

            let cs = 
                cs 
                |> filter
                |> List.map (mapToConstraint o)

            o
            |> Order.solveUnits log
            |> Order.solveConstraints log cs
            |> fun o -> 
                Order.calcScenarios log o


    module Item = Orderable.Item
    module IDto = Item.Dto
    module Component = Orderable.Component
    module CDto = Component.Dto
    module ODto = Orderable.Dto

    module Mapping = Order.Mapping
    module Props = Informedica.GenSolver.Lib.Props
    module Constraint = Informedica.GenSolver.Lib.Constraint
    module Name = WrappedString.Name

    open Informedica.GenSolver.Lib.Types

    let (>|>) (cs, o) c = (c |> List.append cs, o)


    let drugOrder =
        {
            Id = ""
            Name = ""
            Products = []
            Quantities = []
            Unit = ""
            TimeUnit = ""
            RateUnit = "hour"
            Shape = ""
            Divisible = 1N
            Route = ""
            OrderType = ProcessOrder
        }


    let productComponent =
        {
            Name = ""
            Quantities = []
            TimeUnit = "day"
            RateUnit = "hour"
            Substances = []
        }


    let substanceItem =
        {
            Name = ""
            Concentrations = []
            OrderableQuantities = []
            Unit = ""
            DoseUnit = ""
            TimeUnit = ""
            RateUnit = ""
        }


    let unitGroup u =
        ValueUnit.Units.units
        |> List.filter (fun ud ->
            ud.Group <> ValueUnit.Group.WeightGroup
        )
        |> List.tryFind (fun ud ->
            [ 
                ud.Abbreviation.Dut
                ud.Abbreviation.Eng
                ud.Name.Dut
                ud.Name.Eng
            ]
            |> List.append ud.Synonyms
            |> List.exists((=) u)
        )
        |> function 
        | Some ud -> 
            ud.Group 
            |> ValueUnit.Group.toString 
        | None -> "General"
        |> sprintf "%s[%s]" u
            

    let create (d : DrugOrder) : ConstrainedOrder =
        let ou = d.Unit |> unitGroup
        let odto = ODto.dto d.Id d.Name d.Shape

        odto.OrderableQuantity.Unit <- ou
        odto.OrderQuantity.Unit <- ou
            
        match d.OrderType with
        | AnyOrder
        | ProcessOrder -> ()

        | ContinuousOrder ->                
            odto.DoseRate.Unit <- 
                d.RateUnit
                |> unitGroup
                |> sprintf "%s/%s" ou
            odto.DoseRateAdjust.Unit <-
                d.RateUnit
                |> unitGroup 
                |> sprintf "%s/kg[Weight]/%s" ou

        | DiscontinuousOrder ->                
            odto.DoseTotal.Unit <-
                d.TimeUnit
                |> unitGroup
                |> sprintf "%s/%s" ou

        | TimedOrder ->                
            odto.DoseTotal.Unit <-
                d.TimeUnit
                |> unitGroup
                |> sprintf "%s/%s" ou
            odto.DoseRate.Unit <- 
                d.RateUnit
                |> unitGroup
                |> sprintf "%s/%s" ou
            odto.DoseRateAdjust.Unit <-
                d.RateUnit
                |> unitGroup 
                |> sprintf "%s/kg[Weight]/%s" ou

        odto.Components <- 
            [
                for p in d.Products do
                    let cdto = CDto.dto d.Id p.Name

                    cdto.Items <- [ 
                        for s in p.Substances do
                            let su = s.Unit |> unitGroup
                            let du = s.DoseUnit |> unitGroup
                            let tu = s.TimeUnit |> unitGroup

                            let idto = IDto.dto d.Id s.Name

                            idto.ComponentConcentration.Unit <- 
                                sprintf "%s/%s" su ou
                            idto.ComponentQuantity.Unit <- su

                            match d.OrderType with
                            | AnyOrder -> ()
                            | ProcessOrder -> ()
                            | ContinuousOrder ->
                                idto.DoseRateAdjust.Unit <- 
                                    sprintf "%s/kg[Weight]/%s" du tu
                            | DiscontinuousOrder ->
                                idto.DoseQuantity.Unit <- du
                                idto.DoseTotalAdjust.Unit <- 
                                    p.TimeUnit
                                    |> unitGroup
                                    |> sprintf "%s/kg[Weight]/%s" du 
                            | TimedOrder ->
                                idto.DoseQuantity.Unit <- du
                                idto.DoseTotalAdjust.Unit <- 
                                    p.TimeUnit
                                    |> unitGroup
                                    |> sprintf "%s/kg[Weight]/%s" du 
                                idto.DoseRateAdjust.Unit <- 
                                    sprintf "%s/kg[Weight]/%s" du tu
                                
                            idto                
                    ]

                    cdto.OrderableQuantity.Unit <- ou
                    cdto.OrderableConcentration.Unit <- "x[Count]"
                    cdto.OrderQuantity.Unit <- ou

                    cdto                        
            ]

        let dto = 
            match d.OrderType with
            | AnyOrder -> 
                "the order type cannot by 'Any'" 
                |> failwith
            | ProcessOrder ->
                Order.Dto.``process`` d.Id d.Name d.Shape d.Route
            | ContinuousOrder ->
                Order.Dto.continuous d.Id d.Name d.Shape d.Route
            | DiscontinuousOrder ->
                Order.Dto.discontinuous d.Id d.Name d.Shape d.Route
            | TimedOrder ->
                Order.Dto.timed d.Id d.Name d.Shape d.Route

        dto.Orderable <- odto

        dto.Prescription.Frequency.Unit <- 
            sprintf "x[Count]/%s" (d.TimeUnit |> unitGroup)
        dto.Adjust.Unit <- "kg[Weight]"

        let cstr m p vs rs ot = 
            DrugConstraint.create d.Name m p vs rs ot

        dto
        |> Order.Dto.fromDto
        |> fun o ->
            // first add all general orderable constraints
            let co = (DrugConstraint.constraints (o.Orderable.Name |> Name.toString), o)
            // adding orderable constraints
            co 
            >|> [ 
                    // ALL set possible orderable quantities
                    cstr OrderableOrderableQty 
                        (d.Quantities |> Set.ofList |> ValsProp) 
                        NoLimit
                        AnyRouteShape AnyOrder

                    // RECTAL SOLID give max 1 piece from rectal solid 
                    cstr OrderableDoseQty 
                        (1N |> Set.singleton |> ValsProp) 
                        NoLimit
                        RectalSolid DiscontinuousOrder

                    // ORAL SOLID give max 10 pieces from oral solid
                    cstr OrderableDoseQty 
                        ([ 1N / d.Divisible.. 1N / d.Divisible ..10N ]
                            |> Set.ofList |> ValsProp)
                        NoLimit
                        OralSolid DiscontinuousOrder

                    // ORAL FLUID increment
                    cstr OrderableDoseQty 
                        ((1N/d.Divisible) |> Set.singleton 
                                            |> IncrProp)
                        NoLimit
                        OralFluid DiscontinuousOrder
                    cstr OrderableDoseQty 
                        ((1N/d.Divisible) |> Set.singleton 
                                            |> IncrProp)
                        NoLimit
                        OralFluid TimedOrder
                    cstr OrderableDoseRate 
                        ((1N/d.Divisible) |> Set.singleton 
                                            |> IncrProp)
                        NoLimit
                        OralFluid TimedOrder
                    cstr OrderableDoseRate 
                        ((1N/d.Divisible) |> Set.singleton 
                                            |> IncrProp)
                        NoLimit
                        OralFluid ContinuousOrder

                    // INTRAVENUOUS FLUID increment
                    cstr OrderableDoseQty 
                        ((1N/d.Divisible) |> Set.singleton 
                                            |> IncrProp)
                        NoLimit
                        IntravenousFluid DiscontinuousOrder
                    cstr OrderableDoseQty 
                        ((1N/d.Divisible) |> Set.singleton 
                                            |> IncrProp)
                        NoLimit
                        IntravenousFluid TimedOrder
                    cstr OrderableDoseRate 
                        ((1N/d.Divisible) |> Set.singleton 
                                            |> IncrProp)
                        NoLimit
                        IntravenousFluid TimedOrder
                    cstr OrderableDoseRate 
                        ((1N/d.Divisible) |> Set.singleton 
                                            |> IncrProp)
                        NoLimit
                        IntravenousFluid ContinuousOrder

                ]
        |> fun co ->
            d.Products
            |> Seq.fold (fun co p ->
                let n = p.Name
                // adding component constraints
                let co =
                    co
                    >|> [ 
                            // ALL set possible component quantities
                            DrugConstraint.create n 
                                ComponentComponentQty 
                                (p.Quantities |> Set.ofList |> ValsProp) 
                                NoLimit
                                AnyRouteShape AnyOrder
                            // give max 10 solid oral each time
                            //DrugConstraint.create n 
                            //    ComponentOrderableQty 
                            //    ([ 1N / d.Divisible.. 1N / d.Divisible ..10N ]
                            //     |> Set.ofList |> ValsProp)
                            //    NoLimit
                            //    OralSolid DiscontinuousOrder
                            // give max 

                            // ORAL FLUID
                            DrugConstraint.create n 
                                ComponentOrderableQty 
                                ([ 1N / d.Divisible.. 1N / d.Divisible ..250N ]
                                    |> Set.ofList |> ValsProp)
                                NoLimit
                                OralFluid AnyOrder
                            DrugConstraint.create n 
                                ComponentOrderableQty 
                                ([ 1N / d.Divisible]
                                    |> Set.ofList |> IncrProp)
                                NoLimit
                                OralFluid AnyOrder
                            DrugConstraint.create n 
                                ComponentDoseQty 
                                ([ 1N / d.Divisible ] |> Set.ofList |> IncrProp)
                                NoLimit
                                OralFluid DiscontinuousOrder
                            DrugConstraint.create n 
                                ComponentDoseQty 
                                ([ 1N / d.Divisible ] |> Set.ofList |> IncrProp)
                                NoLimit
                                OralFluid TimedOrder
                            //DrugConstraint.create n 
                            //    ComponentDoseRate 
                            //    ([ 1N / d.Divisible ] |> Set.ofList |> IncrProp)
                            //    NoLimit
                            //    OralFluid TimedOrder
                            //DrugConstraint.create n 
                            //    ComponentDoseRate 
                            //    ([ 1N / d.Divisible ] |> Set.ofList |> IncrProp)
                            //    NoLimit
                            //    OralFluid ContinuousOrder

                            // INRAVENOUS FLUID
                            DrugConstraint.create n 
                                ComponentOrderableQty 
                                ([ 1N / d.Divisible.. 1N / d.Divisible ..500N ]
                                    |> Set.ofList |> ValsProp)
                                NoLimit
                                IntravenousFluid AnyOrder
                            DrugConstraint.create n 
                                ComponentOrderableQty 
                                ([ 1N / d.Divisible ]
                                    |> Set.ofList |> IncrProp)
                                NoLimit
                                IntravenousFluid AnyOrder
                            DrugConstraint.create n 
                                ComponentDoseQty 
                                ([ 1N / d.Divisible ] |> Set.ofList |> IncrProp)
                                NoLimit
                                IntravenousFluid DiscontinuousOrder
                            DrugConstraint.create n 
                                ComponentDoseQty 
                                ([ 1N / d.Divisible ] |> Set.ofList |> IncrProp)
                                NoLimit
                                IntravenousFluid TimedOrder
                            //DrugConstraint.create n 
                            //    ComponentDoseRate 
                            //    ([ 1N / d.Divisible ] |> Set.ofList |> IncrProp)
                            //    NoLimit
                            //    IntravenousFluid TimedOrder
                            //DrugConstraint.create n 
                            //    ComponentDoseRate 
                            //    ([ 1N / d.Divisible ] |> Set.ofList |> IncrProp)
                            //    NoLimit
                            //    IntravenousFluid ContinuousOrder

                            // RECTAL SOLID
                            DrugConstraint.create n 
                                ComponentOrderableQty 
                                (1N |> Set.singleton |> ValsProp)  
                                NoLimit
                                RectalSolid DiscontinuousOrder

                            // SINGLE COMPONENT
                            if d.Products |> List.length = 1 then
                                DrugConstraint.create n
                                    ComponentOrderableConc
                                    (1N |> Set.singleton |> ValsProp)  
                                    NoLimit
                                    AnyRouteShape AnyOrder
                        ]

                p.Substances 
                |> Seq.fold (fun co s ->
                    let n = s.Name
                    // adding item constraints
                    co
                    >|> [ 
                            // ALL set concentrations and quanties
                            DrugConstraint.create n 
                                ItemComponentConc 
                                (s.Concentrations |> Set.ofList |> ValsProp) 
                                NoLimit
                                AnyRouteShape AnyOrder
                            DrugConstraint.create n 
                                ItemOrderableQty 
                                (s.OrderableQuantities |> Set.ofList |> ValsProp) 
                                NoLimit
                                AnyRouteShape AnyOrder
                            if d.Products |> List.length = 1 then
                                DrugConstraint.create n
                                    ItemOrderableConc
                                    (s.Concentrations |> Set.ofList |> ValsProp) 
                                    NoLimit
                                    AnyRouteShape AnyOrder
                                    
                        ]
                ) co
            ) co
                

    let doseLimits =
        {
            Name = ""
            Frequencies = []
            Rates = []
            SubstanceName = ""
            MaxDoseQuantity = None
            MinDoseQuantity = None
            MinDoseQuantityAdjust = None
            MaxDoseQuantityAdjust = None
            MaxDoseTotal = None
            MinDoseTotal = None
            MaxDoseTotalAdjust = None
            MinDoseTotalAdjust = None
            MaxDoseRate = None
            MinDoseRate = None
            MaxDoseRateAdjust = None
            MinDoseRateAdjust = None
        }


    let solutionLimits =
        {
            Name = ""
            Component = ""
            MinConcentration = None
            MaxConcentration = None
            DoseCount = Some 1N
            MinTime = None
            MaxTime = None
        }


    let setDoseLimits (dl : DoseLimits) (co : ConstrainedOrder) : ConstrainedOrder =
        let sn = dl.SubstanceName

        let cr m c v co =
            match v with
            | Some v -> 
                co
                >|> [ DrugConstraint.create sn m (c v) NoLimit AnyRouteShape AnyOrder ]
            | None -> co
                    
        co
        |> function
        | (cs, o) ->
            if dl.Rates |> List.isEmpty then (cs, o)
            else
                let drc =
                    DrugConstraint.create dl.Name 
                        OrderableDoseRate  
                        (dl.Rates |> Set.ofList |> ValsProp)
                        NoLimit AnyRouteShape ContinuousOrder 
                        
                cs 
                |> List.replace (fun c -> c.Mapping = OrderableDoseRate &&
                                            c.OrderType = ContinuousOrder) drc
                , o
        >|> [ 
                DrugConstraint.create dl.Name
                    PresFreq 
                    (dl.Frequencies |> Set.ofList |> ValsProp) 
                    NoLimit AnyRouteShape DiscontinuousOrder 
                DrugConstraint.create dl.Name 
                    PresFreq  
                    (dl.Frequencies |> Set.ofList |> ValsProp)
                    NoLimit AnyRouteShape TimedOrder 
            ]
        |> cr ItemDoseQty MaxInclProp dl.MaxDoseQuantity
        |> cr ItemDoseQty MinInclProp dl.MinDoseQuantity
        |> cr ItemDoseAdjustQtyAdjust MaxInclProp dl.MaxDoseQuantityAdjust
        |> cr ItemDoseAdjustQtyAdjust MinInclProp dl.MinDoseQuantityAdjust
        |> cr ItemDoseTotal MaxInclProp dl.MaxDoseTotal
        |> cr ItemDoseTotal MinInclProp dl.MinDoseTotal
        |> cr ItemDoseAdjustTotalAdjust MaxInclProp dl.MaxDoseTotalAdjust
        |> cr ItemDoseAdjustTotalAdjust MinInclProp dl.MinDoseTotalAdjust
        |> cr ItemDoseRate MaxInclProp dl.MaxDoseRate
        |> cr ItemDoseRate MinInclProp dl.MinDoseRate
        |> cr ItemDoseAdjustRateAdjust MaxInclProp dl.MaxDoseRateAdjust
        |> cr ItemDoseAdjustRateAdjust MinInclProp dl.MinDoseRateAdjust


    let setSolutionLimits (sl : SolutionLimits) 
                            (co : ConstrainedOrder) : ConstrainedOrder =
        let (_, o) = co
        let set n m c v co =
            match v with
            | Some v -> 
                co
                >|> [ DrugConstraint.create n m (c v) NoLimit AnyRouteShape AnyOrder ]
            | None -> co

        co
        >|> [
                if sl.DoseCount |> Option.isSome then
                    DrugConstraint.create 
                        sl.Name 
                        OrderableDoseCount 
                        (sl.DoseCount |> Option.get |> Set.singleton |> ValsProp) 
                        NoLimit AnyRouteShape AnyOrder
            ]
        |> set sl.Name ItemOrderableConc MinInclProp sl.MinConcentration
        |> set sl.Name ItemOrderableConc MaxInclProp sl.MaxConcentration
        |> set sl.Name PresTime MinInclProp sl.MinTime
        |> set sl.Name PresTime MaxInclProp sl.MaxTime



    let setAdjust n a (co : ConstrainedOrder) : ConstrainedOrder =
        co
        >|> [ 
                DrugConstraint.create 
                    n 
                    OrderAdjustQty 
                    (a |> Set.singleton |> ValsProp) 
                    NoLimit
                    AnyRouteShape AnyOrder 
            ]


    let evaluate log (co : ConstrainedOrder) =
        let (cs, o) = co

        DrugConstraint.apply log cs o

