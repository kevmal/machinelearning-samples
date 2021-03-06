﻿// Learn more about F# at http://fsharp.org

open System
open System.IO
open Microsoft.ML
open Microsoft.ML.Data
open SentimentAnalysis.DataStructures.Model


let appPath = Path.GetDirectoryName(Environment.GetCommandLineArgs().[0])

let baseDatasetsLocation = @"../../../../Data"
let trainDataPath = sprintf @"%s/wikipedia-detox-250-line-data.tsv" baseDatasetsLocation
let testDataPath = sprintf @"%s/wikipedia-detox-250-line-test.tsv" baseDatasetsLocation

let baseModelsPath = @"../../../../MLModels";
let modelPath = sprintf @"%s/SentimentModel.zip" baseModelsPath



let read (dataPath : string) (dataLoader : TextLoader) =
    dataLoader.Read dataPath

let buildTrainEvaluateAndSaveModel (mlContext : MLContext) =
    // STEP 1: Common data loading configuration
    let textLoader =
        mlContext.Data.CreateTextReader (
            columns = 
                [|
                    TextLoader.Column("Label", Nullable DataKind.Bool, 0)
                    TextLoader.Column("Text", Nullable DataKind.Text, 1)
                |],
            hasHeader = true,
            separatorChar = '\t'
        )

    let trainingDataView = textLoader.Read trainDataPath
    let testDataView = textLoader.Read testDataPath


    // STEP 2: Common data process configuration with pipeline data transformations          
    let dataProcessPipeline = 
        mlContext.Transforms.Text.FeaturizeText("Text", "Features")
        |> Common.ModelBuilder.appendCacheCheckpoint mlContext

    // (OPTIONAL) Peek data (such as 2 records) in training DataView after applying the ProcessPipeline's transformations into "Features" 
    Common.ConsoleHelper.peekDataViewInConsole<SentimentIssue> mlContext trainingDataView dataProcessPipeline 2 |> ignore
    Common.ConsoleHelper.peekVectorColumnDataInConsole mlContext "Features" trainingDataView dataProcessPipeline 1 |> ignore

    // STEP 3: Set the training algorithm, then create and config the modelBuilder                            
    let trainer = mlContext.BinaryClassification.Trainers.FastTree(labelColumn = "Label", featureColumn = "Features")
    let modelBuilder = 
        Common.ModelBuilder.create mlContext dataProcessPipeline
        |> Common.ModelBuilder.addTrainer trainer

    // STEP 4: Train the model fitting to the DataSet
    printfn "=============== Training the model ==============="
    let trainedModel = 
        modelBuilder
        |> Common.ModelBuilder.train trainingDataView

    // STEP 5: Evaluate the model and show accuracy stats
    printfn "===== Evaluating Model's accuracy with Test data ====="
    let predictions = trainedModel.Transform testDataView
    let metrics = mlContext.BinaryClassification.Evaluate(predictions, "Label", "Score")

    Common.ConsoleHelper.printBinaryClassificationMetrics (trainer.ToString()) metrics

    // STEP 6: Save/persist the trained model to a .ZIP file
    (trainedModel, modelBuilder)
    |> Common.ModelBuilder.saveModelAsFile modelPath


// (OPTIONAL) Try/test a single prediction by loding the model from the file, first.
let testSinglePrediction (mlContext : MLContext) =
    let sampleStatement = { Text = "This is a very rude movie" }
    
    let resultprediction = 
        Common.ModelScorer.create mlContext
        |> Common.ModelScorer.loadModelFromZipFile modelPath
        |> Common.ModelScorer.predictSingle sampleStatement

    printfn "=============== Single Prediction  ==============="
    printfn "Text: %s | Prediction: %s sentiment | Probability: %f "
        sampleStatement.Text
        (if Convert.ToBoolean(resultprediction.Prediction) then "Toxic" else "Nice")
        resultprediction.Probability
    printfn "=================================================="
    

[<EntryPoint>]
let main argv =
    
    //Create MLContext to be shared across the model creation workflow objects 
    //Set a random seed for repeatable/deterministic results across multiple trainings.
    let mlContext = MLContext(seed = Nullable 1)

    // Create, Train, Evaluate and Save a model
    buildTrainEvaluateAndSaveModel mlContext
    Common.ConsoleHelper.consoleWriteHeader "=============== End of training processh ==============="

    // Make a single test prediction loding the model from .ZIP file
    testSinglePrediction mlContext

    Common.ConsoleHelper.consoleWriteHeader "=============== End of process, hit any key to finish ==============="
    Common.ConsoleHelper.consolePressAnyKey()

    0 // return an integer exit code
