module Benchmarks

open Informedica.GenOrder.Lib
open OrderLogger
open MathNet.Numerics
open Informedica.GenUnits.Lib
open Types
module Units = ValueUnit.Units
module DrugConstraint = DrugOrder.DrugConstraint
module Quantity = VariableUnit.Quantity

let gentaMicin() =
    
    let order =
        {
            DrugOrder.drugOrder with
                Id = "1"
                Name = "gentamicin"
                Quantities = [ ]
                Divisible = 1N 
                Unit = "ml"
                TimeUnit = "day"
                Shape = "infusion fluid"
                Route = "iv"
                Products = 
                    [
                        { 
                            DrugOrder.productComponent with
                                Name = "gentamicin"
                                Quantities = [ 2N; 10N ]
                                TimeUnit = "day"
                                Substances = 
                                    [
                                        {
                                            DrugOrder.substanceItem with
                                                Name = "gentamicin"
                                                Concentrations = [ 10N; 40N ]
                                                Unit = "mg"
                                                DoseUnit = "mg"
                                                TimeUnit = "day"
                                        }
                                    ]

                        }
                        { 
                            DrugOrder.productComponent with
                                Name = "saline"
                                Quantities = [ 5000N ]
                                TimeUnit = "day"
                                Substances = 
                                    [
                                        {
                                            DrugOrder.substanceItem with
                                                Name = "sodium"
                                                Concentrations = [ 155N / 1000N ]
                                                Unit = "mmol"
                                                DoseUnit = "mmol"
                                                TimeUnit = "day"
                                        }
                                        {
                                            DrugOrder.substanceItem with
                                                Name = "chloride"
                                                Concentrations = [ 155N / 1000N ]
                                                Unit = "mmol"
                                                DoseUnit = "mmol"
                                                TimeUnit = "day"
                                        }
                                    ]

                        }

                    ]
                OrderType = TimedOrder
            }
        |> DrugOrder.create
        |> DrugOrder.setAdjust "gentamicin" (4N)
        |> DrugOrder.setDoseLimits
            {   DrugOrder.doseLimits with
                    Name = "gentamicin"
                    SubstanceName = "gentamicin"
                    Frequencies = [ 1N ]
                    MinDoseTotalAdjust = Some (4N)
                    MaxDoseTotalAdjust = Some (6N)
            }
        |> DrugOrder.setSolutionLimits 
            {
                DrugOrder.solutionLimits with
                    Name = "gentamicin"
                    Component = "gentamicin"
                    MaxConcentration = Some (2N)
                    DoseCount = Some (1N)
                    MaxTime = (Some (1N/2N))

            }
        |> DrugOrder.evaluate logger.Logger

    ()