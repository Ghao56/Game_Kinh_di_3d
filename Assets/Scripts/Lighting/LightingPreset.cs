using System;
using UnityEngine;

[Serializable]
public class LightingPreset
{
    [Header("Ambient (Trilight)")]
    public Color ambientSky = new Color(0.55f, 0.62f, 0.75f, 1f);
    public Color ambientEquator = new Color(0.45f, 0.45f, 0.42f, 1f);
    public Color ambientGround = new Color(0.25f, 0.22f, 0.18f, 1f);
    [Range(0f, 2f)] public float ambientIntensity = 1f;

    [Header("Sun / Moon")]
    [Min(0f)] public float sunIntensity = 1.2f;
    public Color sunColor = Color.white;
    public Vector3 sunEuler = new Vector3(50f, -30f, 0f);

    [Header("Skybox (Procedural)")]
    public Color skyTint = new Color(0.5f, 0.5f, 0.5f, 1f);
    public Color skyGround = new Color(0.35f, 0.3f, 0.25f, 1f);
    [Range(0f, 5f)] public float atmosphereThickness = 1f;
    [Range(0f, 8f)] public float skyExposure = 1.3f;

    [Header("Fog (RenderSettings)")]
    public Color fogColor = new Color(0.7f, 0.75f, 0.8f, 1f);
    [Range(0f, 0.1f)] public float fogDensity = 0.008f;

    [Header("Reflection")]
    [Tooltip("Gán cho RenderSettings.reflectionIntensity. Cubemap phản chiếu mặc định không tự cập nhật theo skybox lúc chạy, nên Night cần hạ xuống để không phản chiếu trời ban ngày.")]
    [Range(0f, 1f)] public float reflectionIntensity = 1f;

    [Header("Post (Color Adjustments)")]
    [Range(-10f, 10f)] public float postExposure = 0f;
    [Range(-100f, 100f)] public float postContrast = 0f;
    [Range(-100f, 100f)] public float postSaturation = 0f;
    [Range(-180f, 180f)] public float postHueShift = 0f;
    public Color postFilter = Color.white;
    [Tooltip("Blend từ trắng sang postFilter. 0 = không lọc màu.")]
    [Range(0f, 1f)] public float postFilterAmount = 0f;

    public void CopyFrom(LightingPreset other)
    {
        ambientSky = other.ambientSky;
        ambientEquator = other.ambientEquator;
        ambientGround = other.ambientGround;
        ambientIntensity = other.ambientIntensity;

        sunIntensity = other.sunIntensity;
        sunColor = other.sunColor;
        sunEuler = other.sunEuler;

        skyTint = other.skyTint;
        skyGround = other.skyGround;
        atmosphereThickness = other.atmosphereThickness;
        skyExposure = other.skyExposure;

        fogColor = other.fogColor;
        fogDensity = other.fogDensity;

        reflectionIntensity = other.reflectionIntensity;

        postExposure = other.postExposure;
        postContrast = other.postContrast;
        postSaturation = other.postSaturation;
        postHueShift = other.postHueShift;
        postFilter = other.postFilter;
        postFilterAmount = other.postFilterAmount;
    }

    public void Lerp(LightingPreset a, LightingPreset b, float t)
    {
        ambientSky = Color.Lerp(a.ambientSky, b.ambientSky, t);
        ambientEquator = Color.Lerp(a.ambientEquator, b.ambientEquator, t);
        ambientGround = Color.Lerp(a.ambientGround, b.ambientGround, t);
        ambientIntensity = Mathf.Lerp(a.ambientIntensity, b.ambientIntensity, t);

        sunIntensity = Mathf.Lerp(a.sunIntensity, b.sunIntensity, t);
        sunColor = Color.Lerp(a.sunColor, b.sunColor, t);
        sunEuler = Vector3.Lerp(a.sunEuler, b.sunEuler, t);

        skyTint = Color.Lerp(a.skyTint, b.skyTint, t);
        skyGround = Color.Lerp(a.skyGround, b.skyGround, t);
        atmosphereThickness = Mathf.Lerp(a.atmosphereThickness, b.atmosphereThickness, t);
        skyExposure = Mathf.Lerp(a.skyExposure, b.skyExposure, t);

        fogColor = Color.Lerp(a.fogColor, b.fogColor, t);
        fogDensity = Mathf.Lerp(a.fogDensity, b.fogDensity, t);

        reflectionIntensity = Mathf.Lerp(a.reflectionIntensity, b.reflectionIntensity, t);

        postExposure = Mathf.Lerp(a.postExposure, b.postExposure, t);
        postContrast = Mathf.Lerp(a.postContrast, b.postContrast, t);
        postSaturation = Mathf.Lerp(a.postSaturation, b.postSaturation, t);
        postHueShift = Mathf.Lerp(a.postHueShift, b.postHueShift, t);
        postFilter = Color.Lerp(a.postFilter, b.postFilter, t);
        postFilterAmount = Mathf.Lerp(a.postFilterAmount, b.postFilterAmount, t);
    }

    public static LightingPreset CreateDay()
    {
        return new LightingPreset
        {
            ambientSky = new Color(0.55f, 0.62f, 0.75f, 1f),
            ambientEquator = new Color(0.45f, 0.45f, 0.42f, 1f),
            ambientGround = new Color(0.25f, 0.22f, 0.18f, 1f),
            ambientIntensity = 1f,
            sunIntensity = 1.2f,
            sunColor = Color.white,
            sunEuler = new Vector3(50f, -30f, 0f),
            skyTint = new Color(0.5f, 0.5f, 0.5f, 1f),
            skyGround = new Color(0.35f, 0.3f, 0.25f, 1f),
            atmosphereThickness = 1f,
            skyExposure = 1.3f,
            fogColor = new Color(0.7f, 0.75f, 0.8f, 1f),
            fogDensity = 0.008f,
            reflectionIntensity = 1f,
            postExposure = 0f,
            postContrast = 0f,
            postSaturation = 0f,
            postHueShift = 0f,
            postFilter = Color.white,
            postFilterAmount = 0f
        };
    }

    public static LightingPreset CreateDusk()
    {
        return new LightingPreset
        {
            ambientSky = new Color(0.35f, 0.25f, 0.28f, 1f),
            ambientEquator = new Color(0.28f, 0.18f, 0.16f, 1f),
            ambientGround = new Color(0.12f, 0.08f, 0.08f, 1f),
            ambientIntensity = 0.9f,
            sunIntensity = 0.35f,
            sunColor = new Color(1f, 0.55f, 0.3f, 1f),
            sunEuler = new Vector3(4f, -30f, 0f),
            skyTint = new Color(0.55f, 0.32f, 0.25f, 1f),
            skyGround = new Color(0.18f, 0.12f, 0.1f, 1f),
            atmosphereThickness = 1.8f,
            skyExposure = 1f,
            fogColor = new Color(0.35f, 0.22f, 0.2f, 1f),
            fogDensity = 0.02f,
            reflectionIntensity = 1f,
            postExposure = -0.2f,
            postContrast = 8f,
            postSaturation = -10f,
            postHueShift = 0f,
            postFilter = new Color(1f, 0.75f, 0.6f, 1f),
            postFilterAmount = 0.15f
        };
    }

    public static LightingPreset CreateNight()
    {
        return new LightingPreset
        {
            ambientSky = new Color(0.06f, 0.07f, 0.11f, 1f),
            ambientEquator = new Color(0.05f, 0.05f, 0.08f, 1f),
            ambientGround = new Color(0.02f, 0.02f, 0.03f, 1f),
            ambientIntensity = 0.5f,
            sunIntensity = 0.06f,
            sunColor = new Color(0.55f, 0.65f, 1f, 1f),
            sunEuler = new Vector3(-12f, -30f, 0f),
            skyTint = new Color(0.05f, 0.06f, 0.12f, 1f),
            skyGround = new Color(0.02f, 0.02f, 0.03f, 1f),
            atmosphereThickness = 2.5f,
            skyExposure = 0.35f,
            fogColor = new Color(0.05f, 0.06f, 0.09f, 1f),
            fogDensity = 0.045f,
            reflectionIntensity = 0.3f,
            postExposure = -0.5f,
            postContrast = 15f,
            postSaturation = -25f,
            postHueShift = 0f,
            postFilter = new Color(0.6f, 0.7f, 1f, 1f),
            postFilterAmount = 0.3f
        };
    }
}
