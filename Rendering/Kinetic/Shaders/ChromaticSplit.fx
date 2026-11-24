sampler2D input : register(s0);

float SplitAmount : register(c0);
float RadialBias : register(c1);
float MicroJitter : register(c2);
float DisplayBrightness : register(c3);
float DisplayContrast : register(c4);

float4 main(float2 uv : TEXCOORD) : COLOR
{
    if (SplitAmount <= 0.000001)
    {
        float4 source = tex2D(input, saturate(uv));
        float3 adjusted = ((source.rgb - 0.5) * DisplayContrast + 0.5) * DisplayBrightness;
        return float4(saturate(adjusted), source.a);
    }

    float2 centerDelta = uv - float2(0.5, 0.5);
    float radius = length(centerDelta);
    float2 radialDirection = radius > 0.0001
        ? centerDelta / radius
        : float2(0.0, 0.0);

    float edgeFactor = smoothstep(0.0, 0.62, radius);
    float radialScale = lerp(1.0, lerp(0.12, 1.0, edgeFactor), saturate(RadialBias));
    float amount = min(max(SplitAmount, 0.0), 0.012) * radialScale;

    float jitterWave = sin((uv.x + uv.y) * 157.0) * cos((uv.x - uv.y) * 113.0);
    float jitter = amount * saturate(MicroJitter) * 0.12 * jitterWave;
    float2 redOffset = radialDirection * amount + float2(jitter, 0.0);
    float2 greenOffset = float2(-amount * 0.18 - jitter * 0.25, 0.0);
    float2 blueOffset = -radialDirection * amount - float2(jitter, 0.0);

    float red = tex2D(input, saturate(uv + redOffset)).r;
    float green = tex2D(input, saturate(uv + greenOffset)).g;
    float blue = tex2D(input, saturate(uv + blueOffset)).b;
    float alpha = tex2D(input, saturate(uv)).a;

    float3 color = float3(red, green, blue);
    color = ((color - 0.5) * DisplayContrast + 0.5) * DisplayBrightness;
    return float4(saturate(color), alpha);
}
