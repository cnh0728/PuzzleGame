using UnityEngine;

public class EffectManager : MonoBehaviour
{
    public static EffectManager Instance { get; private set; }

    [Header("Particle Base Prefab")]
    [SerializeField] private GameObject baseParticlePrefab;

    private Material circleParticleMaterial;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // ⭐️ 코드로 부드러운 원형 텍스처 및 재질(Material) 완전 자동 생성
        CreateCircleMaterial();
    }

    /// <summary>
    /// C# 코드로 중앙은 밝고 외곽은 스르륵 사라지는 부드러운 원형 Texture2D와 Material 생성
    /// </summary>
    private void CreateCircleMaterial()
    {
        int size = 64;
        Texture2D circleTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] colors = new Color[size * size];
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                // 중심에서 멀어질수록 투명해지는 부드러운 알파 곡선
                float alpha = Mathf.Clamp01(1f - (dist / radius));
                alpha = Mathf.Pow(alpha, 1.8f); // 스르륵 은은해지는 감쇄

                colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        circleTex.SetPixels(colors);
        circleTex.Apply();

        // 가장 안전한 2D/Particle 호환 셰이더 선택
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");

        circleParticleMaterial = new Material(shader);
        circleParticleMaterial.mainTexture = circleTex;
    }

    /// <summary>
    /// 위치와 매치 모양에 따라 파티클 이펙트 생성
    /// </summary>
    public void PlayShapeEffect(Vector3 worldPos, MatchShape shape)
    {
        GameObject fxObj;

        if (baseParticlePrefab != null)
        {
            fxObj = Instantiate(baseParticlePrefab, worldPos, Quaternion.identity);
        }
        else
        {
            fxObj = new GameObject("FX_Particle");
            fxObj.transform.position = worldPos;
            fxObj.AddComponent<ParticleSystem>();
        }

        ParticleSystem ps = fxObj.GetComponent<ParticleSystem>();
        ParticleSystemRenderer psRenderer = fxObj.GetComponent<ParticleSystemRenderer>();

        // ⭐️ 1. 수치를 수정하기 전, 파티클을 완전히 멈추고 픽셀/입자를 초기화합니다.
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // 2. 동그라미 재질 적용
        if (psRenderer != null && circleParticleMaterial != null)
        {
            psRenderer.material = circleParticleMaterial;
        }

        // ⭐️ 3. 수치 세팅을 먼저 마치고...
        SetupParticleByShape(ps, shape);

        // ⭐️ 4. 모든 설정이 완전히 끝난 후 Play()를 호출합니다!
        ps.Play();

        float totalLifetime = ps.main.duration + ps.main.startLifetime.constantMax;
        Destroy(fxObj, totalLifetime);
    }

    private void SetupParticleByShape(ParticleSystem ps, MatchShape shape)
    {
        // 1. Main 모듈
        var main = ps.main;
        main.loop = false;
        main.duration = 0.25f;
        main.startLifetime = 0.35f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        // 2. Emission 모듈 (Burst 순간 폭발)
        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0;

        ParticleSystem.Burst burst = new ParticleSystem.Burst(0f, 18);
        emission.SetBursts(new ParticleSystem.Burst[] { burst });

        // 3. Size Over Lifetime (끝으로 갈수록 스르륵 작아짐)
        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0.0f, 1.0f);
        curve.AddKey(1.0f, 0.0f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1.0f, curve);

        // 4. Shape 모듈 및 모양별 색상/크기 세팅
        var shapeModule = ps.shape;
        shapeModule.enabled = true;

        switch (shape)
        {
            case MatchShape.TShape: // 🔴 T자 매치 (강렬한 빨간색 / 주황)
                main.startColor = new Color(1f, 0.25f, 0.1f);
                main.startSize = 0.35f;
                main.startSpeed = 4.5f;
                shapeModule.shapeType = ParticleSystemShapeType.Cone;
                shapeModule.angle = 35f;
                break;

            case MatchShape.LShape: // 🟡 L자 매치 (황금색 / 원형으로 팡 터짐)
                main.startColor = new Color(1f, 0.85f, 0.1f);
                main.startSize = 0.4f;
                main.startSpeed = 5f;
                shapeModule.shapeType = ParticleSystemShapeType.Circle;
                shapeModule.radius = 0.25f;
                break;

            case MatchShape.Cross: // 🔵 십자 매치 (하늘색 Cyan / 십자 영역)
                main.startColor = new Color(0.1f, 0.85f, 1f);
                main.startSize = 0.45f;
                main.startSpeed = 4f;
                shapeModule.shapeType = ParticleSystemShapeType.Rectangle;
                shapeModule.scale = new Vector3(0.7f, 0.7f, 1f);
                break;

            case MatchShape.Square3x3: // 🟣 3x3 정사각형 (자줏빛 / 대형 폭발)
                main.startColor = new Color(0.85f, 0.15f, 1f);
                main.startSize = 0.55f;
                main.startSpeed = 6f;
                shapeModule.shapeType = ParticleSystemShapeType.Box;
                shapeModule.scale = new Vector3(1.1f, 1.1f, 1f);
                break;

            default: // ⚪️ 일반 3개 일자 (은은한 흰색/빛가루)
                main.startColor = new Color(0.9f, 0.95f, 1f);
                main.startSize = 0.25f;
                main.startSpeed = 2.5f;
                shapeModule.shapeType = ParticleSystemShapeType.Sphere;
                shapeModule.radius = 0.15f;
                break;
        }
    }
}