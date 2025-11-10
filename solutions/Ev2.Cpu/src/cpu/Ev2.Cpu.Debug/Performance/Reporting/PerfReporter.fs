namespace Ev2.Cpu.Perf.Reporting

open System
open System.IO
open System.Text
open System.Collections.Generic
open Ev2.Cpu.Perf.Core
open Ev2.Cpu.Perf.Analysis

/// 성능 보고서 생성기
type PerformanceReporter(store: MetricStore, analyzer: PerformanceAnalyzer) =
    
    /// 콘솔 보고서 생성
    member this.GenerateConsoleReport(duration: TimeSpan) =
        let endTime = DateTime.UtcNow
        let startTime = endTime.Subtract(duration)
        let metricNames = store.GetMetricNames()
        
        printfn "🔍 Performance Report"
        printfn "===================="
        printfn "Period: %s to %s" (MetricFormatter.formatTimestamp startTime) (MetricFormatter.formatTimestamp endTime)
        printfn "Duration: %s" (MetricFormatter.formatDuration duration)
        printfn ""
        
        // 메트릭별 요약
        printfn "📊 Metrics Summary"
        printfn "------------------"
        for metricName in metricNames do
            let values = store.GetValuesInRange(metricName, startTime, endTime)
            if not (List.isEmpty values) then
                let latest = List.head (List.rev values)
                let floatValues = values |> List.map (fun v -> v.Value)
                let avg = MetricCalculations.average floatValues
                let min = List.min floatValues
                let max = List.max floatValues
                
                printfn "  %s:" metricName
                printfn "    Current: %s" (MetricFormatter.formatValue latest.Value latest.Unit)
                printfn "    Average: %s" (MetricFormatter.formatValue avg latest.Unit)
                let minFormatted = MetricFormatter.formatValue min latest.Unit
                let maxFormatted = MetricFormatter.formatValue max latest.Unit
                printfn "    Range: %s - %s" minFormatted maxFormatted
                printfn "    Samples: %d" (List.length values)
                printfn ""
        
        // 트렌드 분석
        printfn "📈 Trend Analysis"
        printfn "-----------------"
        for metricName in metricNames |> List.take (min 5 (List.length metricNames)) do
            let trend = analyzer.AnalyzeTrend(metricName, duration)
            let trendSymbol = 
                match trend.Trend with
                | Increasing -> "↗️"
                | Decreasing -> "↘️"
                | Stable -> "➡️"
                | NoTrend -> "❓"
            
            printfn "  %s %s: %+.1f%%%% (%A confidence)" trendSymbol metricName trend.ChangeRate trend.Confidence
            if not (String.IsNullOrEmpty trend.Recommendation) then
                printfn "    💡 %s" trend.Recommendation
        
        printfn ""
        
        // 이상 감지
        printfn "⚠️ Anomaly Detection"
        printfn "--------------------"
        let mutable anomalyFound = false
        for metricName in metricNames do
            let anomalies = analyzer.DetectAnomalies(metricName, duration, 2.0)
            if not (List.isEmpty anomalies) then
                anomalyFound <- true
                printfn "  %s:" metricName
                for anomaly in anomalies |> List.take (min 3 (List.length anomalies)) do
                    let severitySymbol = 
                        match anomaly.Severity with
                        | Critical -> "🔴"
                        | Warning -> "🟡"
                        | Info -> "🔵"
                    printfn "    %s %s - %s" severitySymbol (MetricFormatter.formatTimestamp anomaly.Timestamp) anomaly.Description
        
        if not anomalyFound then
            printfn "  ✅ No anomalies detected"
        
        printfn ""
    
    /// HTML 보고서 생성
    member this.GenerateHtmlReport(filePath: string, duration: TimeSpan) =
        let endTime = DateTime.UtcNow
        let startTime = endTime.Subtract(duration)
        let metricNames = store.GetMetricNames()
        
        use writer = new StreamWriter(filePath, false, Encoding.UTF8)
        
        writer.WriteLine("<!DOCTYPE html>")
        writer.WriteLine("<html lang=\"en\">")
        writer.WriteLine("<head>")
        writer.WriteLine("    <meta charset=\"UTF-8\">")
        writer.WriteLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">")
        writer.WriteLine("    <title>Performance Report</title>")
        writer.WriteLine("    <style>")
        writer.WriteLine( this.GetCssStyles() )
        writer.WriteLine("    </style>")
        writer.WriteLine("</head>")
        writer.WriteLine("<body>")
        
        // 헤더
        writer.WriteLine("    <div class=\"header\">")
        writer.WriteLine("        <h1>🔍 Performance Report</h1>")
        let startFormatted = MetricFormatter.formatTimestamp startTime
        let endFormatted = MetricFormatter.formatTimestamp endTime
        let durationFormatted = MetricFormatter.formatDuration duration
        writer.WriteLine(sprintf "        <p class=\"period\">Period: %s to %s</p>" startFormatted endFormatted)
        writer.WriteLine(sprintf "        <p class=\"duration\">Duration: %s</p>" durationFormatted)
        writer.WriteLine("    </div>")
        
        // 메트릭 요약
        writer.WriteLine("    <div class=\"section\">")
        writer.WriteLine("        <h2>📊 Metrics Summary</h2>")
        writer.WriteLine("        <div class=\"metrics-grid\">")
        
        for metricName in metricNames do
            let values = store.GetValuesInRange(metricName, startTime, endTime)
            if not (List.isEmpty values) then
                let latest = List.head (List.rev values)
                let floatValues = values |> List.map (fun v -> v.Value)
                let avg = MetricCalculations.average floatValues
                let min = List.min floatValues
                let max = List.max floatValues
                
                let currentFormatted = MetricFormatter.formatValue latest.Value latest.Unit
                let avgFormatted = MetricFormatter.formatValue avg latest.Unit
                let minFormatted = MetricFormatter.formatValue min latest.Unit
                let maxFormatted = MetricFormatter.formatValue max latest.Unit
                
                writer.WriteLine("            <div class=\"metric-card\">")
                writer.WriteLine(sprintf "                <h3>%s</h3>" metricName)
                writer.WriteLine(sprintf "                <div class=\"current-value\">%s</div>" currentFormatted)
                writer.WriteLine("                <div class=\"metric-stats\">")
                writer.WriteLine(sprintf "                    <div>Average: %s</div>" avgFormatted)
                writer.WriteLine(sprintf "                    <div>Range: %s - %s</div>" minFormatted maxFormatted)
                writer.WriteLine(sprintf "                    <div>Samples: %d</div>" (List.length values))
                writer.WriteLine("                </div>")
                writer.WriteLine("            </div>")
        
        writer.WriteLine("        </div>")
        writer.WriteLine("    </div>")
        
        // 트렌드 분석
        writer.WriteLine("    <div class=\"section\">")
        writer.WriteLine("        <h2>📈 Trend Analysis</h2>")
        writer.WriteLine("        <div class=\"trends\">")
        
        for metricName in metricNames |> List.take (min 10 (List.length metricNames)) do
            let trend = analyzer.AnalyzeTrend(metricName, duration)
            let trendClass = 
                match trend.Trend with
                | Increasing -> "trend-up"
                | Decreasing -> "trend-down"
                | Stable -> "trend-stable"
                | NoTrend -> "trend-unknown"
            
            writer.WriteLine("            <div class=\"trend-item\">")
            writer.WriteLine(sprintf "                <div class=\"trend-name\">%s</div>" metricName)
            writer.WriteLine(sprintf "                <div class=\"trend-change %s\">%+.1f%%</div>" trendClass trend.ChangeRate)
            writer.WriteLine(sprintf "                <div class=\"trend-confidence\">%A confidence</div>" trend.Confidence)
            if not (String.IsNullOrEmpty trend.Recommendation) then
                writer.WriteLine(sprintf "                <div class=\"trend-recommendation\">%s</div>" trend.Recommendation)
            writer.WriteLine("            </div>")
        
        writer.WriteLine("        </div>")
        writer.WriteLine("    </div>")
        
        // 이상 감지
        writer.WriteLine("    <div class=\"section\">")
        writer.WriteLine("        <h2>⚠️ Anomaly Detection</h2>")
        writer.WriteLine("        <div class=\"anomalies\">")
        
        let mutable anomalyFound = false
        for metricName in metricNames do
            let anomalies = analyzer.DetectAnomalies(metricName, duration, 2.0)
            if not (List.isEmpty anomalies) then
                anomalyFound <- true
                writer.WriteLine(sprintf "            <h3>%s</h3>" metricName)
                for anomaly in anomalies |> List.take (min 5 (List.length anomalies)) do
                    let severityClass = 
                        match anomaly.Severity with
                        | Critical -> "severity-critical"
                        | Warning -> "severity-warning"
                        | Info -> "severity-info"
                    
                    let timestampFormatted = MetricFormatter.formatTimestamp anomaly.Timestamp
                    writer.WriteLine("            <div class=\"anomaly-item\">")
                    writer.WriteLine(sprintf "                <div class=\"anomaly-time\">%s</div>" timestampFormatted)
                    writer.WriteLine(sprintf "                <div class=\"anomaly-severity %s\">%A</div>" severityClass anomaly.Severity)
                    writer.WriteLine(sprintf "                <div class=\"anomaly-description\">%s</div>" anomaly.Description)
                    writer.WriteLine("            </div>")
        
        if not anomalyFound then
            writer.WriteLine("            <div class=\"no-anomalies\">✅ No anomalies detected</div>")
        
        writer.WriteLine("        </div>")
        writer.WriteLine("    </div>")
        
        writer.WriteLine("</body>")
        writer.WriteLine("</html>")
    
    /// JSON 보고서 생성
    member this.GenerateJsonReport(filePath: string, duration: TimeSpan) =
        let endTime = DateTime.UtcNow
        let startTime = endTime.Subtract(duration)
        let metricNames = store.GetMetricNames()
        
        use writer = new StreamWriter(filePath, false, Encoding.UTF8)
        
        writer.WriteLine("{")
        writer.WriteLine(sprintf "  \"reportTime\": \"%s\"," (DateTime.UtcNow.ToString("o")))
        writer.WriteLine(sprintf "  \"startTime\": \"%s\"," (startTime.ToString("o")))
        writer.WriteLine(sprintf "  \"endTime\": \"%s\"," (endTime.ToString("o")))
        writer.WriteLine(sprintf "  \"duration\": \"%O\"," duration)
        writer.WriteLine("  \"metrics\": {")
        
        let metricCount = List.length metricNames
        metricNames |> List.iteri (fun i metricName ->
            let values = store.GetValuesInRange(metricName, startTime, endTime)
            if not (List.isEmpty values) then
                let latest = List.head (List.rev values)
                let floatValues = values |> List.map (fun v -> v.Value)
                let avg = MetricCalculations.average floatValues
                let min = List.min floatValues
                let max = List.max floatValues
                let stdDev = MetricCalculations.standardDeviation floatValues
                
                writer.WriteLine(sprintf "    \"%s\": {" metricName)
                writer.WriteLine(sprintf "      \"current\": %f," latest.Value)
                writer.WriteLine(sprintf "      \"average\": %f," avg)
                writer.WriteLine(sprintf "      \"minimum\": %f," min)
                writer.WriteLine(sprintf "      \"maximum\": %f," max)
                writer.WriteLine(sprintf "      \"standardDeviation\": %f," stdDev)
                writer.WriteLine(sprintf "      \"sampleCount\": %d," (List.length values))
                writer.WriteLine(sprintf "      \"unit\": \"%O\"" latest.Unit)
                
                let comma = if i < metricCount - 1 then "," else ""
                writer.WriteLine(sprintf "    }%s" comma)
        )
        
        writer.WriteLine("  },")
        writer.WriteLine("  \"trends\": [")
        
        let trends = metricNames |> List.map (fun name -> analyzer.AnalyzeTrend(name, duration))
        trends |> List.iteri (fun i trend ->
            writer.WriteLine("    {")
            writer.WriteLine(sprintf "      \"metricName\": \"%s\"," trend.MetricName)
            writer.WriteLine(sprintf "      \"trend\": \"%A\"," trend.Trend)
            writer.WriteLine(sprintf "      \"changeRate\": %f," trend.ChangeRate)
            writer.WriteLine(sprintf "      \"confidence\": \"%A\"," trend.Confidence)
            writer.WriteLine(sprintf "      \"recommendation\": \"%s\"" trend.Recommendation)
            
            let comma = if i < List.length trends - 1 then "," else ""
            writer.WriteLine(sprintf "    }%s" comma)
        )
        
        writer.WriteLine("  ]")
        writer.WriteLine("}")
    
    /// 벤치마크 보고서 생성
    member this.GenerateBenchmarkReport(results: BenchmarkResult list, filePath: string) =
        use writer = new StreamWriter(filePath, false, Encoding.UTF8)
        
        writer.WriteLine("# Benchmark Report")
        writer.WriteLine("")
        writer.WriteLine(sprintf "Generated: %s UTC" (DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")))
        writer.WriteLine("")
        
        writer.WriteLine("## Summary")
        writer.WriteLine("")
        let successful = results |> List.filter (fun r -> r.Success) |> List.length
        let total = List.length results
        writer.WriteLine(sprintf "- Total benchmarks: %d" total)
        writer.WriteLine(sprintf "- Successful: %d" successful)
        writer.WriteLine(sprintf "- Failed: %d" (total - successful))
        writer.WriteLine("")
        
        if successful > 0 then
            let avgThroughput = results |> List.filter (fun r -> r.Success) |> List.map (fun r -> r.ThroughputPerSecond) |> List.average
            let totalMemory = results |> List.map (fun r -> r.MemoryUsage) |> List.max
            
            writer.WriteLine(sprintf "- Average throughput: %.2f ops/sec" avgThroughput)
            writer.WriteLine(sprintf "- Peak memory usage: %s" (MetricFormatter.formatValue (float totalMemory) Bytes))
            writer.WriteLine("")
        
        writer.WriteLine("## Detailed Results")
        writer.WriteLine("")
        
        for result in results do
            writer.WriteLine(sprintf "### %s" result.Name)
            writer.WriteLine("")
            writer.WriteLine(sprintf "- Configuration: %s" result.Configuration)
            writer.WriteLine(sprintf "- Success: %s" (if result.Success then "✅" else "❌"))
            
            if result.Success then
                writer.WriteLine(sprintf "- Duration: %s" (MetricFormatter.formatDuration result.Duration))
                writer.WriteLine(sprintf "- Iterations: %s" (result.Iterations.ToString("N0")))
                writer.WriteLine(sprintf "- Average time: %s" (MetricFormatter.formatDuration result.AverageTime))
                writer.WriteLine(sprintf "- Throughput: %.2f ops/sec" result.ThroughputPerSecond)
                writer.WriteLine(sprintf "- Memory usage: %s" (MetricFormatter.formatValue (float result.MemoryUsage) Bytes))
                
                if result.MinTime <> TimeSpan.Zero && result.MaxTime <> TimeSpan.Zero then
                    let minFormatted = MetricFormatter.formatDuration result.MinTime
                    let maxFormatted = MetricFormatter.formatDuration result.MaxTime
                    writer.WriteLine(sprintf "- Time range: %s - %s" minFormatted maxFormatted)
            else
                match result.ErrorMessage with
                | Some error -> writer.WriteLine(sprintf "- Error: %s" error)
                | None -> writer.WriteLine("- Error: Unknown error occurred")
            
            writer.WriteLine("")
    
    // CSS 스타일 헬퍼
    // CSS 스타일 헬퍼
    member private _.GetCssStyles() : string =
        String.concat "\n" [
        "body { "
        "    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; "
        "    margin: 0; "
        "    padding: 20px; "
        "    background-color: #f5f5f5; "
        "}"
        ".header { "
        "    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); "
        "    color: white; "
        "    padding: 30px; "
        "    border-radius: 10px; "
        "    margin-bottom: 30px; "
        "}"
        ".header h1 { "
        "    margin: 0 0 10px 0; "
        "    font-size: 2.5em; "
        "}"
        ".period, .duration { "
        "    margin: 5px 0; "
        "    opacity: 0.9; "
        "}"
        ".section { "
        "    background: white; "
        "    padding: 25px; "
        "    margin-bottom: 25px; "
        "    border-radius: 10px; "
        "    box-shadow: 0 2px 10px rgba(0,0,0,0.1); "
        "}"
        ".section h2 { "
        "    margin-top: 0; "
        "    color: #333; "
        "    border-bottom: 2px solid #eee; "
        "    padding-bottom: 10px; "
        "}"
        ".metrics-grid { "
        "    display: grid; "
        "    grid-template-columns: repeat(auto-fill, minmax(300px, 1fr)); "
        "    gap: 20px; "
        "}"
        ".metric-card { "
        "    border: 1px solid #ddd; "
        "    border-radius: 8px; "
        "    padding: 20px; "
        "    background: #fafafa; "
        "}"
        ".metric-card h3 { "
        "    margin: 0 0 15px 0; "
        "    color: #555; "
        "}"
        ".current-value { "
        "    font-size: 2em; "
        "    font-weight: bold; "
        "    color: #667eea; "
        "    margin-bottom: 15px; "
        "}"
        ".metric-stats div { "
        "    margin: 5px 0; "
        "    color: #666; "
        "}"
        ".trends { "
        "    display: grid; "
        "    gap: 15px; "
        "}"
        ".trend-item { "
        "    display: grid; "
        "    grid-template-columns: 2fr 1fr 1fr 3fr; "
        "    gap: 15px; "
        "    align-items: center; "
        "    padding: 15px; "
        "    background: #f9f9f9; "
        "    border-radius: 5px; "
        "}"
        ".trend-name { "
        "    font-weight: bold; "
        "}"
        ".trend-change { "
        "    font-weight: bold; "
        "    padding: 5px 10px; "
        "    border-radius: 3px; "
        "}"
        ".trend-up { "
        "    background: #ffebee; "
        "    color: #d32f2f; "
        "}"
        ".trend-down { "
        "    background: #e8f5e8; "
        "    color: #388e3c; "
        "}"
        ".trend-stable { "
        "    background: #e3f2fd; "
        "    color: #1976d2; "
        "}"
        ".trend-unknown { "
        "    background: #f3e5f5; "
        "    color: #7b1fa2; "
        "}"
        ".anomalies h3 { "
        "    color: #d32f2f; "
        "    margin-top: 25px; "
        "}"
        ".anomaly-item { "
        "    display: grid; "
        "    grid-template-columns: 200px 100px 1fr; "
        "    gap: 15px; "
        "    align-items: center; "
        "    padding: 10px; "
        "    margin: 10px 0; "
        "    background: #fff3e0; "
        "    border-left: 4px solid #ff9800; "
        "    border-radius: 3px; "
        "}"
        ".anomaly-time { "
        "    font-family: monospace; "
        "    font-size: 0.9em; "
        "}"
        ".anomaly-severity { "
        "    padding: 3px 8px; "
        "    border-radius: 3px; "
        "    font-size: 0.8em; "
        "    font-weight: bold; "
        "}"
        ".severity-critical { "
        "    background: #ffebee; "
        "    color: #d32f2f; "
        "}"
        ".severity-warning { "
        "    background: #fff8e1; "
        "    color: #f57c00; "
        "}"
        ".severity-info { "
        "    background: #e3f2fd; "
        "    color: #1976d2; "
        "}"
        ".no-anomalies { "
        "    text-align: center; "
        "    padding: 30px; "
        "    color: #4caf50; "
        "    font-size: 1.2em; "
        "}"
    ]