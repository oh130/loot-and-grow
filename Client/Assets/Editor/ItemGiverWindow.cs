using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools > Item Giver
/// 어드민 계정으로 로그인 후 특정 유저에게 아이템/장비를 즉시 지급하는 에디터 툴.
/// </summary>
public class ItemGiverWindow : EditorWindow
{
    // ─── EditorPrefs 키 ──────────────────────────────────────────────
    private const string PrefUrl     = "ItemGiver_Url";
    private const string PrefToken   = "ItemGiver_Token";
    private const string PrefAdminId = "ItemGiver_AdminDbId";

    // ─── 서버 ────────────────────────────────────────────────────────
    private string _url = "http://43.201.137.146:8000";

    // ─── 인증 ────────────────────────────────────────────────────────
    private string _loginId   = "admin";
    private string _password  = "admin";
    private string _token     = "";
    private int    _adminDbId = 0;
    private bool   _loggedIn  = false;

    // ─── 지급 설정 ───────────────────────────────────────────────────
    private int _targetUserId = 1;
    private int _modeIndex    = 0;   // 0 = 소모품/재료, 1 = 장비
    private int _selectedIdx  = 0;
    private int _quantity     = 1;
    private int _enhanceLevel = 0;

    // ─── 아이템 목록 ─────────────────────────────────────────────────
    private ItemTableSO        _itemTable;
    private EquipmentDetailSO  _equipDetail;
    private List<ItemEntry>    _filtered = new List<ItemEntry>();
    private string[]           _options  = Array.Empty<string>();

    // ─── 상태 ────────────────────────────────────────────────────────
    private string _status = "";
    private bool   _isErr  = false;
    private bool   _isBusy = false;

    // ─────────────────────────────────────────────────────────────────

    [MenuItem("Tools/Item Giver")]
    public static void Open()
    {
        var w = GetWindow<ItemGiverWindow>("Item Giver");
        w.minSize = new Vector2(360, 480);
    }

    private void OnEnable()
    {
        _url      = EditorPrefs.GetString(PrefUrl, "http://43.201.137.146:8000");
        _token    = EditorPrefs.GetString(PrefToken, "");
        _adminDbId = EditorPrefs.GetInt(PrefAdminId, 0);
        _loggedIn = !string.IsNullOrEmpty(_token) && _adminDbId > 0;
        LoadItemTable();
    }

    private void LoadItemTable()
    {
        var guids = AssetDatabase.FindAssets("t:ItemTableSO");
        if (guids.Length > 0)
            _itemTable = AssetDatabase.LoadAssetAtPath<ItemTableSO>(AssetDatabase.GUIDToAssetPath(guids[0]));

        var dGuids = AssetDatabase.FindAssets("t:EquipmentDetailSO");
        if (dGuids.Length > 0)
            _equipDetail = AssetDatabase.LoadAssetAtPath<EquipmentDetailSO>(AssetDatabase.GUIDToAssetPath(dGuids[0]));

        BuildItemList();
    }

    private int GetMaxEnhanceLevel()
    {
        if (_modeIndex != 1 || _filtered.Count == 0 || _selectedIdx >= _filtered.Count)
            return 20;
        if (_equipDetail == null) return 20;

        var detail = _equipDetail.GetById(_filtered[_selectedIdx].ItemID);
        if (detail == null) return 20;

        bool isWeapon = detail.WeaponType == "Sword" || detail.WeaponType == "Staff" || detail.WeaponType == "Bow";
        return isWeapon ? 20 : 10;
    }

    private void BuildItemList()
    {
        if (_itemTable == null || _itemTable.items == null)
        {
            _filtered = new List<ItemEntry>();
            _options = Array.Empty<string>();
            return;
        }

        bool equipMode = _modeIndex == 1;
        _filtered = _itemTable.items
            .Where(i => equipMode ? i.ItemType == "Equipment" : i.ItemType != "Equipment")
            .OrderBy(i => i.ItemID)
            .ToList();

        _options = _filtered.Select(i => $"[{i.ItemID}] {i.Name}").ToArray();
        _selectedIdx = Mathf.Clamp(_selectedIdx, 0, Mathf.Max(0, _options.Length - 1));
    }

    // ─── GUI ─────────────────────────────────────────────────────────

    private void OnGUI()
    {
        EditorGUILayout.Space(4);

        // 서버 설정
        EditorGUILayout.LabelField("서버 설정", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        _url = EditorGUILayout.TextField("서버 주소", _url);
        if (EditorGUI.EndChangeCheck()) EditorPrefs.SetString(PrefUrl, _url);

        EditorGUILayout.Space(6);
        DrawSeparator();

        // 로그인
        EditorGUILayout.LabelField("어드민 로그인", EditorStyles.boldLabel);
        if (!_loggedIn)
        {
            _loginId  = EditorGUILayout.TextField("아이디", _loginId);
            _password = EditorGUILayout.PasswordField("비밀번호", _password);
            EditorGUILayout.Space(2);
            GUI.enabled = !_isBusy;
            if (GUILayout.Button("로그인")) _ = LoginAsync();
            GUI.enabled = true;
        }
        else
        {
            EditorGUILayout.HelpBox($"로그인됨  (DB ID: {_adminDbId})", MessageType.Info);
            if (GUILayout.Button("로그아웃")) Logout();
        }

        if (!_loggedIn) { DrawStatus(); return; }

        EditorGUILayout.Space(8);
        DrawSeparator();

        // 아이템 지급
        EditorGUILayout.LabelField("아이템 지급", EditorStyles.boldLabel);
        _targetUserId = EditorGUILayout.IntField("대상 유저 ID", _targetUserId);

        EditorGUILayout.Space(4);
        EditorGUI.BeginChangeCheck();
        _modeIndex = GUILayout.Toolbar(_modeIndex, new[] { "소모품 / 재료", "장비" });
        if (EditorGUI.EndChangeCheck()) { _selectedIdx = 0; BuildItemList(); }

        EditorGUILayout.Space(4);

        if (_itemTable == null)
        {
            EditorGUILayout.HelpBox("ItemTableSO를 찾을 수 없습니다.", MessageType.Warning);
        }
        else if (_options.Length == 0)
        {
            EditorGUILayout.HelpBox("해당 타입의 아이템이 없습니다.", MessageType.Warning);
        }
        else
        {
            _selectedIdx = EditorGUILayout.Popup("아이템", _selectedIdx, _options);

            if (_modeIndex == 1)
            {
                int maxEnhance = GetMaxEnhanceLevel();
                _enhanceLevel = Mathf.Clamp(_enhanceLevel, 0, maxEnhance);
                _enhanceLevel = EditorGUILayout.IntSlider($"강화 수치 (최대 {maxEnhance}강)", _enhanceLevel, 0, maxEnhance);
            }
            else
                _quantity = Mathf.Max(1, EditorGUILayout.IntField("수량", _quantity));
        }

        EditorGUILayout.Space(10);
        GUI.enabled = !_isBusy && _options.Length > 0;
        if (GUILayout.Button("지급", GUILayout.Height(34))) _ = GiveAsync();
        GUI.enabled = true;

        DrawStatus();
    }

    private void DrawStatus()
    {
        if (string.IsNullOrEmpty(_status)) return;
        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox(_status, _isErr ? MessageType.Error : MessageType.Info);
    }

    private static void DrawSeparator()
    {
        EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1), new Color(0.35f, 0.35f, 0.35f));
        EditorGUILayout.Space(4);
    }

    // ─── HTTP 요청 ───────────────────────────────────────────────────

    private async Task LoginAsync()
    {
        _isBusy = true;
        SetStatus("로그인 중...", false);
        Repaint();
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var body = $"{{\"login_id\":\"{_loginId}\",\"password\":\"{_password}\"}}";
            var res  = await client.PostAsync(
                $"{_url.TrimEnd('/')}/auth/login",
                new StringContent(body, Encoding.UTF8, "application/json"));
            var text = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode) { SetStatus($"로그인 실패: {text}", true); return; }

            var parsed = JsonUtility.FromJson<LoginResult>(text);
            if (string.IsNullOrEmpty(parsed.token)) { SetStatus("토큰을 받지 못했습니다.", true); return; }
            if (parsed.role != "admin")              { SetStatus("어드민 계정이 아닙니다.", true); return; }

            _token    = parsed.token;
            _adminDbId = parsed.id;
            _loggedIn = true;
            EditorPrefs.SetString(PrefToken, _token);
            EditorPrefs.SetInt(PrefAdminId, _adminDbId);
            SetStatus($"로그인 성공 (DB ID: {_adminDbId})", false);
        }
        catch (Exception e) { SetStatus(e.Message, true); }
        finally { _isBusy = false; Repaint(); }
    }

    private void Logout()
    {
        _token = ""; _adminDbId = 0; _loggedIn = false;
        EditorPrefs.DeleteKey(PrefToken);
        EditorPrefs.DeleteKey(PrefAdminId);
        SetStatus("", false);
    }

    private async Task GiveAsync()
    {
        if (_filtered.Count == 0 || _selectedIdx >= _filtered.Count) return;
        _isBusy = true;
        SetStatus("지급 중...", false);
        Repaint();
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _token);

            string baseUrl = _url.TrimEnd('/');
            int    itemId  = _filtered[_selectedIdx].ItemID;
            string endpoint, json;

            if (_modeIndex == 1)
            {
                endpoint = $"{baseUrl}/admin/give/equipment?requester_id={_adminDbId}";
                json = $"{{\"target_user_id\":{_targetUserId},\"item_id\":{itemId},\"enhance_level\":{_enhanceLevel}}}";
            }
            else
            {
                endpoint = $"{baseUrl}/admin/give/item?requester_id={_adminDbId}";
                json = $"{{\"target_user_id\":{_targetUserId},\"item_id\":{itemId},\"quantity\":{_quantity}}}";
            }

            var res  = await client.PostAsync(endpoint,
                new StringContent(json, Encoding.UTF8, "application/json"));
            var text = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode) { SetStatus($"지급 실패: {text}", true); return; }

            string name   = _filtered[_selectedIdx].Name;
            string detail = _modeIndex == 1 ? $"{name} (+{_enhanceLevel})" : $"{name} x{_quantity}";
            SetStatus($"지급 완료 — 유저 {_targetUserId}에게 {detail}", false);
        }
        catch (Exception e) { SetStatus(e.Message, true); }
        finally { _isBusy = false; Repaint(); }
    }

    private void SetStatus(string msg, bool isError) { _status = msg; _isErr = isError; }

    // ─── 역직렬화 전용 래퍼 ──────────────────────────────────────────

    [Serializable]
    private class LoginResult
    {
        public int    id;
        public string token;
        public string role;
    }
}
