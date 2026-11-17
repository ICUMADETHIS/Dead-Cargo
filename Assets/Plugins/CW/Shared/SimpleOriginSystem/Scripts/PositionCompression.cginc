float2 _CW_PositionCompressionData; // x = start distance, y = max distance limit

#define DEBUG_SCALE 0.001
#define SSS_LOG_K          0.0001
#define SSS_MAX_WORLD_DIST 1e27

float SSS_GetCompressionRatio(float worldDist)
{
    float startDist = _CW_PositionCompressionData.x;
    float maxDist   = _CW_PositionCompressionData.y;

    // no compression nearby
    if (worldDist <= startDist)
        return 1.0;

    float excess = worldDist - startDist;

    float compressedDist =
        startDist +
        (maxDist - startDist) *
        log(1.0 + excess * SSS_LOG_K) /
        log(1.0 + SSS_MAX_WORLD_DIST * SSS_LOG_K);

    return compressedDist / worldDist;
}

float SSS_DecompressWorldDist(float compressedDist)
{
    float startDist = _CW_PositionCompressionData.x;
    float maxDist   = _CW_PositionCompressionData.y;

    if (compressedDist <= startDist)
        return compressedDist;

    float t =
        saturate(
            (compressedDist - startDist) /
            (maxDist - startDist)
        );

    return startDist +
        (
            exp(
                t *
                log(1.0 + SSS_MAX_WORLD_DIST * SSS_LOG_K)
            ) - 1.0
        ) / SSS_LOG_K;
}

float3 SSS_CompressWorld(float3 worldPos, float3 worldCenter, float worldRadius, out float ratio) 
{
    float3 worldDelta = worldPos - _WorldSpaceCameraPos;
    float  farDist    = distance(_WorldSpaceCameraPos, worldCenter) + worldRadius;
    
    ratio = SSS_GetCompressionRatio(farDist);
    
    return _WorldSpaceCameraPos + worldDelta * ratio;
}

float3 SSS_CompressWorld(float3 worldPos) 
{
    float3 worldDelta = worldPos - _WorldSpaceCameraPos;
    float  worldDist  = length(worldDelta);
    
    return _WorldSpaceCameraPos + worldDelta * SSS_GetCompressionRatio(worldDist);
}

float3 SSS_DecompressWorld(float3 worldPos)
{
    float3 worldDelta = worldPos - _WorldSpaceCameraPos;
    float  worldDist  = length(worldDelta);
    
    return worldDist > 0.001 ? _WorldSpaceCameraPos + (worldDelta / worldDist) * SSS_DecompressWorldDist(worldDist) : _WorldSpaceCameraPos;
}

float3 SSS_DecompressWorld(float3 worldPos, float ratio) 
{
    float3 worldDelta = worldPos - _WorldSpaceCameraPos;
    return _WorldSpaceCameraPos + worldDelta / ratio;
}

float SSS_ElongateDistance(float worldDist)
{
	float minDist = 10000.0;
	float maxDist = 100000.0;
	float scale   = saturate((worldDist - minDist) / (maxDist - minDist));

	return worldDist * lerp(1.0, 1.2, scale);
}