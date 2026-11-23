using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Monta lo que se hace en los vídeos de clase (18/11/2026):
///  Vídeo 1: Muzzle Flash (textura, material, prefab con 2 quads cruzados) asignado a DemoWeapon001.
///  Vídeo 2: BulletHit (sistema de partículas de humo) asignado como Impact Effect a DemoWeapon001,
///           y view model DemoWeapon002 con el modelo Mark23 y su _MuzzleOrigin.
/// Se ejecuta solo la primera vez que se abre el proyecto; se puede repetir desde
/// Tools > FPS > Montar efectos de los vídeos.
/// </summary>
[InitializeOnLoad]
public static class VideoEffectsSetup
{
    const string ArtEffects = "Assets/Art/Effects";
    const string ArtEffectsMaterials = "Assets/Art/Effects/Materials";
    const string MaterialsFolder = "Assets/Materials";
    const string PrefabsEffects = "Assets/Prefabs/Effects";

    const string MuzzleTexPath = ArtEffects + "/muzzleFlash.png";
    const string SmokeTexPath = ArtEffects + "/smoke.png";
    const string MuzzleMatPath = ArtEffectsMaterials + "/MuzzleFlash.mat";
    const string BulletHitMatPath = MaterialsFolder + "/BulletHitParticles.mat";
    const string MuzzlePrefabPath = PrefabsEffects + "/MuzzleFlash/MuzzleFlash.prefab";
    const string BulletHitPrefabPath = PrefabsEffects + "/BulletHit/BulletHit.prefab";
    const string Weapon001DataPath = "Assets/Data/Weapons/Entries/DemoWeapon001.asset";
    const string Mark23ModelPath = "Assets/Models/mark-23-animated-free/source/Mark23.fbx";
    const string Weapon002PrefabPath = "Assets/Prefabs/Weapons/DemoWeapon002.prefab";

    static VideoEffectsSetup()
    {
        EditorApplication.delayCall += AutoRunOnce;
    }

    static void AutoRunOnce()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += AutoRunOnce;
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.Exists(MuzzlePrefabPath) && File.Exists(BulletHitPrefabPath)) return;
        Run();
    }

    [MenuItem("Tools/FPS/Montar efectos de los vídeos", priority = 40)]
    public static void Run()
    {
        EnsureFolder(ArtEffects);
        EnsureFolder(ArtEffectsMaterials);
        EnsureFolder(MaterialsFolder);
        EnsureFolder(PrefabsEffects + "/MuzzleFlash");
        EnsureFolder(PrefabsEffects + "/BulletHit");

        Texture2D muzzleTex = ImportTexture(MuzzleTexPath);
        Texture2D smokeTex = ImportTexture(SmokeTexPath);

        // ---------------- Vídeo 1: Muzzle Flash ----------------
        Material muzzleMat = LoadOrCreateMaterial(MuzzleMatPath, "Universal Render Pipeline/Lit");
        muzzleMat.SetTexture("_BaseMap", muzzleTex);
        muzzleMat.SetTexture("_MainTex", muzzleTex);
        muzzleMat.SetColor("_BaseColor", Color.white);
        SetFloat(muzzleMat, "_WorkflowMode", 1f);   // Metallic
        SetFloat(muzzleMat, "_Metallic", 0f);
        SetFloat(muzzleMat, "_Smoothness", 0.5f);
        SetupTransparent(muzzleMat, alphaClip: true, cutoff: 0.022f, receiveShadows: false);
        EditorUtility.SetDirty(muzzleMat);

        GameObject muzzlePrefab = BuildMuzzleFlash(muzzleMat);

        // ---------------- Vídeo 2: BulletHit ----------------
        Material hitMat = LoadOrCreateMaterial(BulletHitMatPath, "Universal Render Pipeline/Particles/Lit");
        hitMat.SetTexture("_BaseMap", smokeTex);
        hitMat.SetTexture("_MainTex", smokeTex);
        hitMat.SetColor("_BaseColor", Color.white);
        SetFloat(hitMat, "_ColorMode", 0f);   // Multiply
        SetupTransparent(hitMat, alphaClip: false, cutoff: 0.5f, receiveShadows: true);
        EditorUtility.SetDirty(hitMat);

        GameObject bulletHitPrefab = BuildBulletHit(hitMat);

        // ---------------- Asignar en DemoWeapon001 ----------------
        var data = AssetDatabase.LoadAssetAtPath<ScriptableObject>(Weapon001DataPath);
        if (data != null)
        {
            var so = new SerializedObject(data);
            SetRef(so, "muzzleFlashPrefab", muzzlePrefab);
            SetFloatProp(so, "muzzleFlashLifetime", 0.05f);
            SetRef(so, "impactEffectPrefab", bulletHitPrefab);
            SetFloatProp(so, "impactEffectLifetime", 0.5f);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
        }
        else Debug.LogWarning("[VideoEffectsSetup] No se encontró " + Weapon001DataPath);

        // ---------------- Final vídeo 2: DemoWeapon002 (Mark23) ----------------
        BuildWeapon002();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[VideoEffectsSetup] Listo: MuzzleFlash, BulletHit y DemoWeapon002 creados y asignados.");
    }

    // ------------------------------------------------------------------

    static GameObject BuildMuzzleFlash(Material mat)
    {
        var root = new GameObject("MuzzleFlash");

        // Dos quads cruzados a lo largo del cañón (valores del vídeo).
        var q1 = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q1.name = "Quad";
        q1.transform.SetParent(root.transform, false);
        q1.transform.localPosition = new Vector3(0f, 0f, 0.211f);
        q1.transform.localEulerAngles = new Vector3(0f, -90f, 0f);
        q1.transform.localScale = new Vector3(1.247854f, 0.45199f, 0.45199f);

        var q2 = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q2.name = "Quad (1)";
        q2.transform.SetParent(root.transform, false);
        q2.transform.localPosition = new Vector3(0f, 0f, 0.211f);
        q2.transform.localEulerAngles = new Vector3(90f, -90f, 0f);
        q2.transform.localScale = new Vector3(1.247854f, 0.45199f, 0.45199f);

        foreach (var q in new[] { q1, q2 })
        {
            var r = q.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, MuzzlePrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    static GameObject BuildBulletHit(Material mat)
    {
        var go = new GameObject("BulletHit");
        go.transform.localEulerAngles = new Vector3(-90f, 0f, 0f);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 5f;
        main.loop = true;
        main.startDelay = 0f;
        main.startLifetime = 5f;
        main.startSpeed = 3f;
        main.startSize = 0.5f;
        main.startRotation = 0f;
        main.startColor = Color.white;
        main.gravityModifier = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.playOnAwake = true;
        main.maxParticles = 1000;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.rateOverDistance = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)3, (short)3, 1, 0.01f) });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 6f;
        shape.radius = 0.2f;
        shape.radiusThickness = 1f;
        shape.arc = 360f;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        var curve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.05f, 1f),
            new Keyframe(0.3f, 0.85f),
            new Keyframe(0.5f, 0.3f),
            new Keyframe(0.8f, 0.02f),
            new Keyframe(1f, 0f));
        sol.size = new ParticleSystem.MinMaxCurve(1f, curve);

        var col = ps.collision;
        col.enabled = false;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = mat;
        renderer.sortMode = ParticleSystemSortMode.None;
        renderer.minParticleSize = 0f;
        renderer.maxParticleSize = 0.5f;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.lightProbeUsage = LightProbeUsage.Off;

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, BulletHitPrefabPath);
        UnityEngine.Object.DestroyImmediate(go);
        return prefab;
    }

    static void BuildWeapon002()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(Mark23ModelPath);
        if (model == null)
        {
            Debug.LogWarning("[VideoEffectsSetup] No se encontró " + Mark23ModelPath);
            return;
        }

        var root = new GameObject("DemoWeapon002");
        var viewModel = root.AddComponent<WeaponViewModel>();

        var mark23 = (GameObject)PrefabUtility.InstantiatePrefab(model);
        mark23.name = "Mark23";
        mark23.transform.SetParent(root.transform, false);
        mark23.transform.localPosition = new Vector3(-0.02f, -1.56f, 0.44f);
        mark23.transform.localRotation = Quaternion.identity;

        // _MuzzleOrigin dentro de main_j (la corredera), en la punta delantera del arma.
        Transform mainJ = FindDeep(mark23.transform, "main_j");
        Transform parent = mainJ != null ? mainJ : mark23.transform;
        var muzzle = new GameObject("_MuzzleOrigin").transform;

        Vector3 tip = parent.position;
        Transform mainMesh = FindDeep(mark23.transform, "main");
        Renderer rend = mainMesh != null ? mainMesh.GetComponent<Renderer>() : null;
        if (rend != null)
        {
            Bounds b = rend.bounds;
            tip = new Vector3(b.center.x, b.center.y + b.extents.y * 0.3f, b.max.z);
        }
        muzzle.position = tip;
        muzzle.rotation = root.transform.rotation;
        muzzle.SetParent(parent, true);

        var so = new SerializedObject(viewModel);
        SetRef(so, "muzzle", muzzle);
        so.ApplyModifiedPropertiesWithoutUndo();

        EnsureFolder("Assets/Prefabs/Weapons");
        PrefabUtility.SaveAsPrefabAsset(root, Weapon002PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
    }

    // ------------------------------------------------------------------

    static Texture2D ImportTexture(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Material LoadOrCreateMaterial(string path, string shaderName)
    {
        Shader shader = Shader.Find(shaderName);
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        else if (shader != null && mat.shader != shader)
        {
            mat.shader = shader;
        }
        return mat;
    }

    /// <summary>Surface Type = Transparent, Blending Mode = Alpha (como en el vídeo).</summary>
    static void SetupTransparent(Material m, bool alphaClip, float cutoff, bool receiveShadows)
    {
        SetFloat(m, "_Surface", 1f);
        SetFloat(m, "_Blend", 0f);
        SetFloat(m, "_Cull", 2f); // Render Face = Front
        SetFloat(m, "_AlphaClip", alphaClip ? 1f : 0f);
        SetFloat(m, "_Cutoff", cutoff);
        SetFloat(m, "_ReceiveShadows", receiveShadows ? 1f : 0f);
        SetFloat(m, "_SrcBlend", (float)BlendMode.SrcAlpha);
        SetFloat(m, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        SetFloat(m, "_SrcBlendAlpha", (float)BlendMode.One);
        SetFloat(m, "_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        SetFloat(m, "_ZWrite", 0f);

        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)RenderQueue.Transparent;
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        if (alphaClip) m.EnableKeyword("_ALPHATEST_ON"); else m.DisableKeyword("_ALPHATEST_ON");
        if (receiveShadows) m.DisableKeyword("_RECEIVE_SHADOWS_OFF"); else m.EnableKeyword("_RECEIVE_SHADOWS_OFF");
        m.SetShaderPassEnabled("DepthOnly", false);

        // Deja que el propio URP recalcule keywords/blend igual que su inspector.
        try
        {
            Type gui = Type.GetType("UnityEditor.BaseShaderGUI, Unity.RenderPipelines.Universal.Editor");
            MethodInfo mi = gui?.GetMethod("SetMaterialKeywords", BindingFlags.Public | BindingFlags.Static);
            if (mi != null)
            {
                var args = new object[mi.GetParameters().Length];
                args[0] = m;
                mi.Invoke(null, args);
            }
        }
        catch (Exception e)
        {
            Debug.Log("[VideoEffectsSetup] Ajuste automático de keywords URP no disponible: " + e.Message);
        }
    }

    static void SetFloat(Material m, string name, float value)
    {
        if (m.HasProperty(name)) m.SetFloat(name, value);
    }

    static void SetRef(SerializedObject so, string prop, UnityEngine.Object value)
    {
        var p = so.FindProperty(prop);
        if (p != null) p.objectReferenceValue = value;
        else Debug.LogWarning("[VideoEffectsSetup] Campo no encontrado: " + prop);
    }

    static void SetFloatProp(SerializedObject so, string prop, float value)
    {
        var p = so.FindProperty(prop);
        if (p != null) p.floatValue = value;
    }

    static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t)
        {
            var r = FindDeep(c, name);
            if (r != null) return r;
        }
        return null;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
