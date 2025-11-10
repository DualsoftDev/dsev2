namespace Ev2.Cpu.Perf.Analysis

open System
open System.Collections.Generic
open System.Linq
open Ev2.Cpu.Perf.Core

/// 성능 분석기
type PerformanceAnalyzer(store: MetricStore) =
    
    let calculateImpact (regressionType: RegressionType) (changePercent: float) =
        match regressionType with
        | PerformanceRegression -> 
            if abs changePercent > 50.0 then "High impact on user experience"
            elif abs changePercent > 25.0 then "Moderate impact on performance"
            else "Minor performance impact"
        | ResourceRegression -> 
            if abs changePercent > 50.0 then "Significant resource usage increase"
            elif abs changePercent > 25.0 then "Moderate resource impact"
            else "Minor resource increase"
        | QualityRegression -> 
            if abs changePercent > 50.0 then "Critical quality degradation"
            elif abs changePercent > 25.0 then "Notable quality issues"
            else "Minor quality impact"

    let generateBottleneckRecommendation (metricName: string) (score: float) =
        match metricName.ToLower() with
        | name when name.Contains("cpu") -> 
            "Consider CPU optimization or load balancing"
        | name when name.Contains("memory") -> 
            "Investigate memory leaks or optimize memory usage"
        | name when name.Contains("scan") || name.Contains("time") -> 
            "Optimize scan algorithms or reduce statement complexity"
        | name when name.Contains("queue") -> 
            "Increase processing capacity or optimize queue management"
        | _ -> 
            "Monitor metric and investigate root cause"

    /// 성능 트렌드 분석
    member this.AnalyzeTrend(metricName: string, duration: TimeSpan) =
        let endTime = DateTime.UtcNow
        let startTime = endTime.Subtract(duration)
        let values = store.GetValuesInRange(metricName, startTime, endTime)
        
        if List.length values < 2 then
            {
                MetricName = metricName
                Period = duration
                Trend = NoTrend
                ChangeRate = 0.0
                Confidence = Low
                Recommendation = "Insufficient data for trend analysis"
            }
        else
            let sortedValues = values |> List.sortBy (fun v -> v.Timestamp)
            let firstHalf = sortedValues |> List.take (List.length sortedValues / 2)
            let secondHalf = sortedValues |> List.skip (List.length sortedValues / 2)
            
            let firstAvg = firstHalf |> List.map (fun v -> v.Value) |> List.average
            let secondAvg = secondHalf |> List.map (fun v -> v.Value) |> List.average
            
            let changeRate = if firstAvg <> 0.0 then (secondAvg - firstAvg) / firstAvg * 100.0 else 0.0
            
            let trend = 
                if abs changeRate < 5.0 then Stable
                elif changeRate > 0.0 then Increasing
                else Decreasing
            
            let confidence = 
                if List.length values >= 50 then High
                elif List.length values >= 20 then Medium
                else Low
            
            let recommendation = 
                match trend with
                | Increasing when metricName.Contains("Error") || metricName.Contains("Memory") -> 
                    "Investigate increasing trend - potential issue"
                | Decreasing when metricName.Contains("Performance") || metricName.Contains("Throughput") -> 
                    "Investigate decreasing performance"
                | Stable -> "Trend is stable - no immediate action needed"
                | _ -> "Monitor trend for changes"
            
            {
                MetricName = metricName
                Period = duration
                Trend = trend
                ChangeRate = changeRate
                Confidence = confidence
                Recommendation = recommendation
            }
    
    /// 이상 감지
    member this.DetectAnomalies(metricName: string, duration: TimeSpan, sensitivityFactor: float) =
        let endTime = DateTime.UtcNow
        let startTime = endTime.Subtract(duration)
        let values = store.GetValuesInRange(metricName, startTime, endTime)
        
        if List.length values < 10 then []
        else
            let numericValues = values |> List.map (fun v -> v.Value)
            let mean = List.average numericValues
            let stdDev = 
                let variance = numericValues |> List.map (fun x -> (x - mean) ** 2.0) |> List.average
                sqrt variance
            
            let threshold = stdDev * sensitivityFactor
            
            values
            |> List.choose (fun v ->
                let deviation = abs (v.Value - mean)
                if deviation > threshold then
                    Some {
                        Timestamp = v.Timestamp
                        MetricName = metricName
                        Value = v.Value
                        ExpectedValue = mean
                        Deviation = deviation
                        Severity = 
                            if deviation > threshold * 2.0 then Critical
                            elif deviation > threshold * 1.5 then Warning
                            else Info
                        Description = $"Value {v.Value:F2} deviates by {deviation:F2} from expected {mean:F2}"
                    }
                else None
            )
    
    /// 성능 회귀 분석
    member this.DetectRegression(metricName: string, baselineDuration: TimeSpan, currentDuration: TimeSpan) =
        let now = DateTime.UtcNow
        let baselineStart = now.Subtract(baselineDuration).Subtract(currentDuration)
        let baselineEnd = now.Subtract(currentDuration)
        let currentStart = now.Subtract(currentDuration)
        
        let baselineValues = store.GetValuesInRange(metricName, baselineStart, baselineEnd)
        let currentValues = store.GetValuesInRange(metricName, currentStart, now)
        
        if List.isEmpty baselineValues || List.isEmpty currentValues then
            None
        else
            let baselineAvg = baselineValues |> List.map (fun v -> v.Value) |> List.average
            let currentAvg = currentValues |> List.map (fun v -> v.Value) |> List.average
            
            let changePercent = if baselineAvg <> 0.0 then (currentAvg - baselineAvg) / baselineAvg * 100.0 else 0.0
            
            let regressionType = 
                match metricName.ToLower() with
                | name when name.Contains("time") || name.Contains("latency") || name.Contains("duration") ->
                    if changePercent > 10.0 then Some PerformanceRegression else None
                | name when name.Contains("throughput") || name.Contains("rate") || name.Contains("speed") ->
                    if changePercent < -10.0 then Some PerformanceRegression else None
                | name when name.Contains("memory") || name.Contains("cpu") ->
                    if changePercent > 20.0 then Some ResourceRegression else None
                | name when name.Contains("error") || name.Contains("failure") ->
                    if changePercent > 5.0 then Some QualityRegression else None
                | _ -> None
            
            regressionType |> Option.map (fun regType ->
                {
                    MetricName = metricName
                    RegressionType = regType
                    BaselineValue = baselineAvg
                    CurrentValue = currentAvg
                    ChangePercent = changePercent
                    DetectionTime = now
                    Severity = 
                        if abs changePercent > 50.0 then Critical
                        elif abs changePercent > 25.0 then Warning
                        else Info
                    Impact = calculateImpact regType changePercent
                }
            )
    
    /// 상관관계 분석
    member this.AnalyzeCorrelation(metric1: string, metric2: string, duration: TimeSpan) =
        let endTime = DateTime.UtcNow
        let startTime = endTime.Subtract(duration)
        
        let values1 = store.GetValuesInRange(metric1, startTime, endTime)
        let values2 = store.GetValuesInRange(metric2, startTime, endTime)
        
        if List.length values1 < 5 || List.length values2 < 5 then
            {
                Metric1 = metric1
                Metric2 = metric2
                Correlation = 0.0
                Strength = NoCorrelation
                Period = duration
                SampleSize = 0
                Significance = Low
            }
        else
            // 시간 기준으로 정렬하고 가장 가까운 값들 매칭
            let sorted1 = values1 |> List.sortBy (fun v -> v.Timestamp)
            let sorted2 = values2 |> List.sortBy (fun v -> v.Timestamp)
            
            let pairs = this.MatchValuesByTime(sorted1, sorted2)
            
            if List.length pairs < 3 then
                {
                    Metric1 = metric1
                    Metric2 = metric2
                    Correlation = 0.0
                    Strength = NoCorrelation
                    Period = duration
                    SampleSize = List.length pairs
                    Significance = Low
                }
            else
                let correlation = this.CalculatePearsonCorrelation(pairs)
                let strength = 
                    let absCorr = abs correlation
                    if absCorr >= 0.8 then Strong
                    elif absCorr >= 0.5 then Moderate
                    elif absCorr >= 0.3 then Weak
                    else NoCorrelation
                
                let significance = 
                    if List.length pairs >= 30 then High
                    elif List.length pairs >= 15 then Medium
                    else Low
                
                {
                    Metric1 = metric1
                    Metric2 = metric2
                    Correlation = correlation
                    Strength = strength
                    Period = duration
                    SampleSize = List.length pairs
                    Significance = significance
                }
    
    /// 성능 병목 식별
    member this.IdentifyBottlenecks(duration: TimeSpan) =
        let endTime = DateTime.UtcNow
        let startTime = endTime.Subtract(duration)
        let metricNames = store.GetMetricNames()
        
        metricNames
        |> List.choose (fun metricName ->
            let values = store.GetValuesInRange(metricName, startTime, endTime)
            if List.isEmpty values then None
            else
                let avgValue = values |> List.map (fun v -> v.Value) |> List.average
                let maxValue = values |> List.map (fun v -> v.Value) |> List.max
                
                // 병목 점수 계산 (메트릭 타입에 따라 다름)
                let bottleneckScore = 
                    match metricName.ToLower() with
                    | name when name.Contains("time") || name.Contains("latency") -> 
                        if avgValue > 100.0 then avgValue / 10.0 else 0.0
                    | name when name.Contains("cpu") || name.Contains("memory") -> 
                        if avgValue > 80.0 then avgValue else 0.0
                    | name when name.Contains("queue") || name.Contains("wait") -> 
                        if avgValue > 10.0 then avgValue * 5.0 else 0.0
                    | _ -> 0.0
                
                if bottleneckScore > 10.0 then
                    Some {
                        MetricName = metricName
                        BottleneckScore = bottleneckScore
                        AverageValue = avgValue
                        PeakValue = maxValue
                        ImpactLevel = 
                            if bottleneckScore > 80.0 then Critical
                            elif bottleneckScore > 50.0 then Warning
                            else Info
                        Recommendation = generateBottleneckRecommendation metricName bottleneckScore
                    }
                else None
        )
        |> List.sortByDescending (fun b -> b.BottleneckScore)
    
    // 개인 헬퍼 메서드들
    member private _.MatchValuesByTime(values1: MetricValue list, values2: MetricValue list) =
        let tolerance = TimeSpan.FromSeconds(30.0) // 30초 허용 오차
        
        values1
        |> List.choose (fun v1 ->
            values2
            |> List.tryFind (fun v2 -> abs ((v1.Timestamp - v2.Timestamp).TotalSeconds) <= tolerance.TotalSeconds)
            |> Option.map (fun v2 -> v1.Value, v2.Value)
        )
    
    member private _.CalculatePearsonCorrelation(pairs: (float * float) list) =
        if List.length pairs < 2 then 0.0
        else
            let x = pairs |> List.map fst
            let y = pairs |> List.map snd
            
            let meanX = List.average x
            let meanY = List.average y
            
            let numerator = List.map2 (fun xi yi -> (xi - meanX) * (yi - meanY)) x y |> List.sum
            let denomX = x |> List.map (fun xi -> (xi - meanX) ** 2.0) |> List.sum |> sqrt
            let denomY = y |> List.map (fun yi -> (yi - meanY) ** 2.0) |> List.sum |> sqrt
            
            if denomX = 0.0 || denomY = 0.0 then 0.0
            else numerator / (denomX * denomY)

// 분석 결과 타입들
and TrendAnalysisResult = {
    MetricName: string
    Period: TimeSpan
    Trend: TrendDirection
    ChangeRate: float
    Confidence: ConfidenceLevel
    Recommendation: string
}

and TrendDirection = 
    | Increasing 
    | Decreasing 
    | Stable 
    | NoTrend

and ConfidenceLevel = 
    | High 
    | Medium 
    | Low

and AnomalyDetection = {
    Timestamp: DateTime
    MetricName: string
    Value: float
    ExpectedValue: float
    Deviation: float
    Severity: AlertSeverity
    Description: string
}

and RegressionDetection = {
    MetricName: string
    RegressionType: RegressionType
    BaselineValue: float
    CurrentValue: float
    ChangePercent: float
    DetectionTime: DateTime
    Severity: AlertSeverity
    Impact: string
}

and RegressionType = 
    | PerformanceRegression 
    | ResourceRegression 
    | QualityRegression

and CorrelationAnalysis = {
    Metric1: string
    Metric2: string
    Correlation: float
    Strength: CorrelationStrength
    Period: TimeSpan
    SampleSize: int
    Significance: ConfidenceLevel
}

and CorrelationStrength = 
    | Strong 
    | Moderate 
    | Weak 
    | NoCorrelation

and BottleneckIdentification = {
    MetricName: string
    BottleneckScore: float
    AverageValue: float
    PeakValue: float
    ImpactLevel: AlertSeverity
    Recommendation: string
}

// 헬퍼 함수들은 PerformanceAnalyzer 내부에 정의되었습니다.
