using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// Activitat 4 (vídeos del 19/11/2026 + tasques):
///  1. DemoWeapon002: Animator Controller amb els clips del Mark23, WeaponData i prefab de pickup a l'escena.
///  2. UIManager + HUD (munició actual / màxima, reserva i nom de l'arma).
///  3. SFX de tret i de recàrrega. Els sons de recàrrega es llancen amb Animation Events ('PlaySound')
///     posats als clips de recàrrega, al moment exacte en què el carregador surt / entra (es calcula
///     llegint la corba d'animació de l'os del carregador).
///  4. Nivell prototipat amb Blender (Assets/Models/Level/Level.blend): material, col·lisions i instància a l'escena.
/// Es pot tornar a executar des de Tools > FPS > Activitat 4.
/// </summary>
[InitializeOnLoad]
public static class Activitat4Setup
{
    const string SfxFolder = "Assets/Audio/SFX/Weapons";
    const string M4Path = "Assets/Models/m4-fps-weapon-animations-pack-v1/source/M4.fbx";
    const string Mark23Path = "Assets/Models/mark-23-animated-free/source/Mark23.fbx";
    const string Controller002Path = "Assets/Animator/DemoWeapon002.controller";
    const string Weapon001DataPath = "Assets/Data/Weapons/Entries/DemoWeapon001.asset";
    const string Weapon002DataPath = "Assets/Data/Weapons/Entries/DemoWeapon002.asset";
    const string Weapon002PrefabPath = "Assets/Prefabs/Weapons/DemoWeapon002.prefab";
    const string PickupBasePath = "Assets/Prefabs/Pickups/WeaponPickUp.prefab";
    const string Pickup002Path = "Assets/Prefabs/Pickups/DemoWeapon002PickUp.prefab";
    const string DatabasePath = "Assets/Data/Weapons/WeaponDatabase.asset";
    const string ScenePath = "Assets/Scenes/SampleScene.unity";
    public const string LevelFolder = "Assets/Models/Level";
    const string LevelMaterialPath = LevelFolder + "/LevelMaterial.mat";

    static Activitat4Setup()
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
        if (File.Exists(Controller002Path)) return; // ja s'ha executat
        Run();
    }

    [MenuItem("Tools/FPS/Activitat 4 (HUD, SFX, DemoWeapon002, Nivell)", priority = 41)]
    public static void Run()
    {
        AssetDatabase.Refresh();

        // ---------- 3. SFX ----------
        AudioClip[] fire =
        {
            LoadClip("weapon_fire_01.wav"), LoadClip("weapon_fire_02.wav"), LoadClip("weapon_fire_03.wav")
        };
        AudioClip dry = LoadClip("dry_fire.wav");
        AudioClip magOut = LoadClip("reload_mag_out.wav");
        AudioClip magIn = LoadClip("reload_mag_in.wav");
        AudioClip slide = LoadClip("reload_slide.wav");

        AddReloadEvents(M4Path, magOut, magIn, slide);
        AddReloadEvents(Mark23Path, magOut, magIn, slide);

        // ---------- 1. DemoWeapon002 ----------
        AnimatorController controller = BuildController002();
        WeaponViewModel vm002 = SetupViewModel002(controller);
        WeaponPickup pickup002 = BuildPickup002();

        var data001 = AssetDatabase.LoadAssetAtPath<WeaponData>(Weapon001DataPath);
        var data002 = AssetDatabase.LoadAssetAtPath<WeaponData>(Weapon002DataPath);
        if (data001 != null)
        {
            data001.fireSounds = fire;
            data001.dryFireSound = dry;
            data001.reloadSound = null; // la recàrrega sona per Animation Events
            EditorUtility.SetDirty(data001);
        }
        if (data002 != null)
        {
            data002.displayName = "DemoWeapon002";
            data002.slot = WeaponSlot.Secondary;
            if (vm002 != null) data002.viewModelPrefab = vm002;
            if (pickup002 != null) data002.pickupPrefab = pickup002;
            if (data001 != null)
            {
                data002.muzzleFlashPrefab = data001.muzzleFlashPrefab;
                data002.muzzleFlashLifetime = data001.muzzleFlashLifetime;
                data002.impactEffectPrefab = data001.impactEffectPrefab;
                data002.impactEffectLifetime = 1f;
            }
            data002.fireSounds = fire;
            data002.dryFireSound = dry;
            data002.reloadSound = null;
            EditorUtility.SetDirty(data002);

            var db = AssetDatabase.LoadAssetAtPath<WeaponDatabase>(DatabasePath);
            if (db != null)
            {
                var so = new SerializedObject(db);
                var list = so.FindProperty("weapons");
                bool found = false;
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == data002) found = true;
                if (!found)
                {
                    list.InsertArrayElementAtIndex(list.arraySize);
                    list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = data002;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }
        AssetDatabase.SaveAssets();

        // ---------- Escena: pickup, HUD i nivell ----------
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        if (pickup002 != null && GameObject.Find("DemoWeapon002PickUp") == null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(pickup002.gameObject);
            go.name = "DemoWeapon002PickUp";
            var other = GameObject.Find("WeaponPickUp");
            go.transform.position = other != null ? other.transform.position + new Vector3(1.5f, 0f, 0f)
                                                  : new Vector3(2f, 0.45f, 3f);
        }

        BuildHUD();
        SetupLevel(true);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Activitat4Setup] Fet: DemoWeapon002, HUD (UIManager), SFX amb Animation Events i nivell.");
    }

    /// <summary>Prova ràpida en Play: dona les dues armes de demo al jugador.</summary>
    [MenuItem("Tools/FPS/Prova: donar armes al jugador (Play)", priority = 43)]
    static void GiveTestWeapons()
    {
        var wm = Object.FindFirstObjectByType<WeaponManager>();
        if (wm == null) { Debug.LogWarning("No hi ha cap WeaponManager a l'escena."); return; }
        var d1 = AssetDatabase.LoadAssetAtPath<WeaponData>(Weapon001DataPath);
        var d2 = AssetDatabase.LoadAssetAtPath<WeaponData>(Weapon002DataPath);
        if (d2 != null) wm.GiveWeapon(d2, -1, -1, false);
        if (d1 != null) wm.GiveWeapon(d1, -1, -1, true);
    }

    [MenuItem("Tools/FPS/Prova: donar armes al jugador (Play)", true)]
    static bool GiveTestWeaponsValidate() => EditorApplication.isPlaying;

    // =====================================================================
    // SFX + Animation Events
    // =====================================================================

    static AudioClip LoadClip(string file)
    {
        string path = SfxFolder + "/" + file;
        var imp = AssetImporter.GetAtPath(path) as AudioImporter;
        if (imp != null)
        {
            var s = imp.defaultSampleSettings;
            s.loadType = AudioClipLoadType.DecompressOnLoad;
            imp.defaultSampleSettings = s;
            imp.SaveAndReimport();
        }
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (clip == null) Debug.LogWarning("[Activitat4Setup] Falta el so " + path);
        return clip;
    }

    /// <summary>
    /// Afegeix events 'PlaySound' als clips de recàrrega d'un FBX. Els temps surten de la corba de
    /// l'os del carregador: quan comença a moure's (surt) i quan torna a la seva posició (entra).
    /// Si hi ha una corredera que es mou després, s'hi afegeix el so de la corredera.
    /// </summary>
    static void AddReloadEvents(string fbxPath, AudioClip magOut, AudioClip magIn, AudioClip slide)
    {
        var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer == null) { Debug.LogWarning("[Activitat4Setup] No trobo " + fbxPath); return; }

        var sourceClips = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__")).ToDictionary(c => c.name, c => c);

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;

        bool changed = false;
        foreach (var c in clips)
        {
            if (!c.name.ToLowerInvariant().Contains("reload")) continue;
            if (!sourceClips.TryGetValue(c.name, out AnimationClip clip) || clip.length <= 0f) continue;

            float len = clip.length;
            float tOut, tIn;
            if (!FindMovementWindow(clip, IsMagazinePath, out tOut, out tIn))
            {
                tOut = len * 0.25f;
                tIn = len * 0.6f;
            }

            var events = new List<AnimationEvent>
            {
                MakeEvent(tOut / len, magOut),
                MakeEvent(tIn / len, magIn)
            };

            if (FindMovementAfter(clip, IsSlidePath, tIn, out float tSlide) && slide != null)
                events.Add(MakeEvent(tSlide / len, slide));
            else if (c.name.ToLowerInvariant().Contains("empty") && slide != null)
                events.Add(MakeEvent(Mathf.Lerp(tIn, len, 0.4f) / len, slide));

            c.events = events.Where(e => e.objectReferenceParameter != null).ToArray();
            changed = true;
            Debug.Log($"[Activitat4Setup] {Path.GetFileName(fbxPath)}/{c.name}: events a " +
                      string.Join(", ", c.events.Select(e => (e.time * len).ToString("0.00") + "s (" +
                                                              e.objectReferenceParameter.name + ")")));
        }

        if (changed)
        {
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }
    }

    static AnimationEvent MakeEvent(float normalizedTime, AudioClip clip) => new AnimationEvent
    {
        functionName = "PlaySound",
        objectReferenceParameter = clip,
        time = Mathf.Clamp01(normalizedTime)
    };

    static bool IsMagazinePath(string path)
    {
        string last = path.Substring(path.LastIndexOf('/') + 1).ToLowerInvariant();
        return last.Contains("mag") && !last.Contains("bullet");
    }

    static bool IsSlidePath(string path)
    {
        string last = path.Substring(path.LastIndexOf('/') + 1).ToLowerInvariant();
        return last.Contains("slide") || last.Contains("side_j") || last.Contains("bolt") || last.Contains("charg");
    }

    static float[] Displacement(AnimationClip clip, System.Func<string, bool> pathFilter, out float step)
    {
        step = 1f / 60f;
        var bindings = AnimationUtility.GetCurveBindings(clip)
            .Where(b => pathFilter(b.path) && b.propertyName.StartsWith("m_LocalPosition"))
            .ToArray();
        if (bindings.Length == 0) return null;

        string path = bindings.GroupBy(b => b.path).OrderBy(g => g.Key.Count(ch => ch == '/')).First().Key;
        var curves = bindings.Where(b => b.path == path)
            .Select(b => AnimationUtility.GetEditorCurve(clip, b)).Where(cv => cv != null).ToArray();

        int n = Mathf.CeilToInt(clip.length / step) + 1;
        var d = new float[n];
        float[] start = curves.Select(cv => cv.Evaluate(0f)).ToArray();
        for (int i = 0; i < n; i++)
        {
            float t = Mathf.Min(i * step, clip.length), sum = 0f;
            for (int k = 0; k < curves.Length; k++)
            {
                float v = curves[k].Evaluate(t) - start[k];
                sum += v * v;
            }
            d[i] = Mathf.Sqrt(sum);
        }
        return d;
    }

    static bool FindMovementWindow(AnimationClip clip, System.Func<string, bool> filter, out float tStart, out float tEnd)
    {
        tStart = tEnd = 0f;
        var d = Displacement(clip, filter, out float step);
        if (d == null) return false;
        float max = d.Max();
        if (max < 1e-4f) return false;
        float th = max * 0.12f;
        int first = System.Array.FindIndex(d, v => v > th);
        int last = System.Array.FindLastIndex(d, v => v > th);
        if (first < 0 || last <= first) return false;
        tStart = first * step;
        tEnd = Mathf.Min((last + 1) * step, clip.length);
        return true;
    }

    static bool FindMovementAfter(AnimationClip clip, System.Func<string, bool> filter, float after, out float t)
    {
        t = 0f;
        var d = Displacement(clip, filter, out float step);
        if (d == null) return false;
        int from = Mathf.Clamp(Mathf.CeilToInt(after / step), 0, d.Length - 1);
        float baseV = d[from], max = 0f;
        for (int i = from; i < d.Length; i++) max = Mathf.Max(max, Mathf.Abs(d[i] - baseV));
        if (max < 1e-4f) return false;
        for (int i = from; i < d.Length; i++)
            if (Mathf.Abs(d[i] - baseV) > max * 0.5f) { t = i * step; return true; }
        return false;
    }

    // =====================================================================
    // DemoWeapon002
    // =====================================================================

    static AnimatorController BuildController002()
    {
        var clips = AssetDatabase.LoadAllAssetsAtPath(Mark23Path).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__")).ToList();
        AnimationClip Find(params string[] names) =>
            clips.FirstOrDefault(c => names.Any(n => c.name.Equals(n, System.StringComparison.OrdinalIgnoreCase)));

        if (File.Exists(Controller002Path)) AssetDatabase.DeleteAsset(Controller002Path);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(Controller002Path);
        var sm = controller.layers[0].stateMachine;

        void Add(string state, AnimationClip clip, Vector3 pos, bool isDefault = false)
        {
            var s = sm.AddState(state, pos);
            s.motion = clip;
            if (isDefault) sm.defaultState = s;
            if (clip == null) Debug.LogWarning("[Activitat4Setup] Mark23 sense clip per a " + state);
        }

        Add(WeaponAnimStates.Equip, Find("Draw"), new Vector3(300, 0));
        Add(WeaponAnimStates.Reload, Find("Reload"), new Vector3(300, 60));
        Add(WeaponAnimStates.Fire, Find("Shoot", "Fire"), new Vector3(300, 120));
        Add(WeaponAnimStates.Idle, Find("Idle"), new Vector3(300, 180), true);
        Add(WeaponAnimStates.Holster, Find("Hide", "Holster"), new Vector3(300, 240));
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    static WeaponViewModel SetupViewModel002(AnimatorController controller)
    {
        if (!File.Exists(Weapon002PrefabPath))
        {
            Debug.LogWarning("[Activitat4Setup] No existeix " + Weapon002PrefabPath + " (fes primer l'activitat anterior).");
            return null;
        }
        var root = PrefabUtility.LoadPrefabContents(Weapon002PrefabPath);
        var vm = root.GetComponent<WeaponViewModel>();
        var animator = root.GetComponentInChildren<Animator>(true);
        if (animator == null)
        {
            Transform mark = root.transform.Find("Mark23");
            animator = (mark != null ? mark.gameObject : root).AddComponent<Animator>();
        }
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        var so = new SerializedObject(vm);
        so.FindProperty("animator").objectReferenceValue = animator;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, Weapon002PrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
        return AssetDatabase.LoadAssetAtPath<WeaponViewModel>(Weapon002PrefabPath);
    }

    static WeaponPickup BuildPickup002()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(Mark23Path);
        var data002 = AssetDatabase.LoadAssetAtPath<WeaponData>(Weapon002DataPath);
        if (model == null || !File.Exists(PickupBasePath)) return null;

        var root = PrefabUtility.LoadPrefabContents(PickupBasePath);
        root.name = "DemoWeapon002PickUp";
        // Treu el model de l'M4
        foreach (Transform child in root.transform.Cast<Transform>().ToList())
            Object.DestroyImmediate(child.gameObject);

        // Copia les malles de la pistola (main, mag, Side) com a malles estàtiques, sense mans
        var temp = Object.Instantiate(model);
        var visual = new GameObject("Mark23").transform;
        visual.SetParent(root.transform, false);
        foreach (var r in temp.GetComponentsInChildren<Renderer>(true))
        {
            if (r.name.ToLowerInvariant().Contains("hand")) continue;
            Mesh mesh = r is SkinnedMeshRenderer smr ? smr.sharedMesh
                      : r.GetComponent<MeshFilter>() != null ? r.GetComponent<MeshFilter>().sharedMesh : null;
            if (mesh == null) continue;
            var part = new GameObject(r.name);
            part.transform.SetParent(visual, false);
            part.transform.localPosition = temp.transform.InverseTransformPoint(r.transform.position);
            part.transform.localRotation = Quaternion.Inverse(temp.transform.rotation) * r.transform.rotation;
            part.transform.localScale = r.transform.lossyScale;
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = part.AddComponent<MeshRenderer>();
            mr.sharedMaterials = r.sharedMaterials;
        }
        Object.DestroyImmediate(temp);

        // Estirada de costat a terra, com al vídeo (rotació Z 90)
        visual.localRotation = Quaternion.Euler(0f, 0f, 90f);
        Bounds b = new Bounds();
        bool has = false;
        foreach (var mr in visual.GetComponentsInChildren<MeshRenderer>())
        {
            Bounds wb = mr.bounds;
            var lb = new Bounds(root.transform.InverseTransformPoint(wb.center), wb.size);
            if (!has) { b = lb; has = true; } else b.Encapsulate(lb);
        }
        if (has) visual.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z) - new Vector3(0, -0.45f, 0);

        var box = root.GetComponent<BoxCollider>();
        if (box != null && has)
        {
            box.center = new Vector3(0f, -0.45f + b.size.y * 0.5f, 0f);
            box.size = new Vector3(Mathf.Max(b.size.x, 0.05f), Mathf.Max(b.size.y, 0.05f), Mathf.Max(b.size.z, 0.05f));
        }
        var rootRenderer = root.GetComponent<MeshRenderer>();
        if (rootRenderer != null) rootRenderer.enabled = false;

        var pickup = root.GetComponent<WeaponPickup>();
        if (pickup != null && data002 != null) pickup.SetWeapon(data002);

        PrefabUtility.SaveAsPrefabAsset(root, Pickup002Path);
        PrefabUtility.UnloadPrefabContents(root);
        return AssetDatabase.LoadAssetAtPath<WeaponPickup>(Pickup002Path);
    }

    // =====================================================================
    // HUD
    // =====================================================================

    static void BuildHUD()
    {
        if (Object.FindFirstObjectByType<UIManager>() != null) return;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var go = new GameObject("UIManager", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var panel = new GameObject("WeaponPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(go.transform, false);
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(1f, 0f);
        prt.anchoredPosition = new Vector2(-40f, 40f);
        prt.sizeDelta = new Vector2(380f, 170f);
        panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);

        Text MakeText(string name, int size, FontStyle style, float y, float h, TextAnchor anchor, string sample)
        {
            var t = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Shadow));
            t.transform.SetParent(panel.transform, false);
            var rt = t.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(-40f, h);
            var text = t.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = Color.white;
            text.raycastTarget = false;
            text.text = sample;
            return text;
        }

        Text nameText = MakeText("WeaponName", 30, FontStyle.Bold, -12f, 40f, TextAnchor.MiddleRight, "DemoWeapon001");
        Text ammoText = MakeText("Ammo", 64, FontStyle.Bold, -50f, 72f, TextAnchor.MiddleRight, "30 / 30");
        Text reserveText = MakeText("Reserve", 24, FontStyle.Normal, -124f, 34f, TextAnchor.MiddleRight, "Reserva: 90 / 180");

        var ui = go.AddComponent<UIManager>();
        var so = new SerializedObject(ui);
        so.FindProperty("weaponManager").objectReferenceValue = Object.FindFirstObjectByType<WeaponManager>();
        so.FindProperty("weaponPanel").objectReferenceValue = panel;
        so.FindProperty("weaponNameText").objectReferenceValue = nameText;
        so.FindProperty("ammoText").objectReferenceValue = ammoText;
        so.FindProperty("reserveText").objectReferenceValue = reserveText;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // =====================================================================
    // Nivell de Blender
    // =====================================================================

    [MenuItem("Tools/FPS/Col·locar nivell de Blender", priority = 42)]
    public static void SetupLevelMenu()
    {
        SetupLevel(true);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    static string FindLevelModel()
    {
        if (!AssetDatabase.IsValidFolder(LevelFolder)) return null;
        string[] candidates = { LevelFolder + "/Level.blend", LevelFolder + "/Level.fbx" };
        return candidates.FirstOrDefault(p => AssetDatabase.LoadAssetAtPath<GameObject>(p) != null);
    }

    static void SetupLevel(bool placeInScene)
    {
        string path = FindLevelModel();
        if (path == null)
        {
            Debug.Log("[Activitat4Setup] Encara no hi ha cap Level.blend a " + LevelFolder + ".");
            return;
        }

        // Material del blockout (gris clar, dues cares)
        var mat = AssetDatabase.LoadAssetAtPath<Material>(LevelMaterialPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", new Color32(231, 231, 231, 255));
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.2f);
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", (float)CullMode.Off); // Render Face: Both
            mat.doubleSidedGI = true;
            AssetDatabase.CreateAsset(mat, LevelMaterialPath);
        }

        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer != null)
        {
            importer.addCollider = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            foreach (var m in AssetDatabase.LoadAllAssetRepresentationsAtPath(path).OfType<Material>())
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(m), mat);
            importer.SaveAndReimport();
        }

        if (!placeInScene) return;
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (GameObject.Find("Level") == null && model != null)
        {
            var level = (GameObject)PrefabUtility.InstantiatePrefab(model);
            level.name = "Level";
            level.transform.position = Vector3.zero;
            level.isStatic = true;
            foreach (Transform t in level.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
        }
        var plane = GameObject.Find("Plane");
        if (plane != null) plane.SetActive(false);
    }
}

/// <summary>
/// Workflow iteratiu: cada cop que es desa Level.blend des de Blender, Unity el reimporta
/// i aquest postprocessador hi torna a posar les col·lisions automàticament.
/// </summary>
public class LevelModelPostprocessor : AssetPostprocessor
{
    void OnPreprocessModel()
    {
        if (!assetPath.StartsWith(Activitat4Setup.LevelFolder)) return;
        var importer = (ModelImporter)assetImporter;
        importer.addCollider = true;
        importer.importAnimation = false;
        importer.importCameras = false;
        importer.importLights = false;
    }
}
