using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools > Data Importer 창
/// Google Sheets → JSON (Docker) → ScriptableObject 전 과정을 버튼 하나로 실행
/// </summary>
public class DataImporter : EditorWindow
{
    private const string SO_OUTPUT_DIR = "Assets/Data/SO";

    private string _log = "";
    private Vector2 _scrollPos;

    // ─── JsonUtility 배열 파싱용 wrapper 클래스 ────────────────────────────
    // JsonUtility는 최상위 배열을 파싱 못 하므로 {"items":[...]} 형태로 감쌈

    [Serializable] private class ItemWrapper          { public List<ItemEntry>             items = new List<ItemEntry>(); }
    [Serializable] private class EquipmentWrapper     { public List<EquipmentDetailEntry>  items = new List<EquipmentDetailEntry>(); }
    [Serializable] private class ConsumableWrapper    { public List<ConsumableDetailEntry> items = new List<ConsumableDetailEntry>(); }
    [Serializable] private class EnemyWrapper         { public List<EnemyEntry>            items = new List<EnemyEntry>(); }
    [Serializable] private class EnhanceWrapper       { public List<EnhanceEntry>          items = new List<EnhanceEntry>(); }
    [Serializable] private class ArmorEnhanceWrapper  { public List<ArmorEnhanceEntry>     items = new List<ArmorEnhanceEntry>(); }
    [Serializable] private class LootWrapper          { public List<LootEntry>             items = new List<LootEntry>(); }

    // ─── Editor 창 ─────────────────────────────────────────────────────────

    [MenuItem("Tools/Data Importer")]
    public static void ShowWindow()
    {
        var window = GetWindow<DataImporter>("Data Importer");
        window.minSize = new Vector2(360, 420);
    }

    void OnGUI()
    {
        GUILayout.Space(8);
        GUILayout.Label("Google Sheets → ScriptableObjects", EditorStyles.boldLabel);
        GUILayout.Space(4);

        EditorGUILayout.HelpBox(
            "Step 1: Docker로 Python 스크립트를 실행해 Google Sheets에서 JSON을 다운로드합니다.\n" +
            "Step 2: JSON 파일을 읽어 ScriptableObject로 변환합니다.",
            MessageType.Info);

        GUILayout.Space(12);

        if (GUILayout.Button("1.  Google Sheets → JSON  (Docker 실행)", GUILayout.Height(36)))
            DownloadFromGoogleSheets();

        GUILayout.Space(6);

        if (GUILayout.Button("2.  JSON → ScriptableObjects", GUILayout.Height(36)))
            ImportAllJson();

        GUILayout.Space(10);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        GUILayout.Space(4);

        Color prevColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.3f, 0.75f, 0.3f);
        if (GUILayout.Button("All-in-One  (전체 자동 실행)", GUILayout.Height(46)))
        {
            if (DownloadFromGoogleSheets())
                ImportAllJson();
        }
        GUI.backgroundColor = prevColor;

        GUILayout.Space(12);
        GUILayout.Label("Log", EditorStyles.boldLabel);
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.ExpandHeight(true));
        EditorGUILayout.TextArea(_log, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();

        if (GUILayout.Button("Clear"))
            _log = "";
    }

    // ─── Step 1: Docker 실행 ───────────────────────────────────────────────

    bool DownloadFromGoogleSheets()
    {
        string toolsPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Tools"));
        string composeFile = Path.Combine(toolsPath, "docker-compose.yml");

        if (!File.Exists(composeFile))
        {
            Log("❌ docker-compose.yml을 찾을 수 없음: " + toolsPath);
            return false;
        }

        Log("🐳 Docker로 Python 스크립트 실행 중...");
        Log("   (첫 실행은 이미지 빌드로 1~2분 걸릴 수 있습니다)");

        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = "compose run --build --rm data-exporter",
            WorkingDirectory = toolsPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        try
        {
            Process proc = Process.Start(psi);

            if (proc == null)
            {
                Log("❌ 프로세스 시작 실패 (docker 명령어를 찾을 수 없음)");
                return false;
            }

            StringBuilder sbOut = new StringBuilder();
            StringBuilder sbErr = new StringBuilder();

            proc.OutputDataReceived += (sender, e) => { if (e.Data != null) sbOut.AppendLine(e.Data); };
            proc.ErrorDataReceived  += (sender, e) => { if (e.Data != null) sbErr.AppendLine(e.Data); };
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            bool finished = proc.WaitForExit(300000); // 5분

            // ExitCode는 Dispose 전에 저장
            int exitCode = finished ? proc.ExitCode : -1;
            proc.Dispose();

            if (!finished)
            {
                Log("❌ 5분 초과 — 타임아웃");
                return false;
            }

            string stdout = sbOut.ToString().Trim();
            string stderr = sbErr.ToString().Trim();

            if (!string.IsNullOrEmpty(stdout)) Log(stdout);
            if (!string.IsNullOrEmpty(stderr))  Log("[docker] " + stderr);

            if (exitCode != 0)
            {
                Log("❌ Docker 오류 (exit code " + exitCode + ")");
                return false;
            }

            Log("✅ JSON 다운로드 완료");
            return true;
        }
        catch (Exception e)
        {
            Log("❌ Docker 실행 실패: " + e.Message);
            Log("   → Docker Desktop이 실행 중인지 확인하세요.");
            return false;
        }
    }

    // ─── Step 2: JSON → SO ─────────────────────────────────────────────────

    void ImportAllJson()
    {
        string jsonDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Tools/Datas"));

        if (!Directory.Exists(jsonDir))
        {
            Log("❌ JSON 폴더 없음: " + jsonDir);
            return;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Data"))
            AssetDatabase.CreateFolder("Assets", "Data");
        if (!AssetDatabase.IsValidFolder(SO_OUTPUT_DIR))
            AssetDatabase.CreateFolder("Assets/Data", "SO");

        Log("📦 변환 시작...");

        ImportItemTable(jsonDir);
        ImportEquipmentDetail(jsonDir);
        ImportConsumableDetail(jsonDir);
        ImportEnemyTable(jsonDir);
        ImportEnhanceTable(jsonDir);
        ImportArmorEnhanceTable(jsonDir);
        ImportLootTable(jsonDir);
        GenerateShopItemPool(jsonDir);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        UnityEngine.Object folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(SO_OUTPUT_DIR);
        if (folder != null) Selection.activeObject = folder;

        Log("✅ 완료 — Assets/Data/SO/ 폴더를 확인하세요.");
    }

    // ─── 각 테이블 임포트 ───────────────────────────────────────────────────

    void ImportItemTable(string jsonDir)
    {
        ItemWrapper w = ParseJson<ItemWrapper>(jsonDir, "ItemTable");
        List<ItemEntry> entries = w != null ? w.items : new List<ItemEntry>();

        // Pass 1: Sprite Single 모드로 변경 후 일괄 리임포트
        foreach (var entry in entries)
        {
            if (string.IsNullOrEmpty(entry.Path)) continue;
            TextureImporter ti = AssetImporter.GetAtPath(entry.Path) as TextureImporter;
            if (ti == null) { Log($"   ⚠️ 임포터 없음: {entry.Path}"); continue; }
            if (ti.textureType != TextureImporterType.Sprite ||
                ti.spriteImportMode != SpriteImportMode.Single)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.SaveAndReimport();
            }
        }

        // Pass 2: 리임포트 완료 후 스프라이트 로드
        int iconLoaded = 0;
        foreach (var entry in entries)
        {
            if (string.IsNullOrEmpty(entry.Path)) continue;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(entry.Path))
            {
                if (asset is Sprite s) { entry.Icon = s; break; }
            }
            if (entry.Icon != null) iconLoaded++;
            else Log($"   ⚠️ Sprite 로드 실패: {entry.Path}");
        }

        ItemTableSO so = GetOrCreateSO<ItemTableSO>("ItemTableSO");
        so.items = entries;
        EditorUtility.SetDirty(so);
        Log($"   ItemTable       : {entries.Count}개 (아이콘 {iconLoaded}/{entries.Count})");
    }

    void ImportEquipmentDetail(string jsonDir)
    {
        EquipmentWrapper w = ParseJson<EquipmentWrapper>(jsonDir, "EquipmentDetail");
        List<EquipmentDetailEntry> entries = w != null ? w.items : new List<EquipmentDetailEntry>();
        EquipmentDetailSO so = GetOrCreateSO<EquipmentDetailSO>("EquipmentDetailSO");
        so.items = entries;
        EditorUtility.SetDirty(so);
        Log("   EquipmentDetail : " + entries.Count + "개");
    }

    void ImportConsumableDetail(string jsonDir)
    {
        ConsumableWrapper w = ParseJson<ConsumableWrapper>(jsonDir, "ConsumableDetail");
        List<ConsumableDetailEntry> entries = w != null ? w.items : new List<ConsumableDetailEntry>();
        ConsumableDetailSO so = GetOrCreateSO<ConsumableDetailSO>("ConsumableDetailSO");
        so.items = entries;
        EditorUtility.SetDirty(so);
        Log("   ConsumableDetail: " + entries.Count + "개");
    }

    void ImportEnemyTable(string jsonDir)
    {
        EnemyWrapper w = ParseJson<EnemyWrapper>(jsonDir, "EnemyTable");
        List<EnemyEntry> entries = w != null ? w.items : new List<EnemyEntry>();
        EnemyTableSO so = GetOrCreateSO<EnemyTableSO>("EnemyTableSO");
        so.items = entries;
        EditorUtility.SetDirty(so);
        Log("   EnemyTable      : " + entries.Count + "개");
    }

    void ImportEnhanceTable(string jsonDir)
    {
        EnhanceWrapper w = ParseJson<EnhanceWrapper>(jsonDir, "EnhanceTable");
        List<EnhanceEntry> entries = w != null ? w.items : new List<EnhanceEntry>();
        EnhanceTableSO so = GetOrCreateSO<EnhanceTableSO>("EnhanceTableSO");
        so.items = entries;
        EditorUtility.SetDirty(so);
        Log("   EnhanceTable    : " + entries.Count + "개");
    }

    void ImportArmorEnhanceTable(string jsonDir)
    {
        ArmorEnhanceWrapper w = ParseJson<ArmorEnhanceWrapper>(jsonDir, "ArmorEnhanceTable");
        List<ArmorEnhanceEntry> entries = w != null ? w.items : new List<ArmorEnhanceEntry>();
        ArmorEnhanceTableSO so = GetOrCreateSO<ArmorEnhanceTableSO>("ArmorEnhanceTableSO");
        so.items = entries;
        EditorUtility.SetDirty(so);
        Log("   ArmorEnhanceTable: " + entries.Count + "개");
    }

    void ImportLootTable(string jsonDir)
    {
        LootWrapper w = ParseJson<LootWrapper>(jsonDir, "LootTable");
        List<LootEntry> entries = w != null ? w.items : new List<LootEntry>();
        LootTableSO so = GetOrCreateSO<LootTableSO>("LootTableSO");
        so.items = entries;
        EditorUtility.SetDirty(so);
        Log("   LootTable       : " + entries.Count + "개");
    }

    void GenerateShopItemPool(string jsonDir)
    {
        ItemWrapper w = ParseJson<ItemWrapper>(jsonDir, "ItemTable");
        if (w == null) { Log("   ⚠️ shop_item_pool.py 생성 건너뜀 (ItemTable 파싱 실패)"); return; }

        string serverPath = Path.GetFullPath(Path.Combine(
            Application.dataPath, "../../Server/DataHub/app/shop_item_pool.py"));

        // 분류 규칙
        // 장비 고정: Low/Mid 등급 장비 전부
        // 장비 랜덤: High/Top 등급 장비
        // 소모품 고정: 이름에 "포션" 포함(전 등급) + Low/Mid 스크롤
        // 소모품 랜덤: High/Top 등급 스크롤

        var equipFixed   = new System.Collections.Generic.List<ItemEntry>();
        var equipRandom  = new System.Collections.Generic.List<ItemEntry>();
        var consumFixed  = new System.Collections.Generic.List<ItemEntry>();
        var consumRandom = new System.Collections.Generic.List<ItemEntry>();

        foreach (var item in w.items)
        {
            if (item.ItemType == "Equipment")
            {
                if (item.Grade == "Low" || item.Grade == "Mid")
                    equipFixed.Add(item);
                else if (item.Grade == "High" || item.Grade == "Top")
                    equipRandom.Add(item);
            }
            else if (item.ItemType == "Consumable")
            {
                bool isPotion = item.Name != null && item.Name.Contains("포션");
                if (isPotion || item.Grade == "Low" || item.Grade == "Mid")
                    consumFixed.Add(item);
                else if (item.Grade == "High" || item.Grade == "Top")
                    consumRandom.Add(item);
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine("# 이 파일은 DataImporter(Unity Editor)가 자동 생성합니다. 직접 수정하지 마세요.");
        sb.AppendLine("# 갱신하려면 Tools > Data Importer 에서 JSON → ScriptableObjects 를 실행하세요.");
        sb.AppendLine("from typing import List, Dict");
        sb.AppendLine();
        AppendPool(sb, "EQUIPMENT_FIXED_POOL",   equipFixed);
        AppendPool(sb, "EQUIPMENT_RANDOM_POOL",  equipRandom);
        AppendPool(sb, "CONSUMABLE_FIXED_POOL",  consumFixed);
        AppendPool(sb, "CONSUMABLE_RANDOM_POOL", consumRandom);

        File.WriteAllText(serverPath, sb.ToString(), Encoding.UTF8);
        Log($"   shop_item_pool.py: 장비고정={equipFixed.Count}, 장비랜덤={equipRandom.Count}" +
            $", 소모품고정={consumFixed.Count}, 소모품랜덤={consumRandom.Count} → {serverPath}");
    }

    static void AppendPool(StringBuilder sb, string varName, System.Collections.Generic.List<ItemEntry> items)
    {
        sb.AppendLine($"{varName}: List[Dict] = [");
        foreach (var item in items)
            sb.AppendLine($"    {{\"item_id\": {item.ItemID}}},");
        sb.AppendLine("]");
        sb.AppendLine();
    }

    // ─── 유틸 ───────────────────────────────────────────────────────────────

    T ParseJson<T>(string jsonDir, string tableName) where T : class
    {
        string path = Path.Combine(jsonDir, tableName + ".json");
        if (!File.Exists(path))
        {
            Log("   ⚠️ 파일 없음: " + tableName + ".json");
            return null;
        }

        string raw = File.ReadAllText(path);
        string wrapped = "{\"items\":" + raw + "}";

        try
        {
            return JsonUtility.FromJson<T>(wrapped);
        }
        catch (Exception e)
        {
            Log("   ❌ 파싱 오류 (" + tableName + "): " + e.Message);
            return null;
        }
    }

    T GetOrCreateSO<T>(string fileName) where T : ScriptableObject
    {
        string assetPath = SO_OUTPUT_DIR + "/" + fileName + ".asset";
        T existing = AssetDatabase.LoadAssetAtPath<T>(assetPath);
        if (existing != null) return existing;

        T so = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(so, assetPath);
        return so;
    }

    void Log(string msg)
    {
        _log += "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg + "\n";
        UnityEngine.Debug.Log("[DataImporter] " + msg);
        Repaint();
    }
}
