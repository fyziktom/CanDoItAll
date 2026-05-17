using System.Security.Cryptography;
using System.Text;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed class CognitiveMemoryScoreGeometryDriver(ICognitiveMemoryScoreSpaceRegistry scoreSpaceRegistry) : ICognitiveMemoryScoreGeometryDriver
{
    private const double EqualityTolerance = 0.000001d;

    public async ValueTask<CognitiveMemoryScoreEvaluationTrace> EvaluateAsync(
        CognitiveMemoryScoreEvaluationRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.InputVectors);
        ArgumentNullException.ThrowIfNull(request.CandidateShapes);

        var definition = await scoreSpaceRegistry.GetDefinitionAsync(
            request.SpaceKind,
            request.SchemaVersion,
            cancellationToken);

        var inputVector = GetPrimaryVector(request);
        EnsureCandidateShapesMatchRequest(request);

        var missingRequiredDimensions = FindMissingRequiredDimensions(definition, request.InputVectors);
        var matchedShapes = missingRequiredDimensions.Count == 0
            ? request.CandidateShapes.Where(shape => ShapeMatches(shape, request.InputVectors)).ToList()
            : [];
        var scalarProjection = CreateScalarProjection(definition, inputVector, matchedShapes, missingRequiredDimensions);
        var explanation = CreateExplanation(missingRequiredDimensions, matchedShapes, scalarProjection);
        var traceId = CreateTraceId(request, missingRequiredDimensions, matchedShapes, scalarProjection);

        return new CognitiveMemoryScoreEvaluationTrace(
            traceId,
            request.ProjectId,
            request.OwnerKind,
            request.OwnerId,
            request.SpaceKind,
            request.SchemaVersion,
            request.InputVectors,
            matchedShapes,
            missingRequiredDimensions,
            scalarProjection,
            explanation,
            definition.AlgorithmVersion,
            inputVector.CalculatedAtUtc);
    }

    private static CognitiveMemoryScoreVectorSnapshot GetPrimaryVector(CognitiveMemoryScoreEvaluationRequest request)
    {
        var inputVector = request.InputVectors.FirstOrDefault(vector =>
            vector.SpaceKind == request.SpaceKind &&
            vector.SchemaVersion == request.SchemaVersion);

        return inputVector ?? throw new InvalidOperationException(
            $"Score evaluation for '{request.SpaceKind}' schema '{request.SchemaVersion}' requires at least one matching input vector.");
    }

    private static void EnsureCandidateShapesMatchRequest(CognitiveMemoryScoreEvaluationRequest request)
    {
        var invalidShape = request.CandidateShapes.FirstOrDefault(shape =>
            shape.SpaceKind != request.SpaceKind ||
            shape.SchemaVersion != request.SchemaVersion);
        if (invalidShape is not null)
        {
            throw new InvalidOperationException(
                $"Score shape '{invalidShape.ShapeKind}' belongs to '{invalidShape.SpaceKind}' schema '{invalidShape.SchemaVersion}', not request '{request.SpaceKind}' schema '{request.SchemaVersion}'.");
        }
    }

    private static IReadOnlyList<CognitiveMemoryMissingScoreDimension> FindMissingRequiredDimensions(
        CognitiveMemoryScoreSpaceDefinition definition,
        IReadOnlyList<CognitiveMemoryScoreVectorSnapshot> inputVectors)
    {
        var missing = new List<CognitiveMemoryMissingScoreDimension>();
        foreach (var dimension in definition.Dimensions.Where(dimension => dimension.Required))
        {
            if (FindComponent(inputVectors, dimension.Kind) is null)
            {
                missing.Add(new CognitiveMemoryMissingScoreDimension(
                    dimension.Kind,
                    CognitiveMemoryScoreMissingDimensionReason.NotObserved));
            }
        }

        return missing;
    }

    private static CognitiveMemoryScoreComponent? FindComponent(
        IReadOnlyList<CognitiveMemoryScoreVectorSnapshot> inputVectors,
        CognitiveMemoryScoreDimensionKind dimensionKind)
    {
        foreach (var vector in inputVectors)
        {
            foreach (var component in vector.Components)
            {
                if (component.DimensionKind == dimensionKind)
                {
                    return component;
                }
            }
        }

        return null;
    }

    private static bool ShapeMatches(
        CognitiveMemoryScoreShapeSnapshot shape,
        IReadOnlyList<CognitiveMemoryScoreVectorSnapshot> inputVectors)
        => shape.ShapeKind switch
        {
            CognitiveMemoryScoreShapeKind.PointVector => MatchesPoint(shape, inputVectors),
            CognitiveMemoryScoreShapeKind.CentroidRadius => MatchesCentroidRadius(shape, inputVectors),
            CognitiveMemoryScoreShapeKind.WeightedRegion => MatchesBoundedRegion(shape, inputVectors),
            CognitiveMemoryScoreShapeKind.ThresholdEnvelope => MatchesBoundedRegion(shape, inputVectors),
            CognitiveMemoryScoreShapeKind.BoundaryPlane => MatchesBoundedRegion(shape, inputVectors),
            CognitiveMemoryScoreShapeKind.ParetoFrontier => MatchesBoundedRegion(shape, inputVectors),
            CognitiveMemoryScoreShapeKind.TimeDecayedTrajectory => MatchesBoundedRegion(shape, inputVectors),
            _ => false
        };

    private static bool MatchesPoint(
        CognitiveMemoryScoreShapeSnapshot shape,
        IReadOnlyList<CognitiveMemoryScoreVectorSnapshot> inputVectors)
    {
        foreach (var shapeComponent in shape.Components)
        {
            var component = FindComponent(inputVectors, shapeComponent.DimensionKind);
            if (component is null || Math.Abs(component.NormalizedValue - shapeComponent.Center) > EqualityTolerance)
            {
                return false;
            }
        }

        return true;
    }

    private static bool MatchesCentroidRadius(
        CognitiveMemoryScoreShapeSnapshot shape,
        IReadOnlyList<CognitiveMemoryScoreVectorSnapshot> inputVectors)
    {
        if (shape.Radius is null)
        {
            return false;
        }

        var weightedDistance = 0d;
        var totalWeight = 0d;
        foreach (var shapeComponent in shape.Components)
        {
            var component = FindComponent(inputVectors, shapeComponent.DimensionKind);
            if (component is null)
            {
                return false;
            }

            var weight = shapeComponent.Weight == 0 ? 1 : shapeComponent.Weight;
            var difference = component.NormalizedValue - shapeComponent.Center;
            weightedDistance += weight * difference * difference;
            totalWeight += weight;
        }

        if (totalWeight == 0)
        {
            return false;
        }

        var normalizedDistance = Math.Sqrt(weightedDistance / totalWeight);
        return normalizedDistance <= shape.Radius.Value;
    }

    private static bool MatchesBoundedRegion(
        CognitiveMemoryScoreShapeSnapshot shape,
        IReadOnlyList<CognitiveMemoryScoreVectorSnapshot> inputVectors)
    {
        foreach (var shapeComponent in shape.Components)
        {
            var component = FindComponent(inputVectors, shapeComponent.DimensionKind);
            if (component is null)
            {
                return false;
            }

            if (shapeComponent.LowerBound is not null && component.NormalizedValue < shapeComponent.LowerBound)
            {
                return false;
            }

            if (shapeComponent.UpperBound is not null && component.NormalizedValue > shapeComponent.UpperBound)
            {
                return false;
            }
        }

        return true;
    }

    private static CognitiveMemoryScoreScalarProjection? CreateScalarProjection(
        CognitiveMemoryScoreSpaceDefinition definition,
        CognitiveMemoryScoreVectorSnapshot inputVector,
        IReadOnlyList<CognitiveMemoryScoreShapeSnapshot> matchedShapes,
        IReadOnlyList<CognitiveMemoryMissingScoreDimension> missingRequiredDimensions)
    {
        if (definition.ScalarProjectionKind == CognitiveMemoryScoreScalarProjectionKind.None)
        {
            return null;
        }

        if (missingRequiredDimensions.Count > 0)
        {
            return new CognitiveMemoryScoreScalarProjection(
                definition.ScalarProjectionKind,
                CognitiveMemoryScoreProjectionBucket.Reject,
                null,
                null,
                $"Missing required dimensions: {string.Join(", ", missingRequiredDimensions.Select(dimension => dimension.DimensionKind))}.");
        }

        var displayScore = ComputeDisplayScore(definition, inputVector);
        var bucket = GetBucket(matchedShapes, displayScore);
        return new CognitiveMemoryScoreScalarProjection(
            definition.ScalarProjectionKind,
            bucket,
            displayScore,
            null,
            matchedShapes.Count == 0
                ? "Projection derived from score vector components."
                : $"Projection derived from {matchedShapes.Count} matched score shape(s).");
    }

    private static double? ComputeDisplayScore(
        CognitiveMemoryScoreSpaceDefinition definition,
        CognitiveMemoryScoreVectorSnapshot inputVector)
    {
        var weightedValue = 0d;
        var totalWeight = 0d;
        foreach (var dimension in definition.Dimensions)
        {
            var component = inputVector.Components.FirstOrDefault(component => component.DimensionKind == dimension.Kind);
            if (component is null)
            {
                continue;
            }

            var contribution = dimension.HigherIsBetter
                ? component.NormalizedValue
                : 1 - component.NormalizedValue;
            weightedValue += contribution * dimension.DefaultWeight * component.Confidence;
            totalWeight += dimension.DefaultWeight * component.Confidence;
        }

        return totalWeight == 0
            ? null
            : Math.Clamp(weightedValue / totalWeight, 0, 1);
    }

    private static CognitiveMemoryScoreProjectionBucket GetBucket(
        IReadOnlyList<CognitiveMemoryScoreShapeSnapshot> matchedShapes,
        double? displayScore)
    {
        var shapeBucket = matchedShapes
            .Select(shape => shape.ProjectionBucket)
            .Where(bucket => bucket != CognitiveMemoryScoreProjectionBucket.Unknown)
            .OrderByDescending(GetBucketSeverity)
            .FirstOrDefault();

        if (shapeBucket != CognitiveMemoryScoreProjectionBucket.Unknown)
        {
            return shapeBucket;
        }

        if (displayScore is null)
        {
            return CognitiveMemoryScoreProjectionBucket.NeedsReview;
        }

        if (displayScore >= 0.75)
        {
            return CognitiveMemoryScoreProjectionBucket.StrongAccept;
        }

        return displayScore >= 0.5
            ? CognitiveMemoryScoreProjectionBucket.WeakAccept
            : CognitiveMemoryScoreProjectionBucket.NeedsReview;
    }

    private static int GetBucketSeverity(CognitiveMemoryScoreProjectionBucket bucket)
        => bucket switch
        {
            CognitiveMemoryScoreProjectionBucket.Reject => 70,
            CognitiveMemoryScoreProjectionBucket.Abstain => 60,
            CognitiveMemoryScoreProjectionBucket.Inhibit => 50,
            CognitiveMemoryScoreProjectionBucket.NeedsReview => 40,
            CognitiveMemoryScoreProjectionBucket.NeedsClarification => 30,
            CognitiveMemoryScoreProjectionBucket.WeakAccept => 20,
            CognitiveMemoryScoreProjectionBucket.StrongAccept => 10,
            _ => 0
        };

    private static string CreateExplanation(
        IReadOnlyList<CognitiveMemoryMissingScoreDimension> missingRequiredDimensions,
        IReadOnlyList<CognitiveMemoryScoreShapeSnapshot> matchedShapes,
        CognitiveMemoryScoreScalarProjection? scalarProjection)
    {
        if (missingRequiredDimensions.Count > 0)
        {
            return $"Evaluation rejected because required dimensions are missing: {string.Join(", ", missingRequiredDimensions.Select(dimension => dimension.DimensionKind))}.";
        }

        if (matchedShapes.Count > 0)
        {
            return string.Join(" | ", matchedShapes.Select(shape => shape.Explanation));
        }

        return scalarProjection is null
            ? "Evaluation completed without scalar projection."
            : "Evaluation completed with scalar projection derived from vector components.";
    }

    private static CognitiveMemoryScoreEvaluationId CreateTraceId(
        CognitiveMemoryScoreEvaluationRequest request,
        IReadOnlyList<CognitiveMemoryMissingScoreDimension> missingRequiredDimensions,
        IReadOnlyList<CognitiveMemoryScoreShapeSnapshot> matchedShapes,
        CognitiveMemoryScoreScalarProjection? scalarProjection)
    {
        var raw = new StringBuilder()
            .Append(request.ProjectId?.ToString("D"))
            .Append('|')
            .Append(request.OwnerKind)
            .Append('|')
            .Append(request.OwnerId?.ToString("D"))
            .Append('|')
            .Append(request.SpaceKind)
            .Append('|')
            .Append(request.SchemaVersion)
            .Append('|')
            .Append(string.Join(",", request.InputVectors.Select(vector => vector.InputHash.Value)))
            .Append('|')
            .Append(string.Join(",", matchedShapes.Select(shape => shape.Explanation)))
            .Append('|')
            .Append(string.Join(",", missingRequiredDimensions.Select(dimension => dimension.DimensionKind)))
            .Append('|')
            .Append(scalarProjection?.Bucket)
            .ToString();

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return new CognitiveMemoryScoreEvaluationId(new Guid(hash.AsSpan(0, 16)));
    }
}
