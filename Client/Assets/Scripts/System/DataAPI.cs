using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 서버와의 모든 HTTP 통신을 담당하는 중앙 API 클라이언트.
/// 각 Manager는 UnityWebRequest를 직접 쓰지 않고 이 클래스의 메서드를 호출한다.
/// </summary>
public static class DataAPI
{
    private const string BASE_URL = "http://43.201.137.146:8000";

    // ─── 공통 요청 헬퍼 (내부용 — JSON 문자열 그대로 반환) ────────────────────

    // 토큰이 있으면 Authorization 헤더를 붙여주는 공통 함수
    private static void SetAuthHeader(UnityWebRequest req)
    {
        if (!string.IsNullOrEmpty(UserSession.Token))
            req.SetRequestHeader("Authorization", "Bearer " + UserSession.Token);
    }

    private static string ExtractErrorMessage(UnityWebRequest req)
    {
        // req.error는 "HTTP/1.1 400 Bad Request" 같은 HTTP 상태 텍스트만 담겨 있다.
        // FastAPI의 실제 에러 메시지({"detail": "..."})는 downloadHandler.text에 있다.
        string body = req.downloadHandler?.text;
        if (!string.IsNullOrEmpty(body)) return body;
        return req.error;
    }

    private static IEnumerator Get(string endpoint, Action<string> onSuccess, Action<string> onError)
    {
        using UnityWebRequest req = UnityWebRequest.Get(BASE_URL + endpoint);
        SetAuthHeader(req);
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            onSuccess?.Invoke(req.downloadHandler.text);
        else
            onError?.Invoke(ExtractErrorMessage(req));
    }

    private static IEnumerator Post(string endpoint, string bodyJson, Action<string> onSuccess, Action<string> onError)
    {
        using UnityWebRequest req = new UnityWebRequest(BASE_URL + endpoint, "POST");
        req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(bodyJson));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        SetAuthHeader(req);
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            onSuccess?.Invoke(req.downloadHandler.text);
        else
            onError?.Invoke(ExtractErrorMessage(req));
    }

    private static IEnumerator Put(string endpoint, string bodyJson, Action<string> onSuccess, Action<string> onError)
    {
        using UnityWebRequest req = new UnityWebRequest(BASE_URL + endpoint, "PUT");
        req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(bodyJson));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        SetAuthHeader(req);
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            onSuccess?.Invoke(req.downloadHandler.text);
        else
            onError?.Invoke(ExtractErrorMessage(req));
    }

    private static IEnumerator Delete(string endpoint, string bodyJson, Action<string> onSuccess, Action<string> onError)
    {
        using UnityWebRequest req = new UnityWebRequest(BASE_URL + endpoint, "DELETE");
        req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(bodyJson));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        SetAuthHeader(req);
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            onSuccess?.Invoke(req.downloadHandler.text);
        else
            onError?.Invoke(ExtractErrorMessage(req));
    }

    // ─── 제네릭 헬퍼 — 호출부에서 FromJson 안 써도 되게 ──────────────────────

    private static IEnumerator Get<T>(string endpoint, Action<T> onSuccess, Action<string> onError) where T : class
    {
        yield return Get(endpoint, json => onSuccess?.Invoke(JsonUtility.FromJson<T>(json)), onError);
    }

    private static IEnumerator Post<T>(string endpoint, string bodyJson, Action<T> onSuccess, Action<string> onError) where T : class
    {
        yield return Post(endpoint, bodyJson, json => onSuccess?.Invoke(JsonUtility.FromJson<T>(json)), onError);
    }

    private static IEnumerator Put<T>(string endpoint, string bodyJson, Action<T> onSuccess, Action<string> onError) where T : class
    {
        yield return Put(endpoint, bodyJson, json => onSuccess?.Invoke(JsonUtility.FromJson<T>(json)), onError);
    }

    // ─── Auth ──────────────────────────────────────────────────────────────

    /// <summary>
    /// 로그인 요청.
    /// 성공 시 onSuccess에 LoginResponse가 전달되며, UserSession.Apply(res)로 세션에 저장한다.
    /// 실패 시 onError에 에러 문자열이 전달된다 (잘못된 ID/PW, 서버 오류 등).
    /// </summary>
    /// <param name="loginId">로그인 아이디</param>
    /// <param name="password">비밀번호</param>
    /// <example>
    /// yield return StartCoroutine(DataAPI.Login("myId", "myPw",
    ///     onSuccess: res => {
    ///         UserSession.Apply(res);
    ///     },
    ///     onError: err => statusText.text = "아이디 또는 비밀번호가 틀렸습니다."
    /// ));
    /// </example>
    public static IEnumerator Login(string loginId, string password, Action<LoginResponse> onSuccess, Action<string> onError = null)
    {
        string body = JsonUtility.ToJson(new LoginData { login_id = loginId, password = password });
        yield return Post<LoginResponse>("/auth/login", body, onSuccess, onError);
    }

    /// <summary>
    /// 토큰 유효성 검증.
    /// 게임 서버(NGO 데디케이티드 서버)가 클라이언트 접속 시 토큰을 검증할 때 사용한다.
    /// UserSession.Token이 아닌 외부에서 받은 토큰을 직접 넘기는 방식이라
    /// 일반 클라이언트 코드에서는 사용할 일이 없다.
    /// </summary>
    /// <param name="token">검증할 JWT 토큰 문자열</param>
    /// <example>
    /// // 게임 서버의 ConnectionApprovalCallback 내부에서 호출
    /// yield return StartCoroutine(DataAPI.VerifyToken(clientToken,
    ///     onSuccess: res => {
    ///         response.Approved = true;
    ///         Debug.Log($"인증 성공: {res.username} (id={res.id}, role={res.role})");
    ///     },
    ///     onError: _ => response.Approved = false
    /// ));
    /// </example>
    public static IEnumerator VerifyToken(string token, Action<VerifyResponse> onSuccess, Action<string> onError = null)
    {
        using UnityWebRequest req = UnityWebRequest.Get(BASE_URL + "/auth/verify");
        req.SetRequestHeader("Authorization", "Bearer " + token);
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            onSuccess?.Invoke(JsonUtility.FromJson<VerifyResponse>(req.downloadHandler.text));
        else
            onError?.Invoke(req.error);
    }

    /// <summary>
    /// 회원가입 요청.
    /// 성공 시 생성된 유저 정보(LoginResponse)가 반환된다.
    /// 아이디 중복이면 onError 호출.
    /// </summary>
    /// <param name="loginId">사용할 로그인 아이디 (중복 불가)</param>
    /// <param name="password">비밀번호</param>
    /// <param name="username">게임에서 표시될 닉네임</param>
    /// <example>
    /// yield return StartCoroutine(DataAPI.Register("myId", "myPw", "홍길동",
    ///     onSuccess: res => Debug.Log("가입 완료: " + res.username),
    ///     onError:   err => statusText.text = "이미 존재하는 아이디입니다."
    /// ));
    /// </example>
    public static IEnumerator Register(string loginId, string password, string username, Action<LoginResponse> onSuccess, Action<string> onError = null)
    {
        string body = JsonUtility.ToJson(new RegisterData { login_id = loginId, password = password, username = username });
        yield return Post<LoginResponse>("/auth/register", body, onSuccess, onError);
    }

    // ─── Inventory ─────────────────────────────────────────────────────────

    /// <summary>
    /// 유저의 인벤토리 전체 조회.
    /// 응답의 res.items 리스트를 순회해 슬롯 UI를 구성한다.
    /// </summary>
    /// <param name="userId">UserSession.DbUserId를 넘긴다</param>
    /// <example>
    /// yield return StartCoroutine(DataAPI.GetInventory(UserSession.DbUserId,
    ///     onSuccess: res => PopulateSlots(res.items),
    ///     onError:   err => Debug.LogError(err)
    /// ));
    /// </example>
    public static IEnumerator GetInventory(int userId, Action<InventoryResponse> onSuccess, Action<string> onError = null)
    {
        yield return Get<InventoryResponse>($"/inventory/{userId}", onSuccess, onError);
    }

    /// <summary>
    /// 장비 아이템을 인벤토리에 추가. 항상 새 슬롯 생성 (스택 없음), enhance_level 보존.
    /// 인벤토리가 20칸을 초과하면 onError("가방이 꽉 찼습니다") 호출.
    /// 장착 교체 시에는 DoEquip에서 먼저 새 장비를 장착/삭제한 뒤 이 함수를 호출해야
    /// 슬롯 수가 유지되어 20칸 제한에 걸리지 않는다.
    /// </summary>
    /// <param name="userId">UserSession.DbUserId를 넘긴다</param>
    /// <param name="itemId">ItemTableSO의 ItemID 값</param>
    /// <param name="enhanceLevel">강화 레벨 (0 이상)</param>
    /// <example>
    /// // 장비 해제 후 인벤토리로 반환
    /// yield return StartCoroutine(DataAPI.AddEquipment(UserSession.DbUserId, itemId, enhanceLevel,
    ///     onSuccess: _ => InventoryManager.Instance.Refresh(),
    ///     onError: err => Debug.LogError(err)
    /// ));
    /// </example>
    public static IEnumerator AddEquipment(int userId, int itemId, int enhanceLevel, Action<InventoryItemResponse> onSuccess = null, Action<string> onError = null)
    {
        string body = JsonUtility.ToJson(new InventoryAddEquipmentData { user_id = userId, item_id = itemId, enhance_level = enhanceLevel });
        yield return Post<InventoryItemResponse>("/inventory/add/equipment", body, onSuccess, onError);
    }

    /// <summary>
    /// 소모품/재료를 인벤토리에 추가. 루팅, 구매, 퀘스트 보상 등.
    /// 퀵슬롯에 같은 item_id가 있으면 거기에 먼저 합산, 없으면 일반 인벤토리에 합산.
    /// 일반 인벤토리 20칸이 꽉 찼으면 onError("가방이 꽉 찼습니다") 호출.
    /// </summary>
    /// <param name="userId">UserSession.DbUserId를 넘긴다</param>
    /// <param name="itemId">ItemTableSO의 ItemID 값 (예: 포션=4000)</param>
    /// <param name="quantity">추가할 수량</param>
    /// <example>
    /// yield return StartCoroutine(DataAPI.AddItem(UserSession.DbUserId, 4000, 3));
    /// </example>
    public static IEnumerator AddItem(int userId, int itemId, int quantity = 1, Action<InventoryItemResponse> onSuccess = null, Action<string> onError = null)
    {
        string body = JsonUtility.ToJson(new InventoryAddItemData { user_id = userId, item_id = itemId, quantity = quantity });
        yield return Post<InventoryItemResponse>("/inventory/add/item", body, onSuccess, onError);
    }

    /// <summary>
    /// 소모품을 퀵슬롯에 직접 추가. 인벤토리가 꽉 찼을 때 PickupItem의 fallback으로 사용.
    /// 같은 item_id가 이미 퀵슬롯에 있으면 수량 합산, 없으면 빈 슬롯(0~4)에 배정.
    /// 퀵슬롯도 꽉 찼으면 onError("퀵슬롯이 꽉 찼습니다") 호출.
    /// </summary>
    /// <param name="userId">UserSession.DbUserId를 넘긴다</param>
    /// <param name="itemId">ItemTableSO의 ItemID 값</param>
    /// <param name="quantity">추가할 수량</param>
    /// <example>
    /// // InventoryManager.PickupItem 내부 — 인벤토리 꽉 찼을 때 호출
    /// yield return StartCoroutine(DataAPI.AddToQuickSlot(UserSession.DbUserId, itemId, quantity,
    ///     onSuccess: _ => Refresh(),
    ///     onError: err => { if (err.Contains("퀵슬롯이 꽉 찼습니다")) ShowFullInventoryMessage(); }
    /// ));
    /// </example>
    public static IEnumerator AddToQuickSlot(int userId, int itemId, int quantity = 1, Action<InventoryItemResponse> onSuccess = null, Action<string> onError = null)
    {
        string body = JsonUtility.ToJson(new InventoryAddItemData { user_id = userId, item_id = itemId, quantity = quantity });
        yield return Post<InventoryItemResponse>("/inventory/add/quickslot", body, onSuccess, onError);
    }

    /// <summary>
    /// 일반 인벤토리 소모품을 퀵슬롯(0~4)에 등록.
    /// 해당 슬롯이 이미 사용 중이거나 아이템이 이미 퀵슬롯에 있으면 onError 호출.
    /// 등록된 아이템은 20칸 카운트에서 제외된다.
    /// </summary>
    /// <param name="userId">UserSession.DbUserId를 넘긴다</param>
    /// <param name="inventoryId">등록할 아이템의 인벤토리 슬롯 id (InventoryItemResponse.id)</param>
    /// <param name="slotIndex">배정할 퀵슬롯 인덱스 (0~4)</param>
    /// <example>
    /// // QuickSlotManager.DoAssign 내부
    /// yield return StartCoroutine(DataAPI.AssignQuickSlot(UserSession.DbUserId, inventoryId, freeIndex,
    ///     onSuccess: _ => InventoryManager.Instance?.Refresh(),
    ///     onError:   err => Debug.LogError(err)
    /// ));
    /// </example>
    public static IEnumerator AssignQuickSlot(int userId, int inventoryId, int slotIndex, Action<InventoryItemResponse> onSuccess = null, Action<string> onError = null)
    {
        string body = JsonUtility.ToJson(new QuickslotAssignData { user_id = userId, inventory_id = inventoryId, slot_index = slotIndex });
        yield return Put<InventoryItemResponse>("/inventory/quickslot/assign", body, onSuccess, onError);
    }

    /// <summary>
    /// 퀵슬롯 해제. 아이템이 일반 인벤토리로 복귀 (20칸 카운트에 포함됨).
    /// 일반 인벤토리가 꽉 찼으면 onError 호출.
    /// </summary>
    /// <param name="userId">UserSession.DbUserId를 넘긴다</param>
    /// <param name="inventoryId">해제할 아이템의 인벤토리 슬롯 id (InventoryItemResponse.id)</param>
    /// <example>
    /// // QuickSlotManager.DoUnassign 내부
    /// yield return StartCoroutine(DataAPI.UnassignQuickSlot(UserSession.DbUserId, inventoryId,
    ///     onSuccess: _ => InventoryManager.Instance?.Refresh(),
    ///     onError:   err => Debug.LogError(err)
    /// ));
    /// </example>
    public static IEnumerator UnassignQuickSlot(int userId, int inventoryId, Action<InventoryItemResponse> onSuccess = null, Action<string> onError = null)
    {
        string body = JsonUtility.ToJson(new QuickslotUnassignData { user_id = userId, inventory_id = inventoryId });
        yield return Put<InventoryItemResponse>("/inventory/quickslot/unassign", body, onSuccess, onError);
    }

    /// <summary>
    /// 아이템 수량 소모.
    /// 수량이 0이 되면 슬롯 자체가 삭제된다. 수량 부족 시 onError 호출.
    /// </summary>
    /// <param name="userId">UserSession.DbUserId를 넘긴다</param>
    /// <param name="inventoryId">소모할 슬롯의 id (InventoryItemResponse.id)</param>
    /// <param name="quantity">소모할 수량</param>
    /// <example>
    /// // 포션 1개 소모
    /// yield return StartCoroutine(DataAPI.ConsumeItem(UserSession.DbUserId, slotData.InventoryId, 1));
    /// </example>
    public static IEnumerator ConsumeItem(int userId, int inventoryId, int quantity, Action<InventoryItemResponse> onSuccess = null, Action<string> onError = null)
    {
        string body = JsonUtility.ToJson(new InventoryConsumeData { user_id = userId, inventory_id = inventoryId, quantity = quantity });
        yield return Post<InventoryItemResponse>("/inventory/consume", body, onSuccess, onError);
    }

    /// <summary>
    /// 인벤토리 슬롯 삭제.
    /// inventoryId는 아이템의 itemId가 아니라 인벤토리 슬롯 자체의 id다.
    /// InventoryItemResponse.id 값을 사용한다.
    /// </summary>
    /// <param name="userId">UserSession.DbUserId를 넘긴다 (본인 아이템인지 서버에서 확인)</param>
    /// <param name="inventoryId">삭제할 슬롯의 id (InventoryItemResponse.id)</param>
    /// <example>
    /// // slotData.InventoryId는 서버에서 받은 인벤토리 슬롯 id
    /// yield return StartCoroutine(DataAPI.DeleteItem(UserSession.DbUserId, slotData.InventoryId));
    /// </example>
    public static IEnumerator DeleteItem(int userId, int inventoryId, Action<string> onSuccess = null, Action<string> onError = null)
    {
        string body = JsonUtility.ToJson(new InventoryDeleteData { user_id = userId, inventory_id = inventoryId });
        yield return Delete("/inventory/delete", body, onSuccess, onError);
    }

    /// <summary>
    /// 어드민 전용: 타 유저의 인벤토리 슬롯 삭제.
    /// 서버가 admin 계정으로 로그인된 상태에서만 호출 가능하다 (사망 처리 등).
    /// </summary>
    /// <param name="userId">슬롯을 삭제할 대상 유저의 DB ID</param>
    /// <param name="inventoryId">삭제할 슬롯의 id (InventoryItemResponse.id)</param>
    /// <example>
    /// // 사망한 플레이어의 아이템 제거 (서버 측 어드민 계정으로 호출)
    /// yield return StartCoroutine(DataAPI.AdminDeleteItem(targetUserId, inventoryId,
    ///     onSuccess: _ => Debug.Log("아이템 제거 완료"),
    ///     onError:   err => Debug.LogError(err)
    /// ));
    /// </example>
    public static IEnumerator AdminDeleteItem(int userId, int inventoryId, Action<string> onSuccess = null, Action<string> onError = null)
    {
        string body = JsonUtility.ToJson(new InventoryDeleteData { user_id = userId, inventory_id = inventoryId });
        yield return Delete("/inventory/admin/delete", body, onSuccess, onError);
    }

    // ─── Character ─────────────────────────────────────────────────────────

    /// <summary>
    /// 캐릭터 정보 조회 (HP, 골드, 마지막 위치).
    /// 씬 로드 시 호출해 캐릭터 상태를 복원할 때 사용한다.
    /// </summary>
    /// <param name="userId">UserSession.DbUserId를 넘긴다</param>
    /// <example>
    /// yield return StartCoroutine(DataAPI.GetCharacter(UserSession.DbUserId,
    ///     onSuccess: res => {
    ///         hp = res.current_hp;
    ///         transform.position = new Vector3(res.pos_x, res.pos_y, res.pos_z);
    ///     }
    /// ));
    /// </example>
    public static IEnumerator GetCharacter(int userId, Action<CharacterResponse> onSuccess, Action<string> onError = null)
    {
        yield return Get<CharacterResponse>($"/character/{userId}", onSuccess, onError);
    }

    /// <summary>
    /// 캐릭터 정보 저장.
    /// CharacterUpdateData에서 바꿀 필드만 채우면 서버는 해당 필드만 업데이트한다.
    /// 변경할 필드만 채우면 되고, null인 필드는 서버에서 건드리지 않는다.
    /// 게임 종료 또는 씬 전환 시 위치/스탯 저장에 사용한다.
    /// </summary>
    /// <param name="userId">UserSession.DbUserId를 넘긴다</param>
    /// <param name="data">변경할 필드만 채운 CharacterUpdateData 객체</param>
    /// <example>
    /// // 위치만 저장
    /// var data = new CharacterUpdateData {
    ///     pos_x = transform.position.x,
    ///     pos_y = transform.position.y,
    ///     pos_z = transform.position.z
    /// };
    /// yield return StartCoroutine(DataAPI.UpdateCharacter(UserSession.DbUserId, data));
    /// </example>
    public static IEnumerator UpdateCharacter(int userId, CharacterUpdateData data, Action<CharacterResponse> onSuccess = null, Action<string> onError = null)
    {
        string body = JsonUtility.ToJson(data);
        yield return Put<CharacterResponse>($"/character/{userId}", body, onSuccess, onError);
    }

    // ─── Equipment ─────────────────────────────────────────────────────────

    /// <summary>
    /// 유저의 장착 장비 목록 조회.
    /// 슬롯은 "weapon" / "helmet" / "top" / "bottom" / "accessory" 5종류이며 각각 최대 1개씩 장착 가능하다.
    /// </summary>
    /// <param name="userId">UserSession.DbUserId를 넘긴다</param>
    /// <example>
    /// yield return StartCoroutine(DataAPI.GetEquipped(UserSession.DbUserId,
    ///     onSuccess: res => {
    ///         foreach (var equip in res.items)
    ///             Debug.Log($"{equip.slot_type} 슬롯: item_id {equip.item_id}");
    ///     }
    /// ));
    /// </example>
    public static IEnumerator GetEquipped(int userId, Action<EquippedItemListResponse> onSuccess, Action<string> onError = null)
    {
        yield return Get($"/equipment/{userId}",
            json => onSuccess?.Invoke(JsonUtility.FromJson<EquippedItemListResponse>("{\"items\":" + json + "}")),
            onError);
    }

    /// <summary>
    /// 아이템 장착.
    /// 해당 슬롯에 이미 장착된 게 있으면 자동으로 교체된다.
    /// </summary>
    /// <param name="userId">UserSession.DbUserId를 넘긴다</param>
    /// <param name="slotType">"weapon" / "helmet" / "top" / "bottom" / "accessory" 중 하나</param>
    /// <param name="itemId">장착할 아이템의 ItemTableSO ItemID (예: 검=3000)</param>
    /// <example>
    /// // 검(3000)을 weapon 슬롯에 장착
    /// yield return StartCoroutine(DataAPI.EquipItem(UserSession.DbUserId, "weapon", 3000));
    /// </example>
    public static IEnumerator EquipItem(int userId, string slotType, int itemId, int enhanceLevel = 0, Action<EquippedItemResponse> onSuccess = null, Action<string> onError = null)
    {
        string body = JsonUtility.ToJson(new EquipData { user_id = userId, slot_type = slotType, item_id = itemId, enhance_level = enhanceLevel });
        yield return Post<EquippedItemResponse>("/equipment/equip", body, onSuccess, onError);
    }

    /// <summary>
    /// 장착 해제.
    /// 해당 슬롯의 장비를 제거한다. 슬롯이 비어있으면 서버에서 404 에러가 반환된다.
    /// </summary>
    /// <param name="userId">UserSession.DbUserId를 넘긴다</param>
    /// <param name="slotType">"weapon" / "helmet" / "top" / "bottom" / "accessory" 중 하나</param>
    /// <example>
    /// yield return StartCoroutine(DataAPI.UnequipItem(UserSession.DbUserId, "weapon"));
    /// </example>
    public static IEnumerator UnequipItem(int userId, string slotType, Action<string> onSuccess = null, Action<string> onError = null)
    {
        string body = JsonUtility.ToJson(new UnequipData { user_id = userId, slot_type = slotType });
        yield return Post("/equipment/unequip", body, onSuccess, onError);
    }

    // ─── Enhance ───────────────────────────────────────────────────────────

    /// <summary>
    /// 무기 강화 요청. 강화석을 소모하고 성공 여부와 새 강화 단계를 반환한다.
    /// success_rate, req_stone은 EnhanceTableSO.GetByLevel(현재레벨 + 1)에서 읽어 전달한다.
    /// 강화석 부족 시 onError("강화석이 부족합니다"), 최대 강화 시 onError("최대 강화 단계입니다").
    /// </summary>
    /// <param name="userId">UserSession.DbUserId를 넘긴다</param>
    /// <param name="inventoryId">강화할 무기 슬롯 id (InventoryItemResponse.id)</param>
    /// <param name="stoneInventoryId">강화석 슬롯 id (InventoryItemResponse.id)</param>
    /// <param name="successRate">EnhanceEntry.SuccessRate (정수 %, 예: 95)</param>
    /// <param name="reqStone">EnhanceEntry.ReqStone (소모 강화석 수)</param>
    /// <example>
    /// var entry = enhanceTableSO.GetByLevel(weapon.EnhanceLevel + 1);
    /// yield return StartCoroutine(DataAPI.EnhanceWeapon(
    ///     UserSession.DbUserId, weapon.InventoryId, stone.InventoryId,
    ///     (int)entry.SuccessRate, entry.ReqStone,
    ///     onSuccess: res => {
    ///         if (res.success) Debug.Log($"+{res.new_enhance_level} 강화 성공!");
    ///         else             Debug.Log("강화 실패");
    ///         InventoryManager.Instance?.Refresh();
    ///     },
    ///     onError: err => Debug.LogError(err)
    /// ));
    /// </example>
    public static IEnumerator EnhanceWeapon(int userId, int inventoryId, int stoneInventoryId, int successRate, int reqStone, Action<EnhanceResponse> onSuccess = null, Action<string> onError = null)
    {
        string body = JsonUtility.ToJson(new EnhanceData
        {
            user_id             = userId,
            inventory_id        = inventoryId,
            stone_inventory_id  = stoneInventoryId,
            success_rate        = successRate,
            req_stone           = reqStone,
        });
        yield return Post<EnhanceResponse>("/enhance/weapon", body, onSuccess, onError);
    }

    [Serializable] private class EnhanceData { public int user_id; public int inventory_id; public int stone_inventory_id; public int success_rate; public int req_stone; }

    // ─── Shop ──────────────────────────────────────────────────────────────

    /// <summary>
    /// 현재 상점 로테이션 아이템 목록 조회.
    /// 만료됐거나 비어있으면 서버가 자동 재생성한다.
    /// </summary>
    /// <example>
    /// yield return StartCoroutine(DataAPI.GetShopItems(
    ///     onSuccess: res => ShopPanel에 res.items 전달,
    ///     onError:   err => Debug.LogError(err)
    /// ));
    /// </example>
    public static IEnumerator GetShopItems(string shopType, Action<ShopItemsResponse> onSuccess, Action<string> onError = null)
    {
        yield return Get<ShopItemsResponse>($"/shop/items?shop_type={shopType}", onSuccess, onError);
    }

    /// <summary>
    /// 상점 아이템 구매. buy_price는 클라이언트 SO(ItemEntry.Buy)에서 읽어 전달한다.
    /// 성공 시 구매 후 남은 골드(ShopBuyResponse.gold)가 반환된다.
    /// 골드 부족: onError("골드가 부족합니다"), 인벤토리 꽉 참: onError("가방이 꽉 찼습니다").
    /// </summary>
    /// <param name="userId">UserSession.DbUserId를 넘긴다</param>
    /// <param name="shopRotationId">ShopItemResponse.id</param>
    /// <param name="buyPrice">ItemEntry.Buy</param>
    /// <param name="itemType">ItemEntry.ItemType — 서버가 인벤토리 추가 방식 결정에 사용</param>
    /// <example>
    /// yield return StartCoroutine(DataAPI.BuyShopItem(UserSession.DbUserId, item.id, itemInfo.Buy, itemInfo.ItemType,
    ///     onSuccess: res => { InventoryManager.Instance?.SetGold(res.gold); },
    ///     onError:   err => Debug.LogError(err)
    /// ));
    /// </example>
    public static IEnumerator BuyShopItem(int userId, int itemId, int shopRotationId, int buyPrice, string itemType, int quantity, Action<ShopBuyResponse> onSuccess = null, Action<string> onError = null)
    {
        string body = JsonUtility.ToJson(new ShopBuyData { user_id = userId, item_id = itemId, shop_rotation_id = shopRotationId, buy_price = buyPrice, item_type = itemType, quantity = quantity });
        yield return Post<ShopBuyResponse>("/shop/buy", body, onSuccess, onError);
    }

    /// <summary>
    /// 인벤토리 아이템 판매. 판매 단가는 ItemEntry.Sell을 그대로 전달한다.
    /// 성공 시 판매 후 남은 골드와 판매 총액(ShopSellResponse)이 반환된다.
    /// </summary>
    /// <param name="userId">UserSession.DbUserId를 넘긴다</param>
    /// <param name="inventoryId">판매할 슬롯의 id (InventoryItemResponse.id)</param>
    /// <param name="sellPrice">ItemEntry.Sell 단가</param>
    /// <param name="quantity">판매 수량</param>
    /// <example>
    /// yield return StartCoroutine(DataAPI.SellItem(UserSession.DbUserId, data.InventoryId, data.ItemInfo.Sell, quantity,
    ///     onSuccess: res => { InventoryManager.Instance?.SetGold(res.gold); },
    ///     onError:   err => Debug.LogError(err)
    /// ));
    /// </example>
    public static IEnumerator SellItem(int userId, int inventoryId, int sellPrice, int quantity, Action<ShopSellResponse> onSuccess = null, Action<string> onError = null)
    {
        string body = JsonUtility.ToJson(new ShopSellData { user_id = userId, inventory_id = inventoryId, sell_price = sellPrice, quantity = quantity });
        yield return Post<ShopSellResponse>("/shop/sell", body, onSuccess, onError);
    }

    // ─── Storage ───────────────────────────────────────────────────────────────

    /// <summary>
    /// 창고 전체 조회.
    /// </summary>
    /// <param name="userId">UserSession.DbUserId를 넘긴다</param>
    /// <example>
    /// yield return StartCoroutine(DataAPI.GetStorage(UserSession.DbUserId,
    ///     onSuccess: res => PopulateStorageSlots(res.items),
    ///     onError:   err => Debug.LogError(err)
    /// ));
    /// </example>
    public static IEnumerator GetStorage(int userId, Action<StorageResponse> onSuccess, Action<string> onError = null)
    {
        yield return Get<StorageResponse>($"/storage/{userId}", onSuccess, onError);
    }

    /// <summary>
    /// 인벤토리 아이템을 창고로 이동.
    /// 창고가 20칸 꽉 찼으면 onError("창고가 꽉 찼습니다") 호출.
    /// </summary>
    /// <param name="userId">UserSession.DbUserId를 넘긴다</param>
    /// <param name="inventoryId">이동할 인벤토리 슬롯 id (InventoryItemResponse.id)</param>
    /// <param name="quantity">이동할 수량</param>
    /// <example>
    /// yield return StartCoroutine(DataAPI.DepositItem(UserSession.DbUserId, slot.InventoryId, quantity,
    ///     onSuccess: _ => { StoragePanel.Instance?.Refresh(); InventoryManager.Instance?.Refresh(); },
    ///     onError:   err => Debug.LogError(err)
    /// ));
    /// </example>
    public static IEnumerator DepositItem(int userId, int inventoryId, int quantity, Action<StorageItemResponse> onSuccess = null, Action<string> onError = null)
    {
        string body = JsonUtility.ToJson(new StorageDepositData { user_id = userId, inventory_id = inventoryId, quantity = quantity });
        yield return Post<StorageItemResponse>("/storage/deposit", body, onSuccess, onError);
    }

    /// <summary>
    /// 창고 아이템을 인벤토리로 이동.
    /// 인벤토리가 20칸 꽉 찼으면 onError("가방이 꽉 찼습니다") 호출.
    /// </summary>
    /// <param name="userId">UserSession.DbUserId를 넘긴다</param>
    /// <param name="storageId">이동할 창고 슬롯 id (StorageItemResponse.id)</param>
    /// <param name="quantity">이동할 수량</param>
    /// <example>
    /// yield return StartCoroutine(DataAPI.WithdrawItem(UserSession.DbUserId, slot.StorageId, quantity,
    ///     onSuccess: _ => { StoragePanel.Instance?.Refresh(); InventoryManager.Instance?.Refresh(); },
    ///     onError:   err => Debug.LogError(err)
    /// ));
    /// </example>
    public static IEnumerator WithdrawItem(int userId, int storageId, int quantity, Action<InventoryItemResponse> onSuccess = null, Action<string> onError = null)
    {
        string body = JsonUtility.ToJson(new StorageWithdrawData { user_id = userId, storage_id = storageId, quantity = quantity });
        yield return Post<InventoryItemResponse>("/storage/withdraw", body, onSuccess, onError);
    }

    // ─── Admin ─────────────────────────────────────────────────────────
    /// <summary>
    /// DB에 저장된 전체 유저 리스트를 가져온다. (어드민 전용)
    /// </summary>
    /// <param name="requesterId">요청하는 어드민의 DB ID</param>
    /// <example>
    /// int targetId = 2;
    /// int myId = UserSession.DbUserId;
    /// yield return StartCoroutine(DataAPI.GetAllUsers(myId,
    ///     onSuccess: userList => {
    ///         Debug.Log($"전체 유저 {userList.Count}명 로드 성공");
    ///         foreach (var user in userList)
    ///         {
    ///             Debug.Log($"[{user.id}] {user.username} ({user.role}) - 밴 여부: {user.is_banned}");
    ///         }
    ///     },
    ///     onError: err => Debug.LogError($"유저 리스트 로드 실패: {err}")
    /// ));
    /// </example>
    public static IEnumerator GetAllUsers(int requesterId, Action<List<AdminUserResponse>> onSuccess, Action<string> onError = null)
    {
        string endpoint = $"/admin/users?requester_id={requesterId}";
        yield return Get(endpoint, 
            onSuccess: json => {
                string newJson = "{ \"users\": " + json + " }";
                var wrapper = JsonUtility.FromJson<AdminUserListWrapper>(newJson);
                onSuccess?.Invoke(wrapper.users);
            }, 
            onError: onError
        );
    }

    /// <summary>
    /// 특정 유저 계정을 삭제한다. (어드민 전용)
    /// 인벤토리·캐릭터·장착 장비가 전부 삭제되며 복구 불가.
    /// 어드민 계정은 삭제 불가 (서버에서 403 반환).
    /// </summary>
    /// <param name="targetUserId">삭제할 유저의 DB ID</param>
    /// <param name="requesterId">요청하는 어드민의 DB ID</param>
    /// <example>
    /// int targetId = 2;
    /// int myId = UserSession.DbUserId; // 내 ID (이미 어드민이어야 함)
    /// yield return StartCoroutine(DataAPI.DeleteUser(targetId, myId,
    ///     onSuccess: _ => Debug.Log($"{targetId}번 유저가 삭제되었습니다."),
    ///     onError:   err => Debug.LogError($"삭제 실패: {err}")
    /// ));
    /// </example>
    public static IEnumerator DeleteUser(int targetUserId, int requesterId, Action<string> onSuccess = null, Action<string> onError = null)
    {
        string endpoint = $"/admin/users/{targetUserId}?requester_id={requesterId}";
        // body 없는 DELETE — 빈 JSON 전달
        yield return Delete(endpoint, "{}", onSuccess, onError);
    }

    /// <summary>
    /// 특정 유저를 밴(금지)하거나 밴을 해제한다. (어드민 전용)
    /// </summary>
    /// <param name="targetUserId">밴/해제 대상의 DB ID</param>
    /// <param name="requesterId">명령을 내리는 어드민의 DB ID</param>
    /// <param name="isBanned">true면 밴, false면 밴 해제</param>
    /// <example>
    /// int targetId = 2;
    /// int myId = UserSession.DbUserId;
    /// yield return StartCoroutine(DataAPI.UpdateUserBanStatus(targetId, myId, true,
    ///     onSuccess: res => {
    ///         Debug.Log($"{targetId}번 유저가 차단되었습니다.");
    ///         // 필요하다면 여기서 해당 유저를 서버에서 킥(Kick)하는 로직 연동
    ///     },
    ///     onError: err => Debug.LogError($"차단 실패: {err}")
    /// ));
    /// </example>
    public static IEnumerator UpdateUserBanStatus(int targetUserId, int requesterId, bool isBanned, Action<string> onSuccess = null, Action<string> onError = null)
    {
        string endpoint = $"/admin/users/{targetUserId}/ban?requester_id={requesterId}";
        string body = JsonUtility.ToJson(new BanUpdateData { is_banned = isBanned });
        yield return Put(endpoint, body, onSuccess, onError);
    }

    /// <summary>
    /// 유저의 권한(Role)을 변경한다. (어드민 전용)
    /// </summary>
    /// <param name="targetUserId">권한을 바꿀 대상의 DB ID</param>
    /// <param name="requesterId">명령을 내리는 어드민의 DB ID</param>
    /// <param name="newRole">"admin" 또는 "user"</param>
    /// <example>
    /// int targetId = 2;
    /// int myId = UserSession.DbUserId; // 내 ID (이미 어드민이어야 함)
    /// yield return StartCoroutine(DataAPI.UpdateUserRole(targetId, myId, "admin",
    ///     onSuccess: res => {
    ///         Debug.Log($"{targetId}번 유저가 어드민으로 승격되었습니다.");
    ///     },
    ///     onError: err => {
    ///         Debug.LogError($"권한 변경 실패: {err}");
    ///     }
    /// ));
    /// </example>
    public static IEnumerator UpdateUserRole(int targetUserId, int requesterId, string newRole, Action<string> onSuccess = null, Action<string> onError = null)
    {
        string endpoint = $"/admin/users/{targetUserId}/role?requester_id={requesterId}";
        string body = JsonUtility.ToJson(new RoleUpdateData { role = newRole });
        yield return Put(endpoint, body, onSuccess, onError);
    }

    // ─── 요청 바디용 내부 클래스 ────────────────────────────────────────────

    [Serializable] private class LoginData        { public string login_id; public string password; }
    [Serializable] private class RegisterData     { public string login_id; public string password; public string username; }
    [Serializable] private class InventoryAddEquipmentData { public int user_id; public int item_id; public int enhance_level; }
    [Serializable] private class InventoryAddItemData      { public int user_id; public int item_id; public int quantity; }
    [Serializable] private class QuickslotAssignData       { public int user_id; public int inventory_id; public int slot_index; }
    [Serializable] private class QuickslotUnassignData     { public int user_id; public int inventory_id; }
    [Serializable] private class InventoryConsumeData { public int user_id; public int inventory_id; public int quantity; }
    [Serializable] private class InventoryDeleteData  { public int user_id; public int inventory_id; }
    [Serializable] private class EquipData        { public int user_id; public string slot_type; public int item_id; public int enhance_level; }
    [Serializable] private class UnequipData      { public int user_id; public string slot_type; }
    [Serializable] private class BanUpdateData { public bool is_banned; }
    [Serializable] private class RoleUpdateData { public string role; }
    [Serializable] private class ShopBuyData       { public int user_id; public int item_id; public int shop_rotation_id; public int buy_price; public string item_type; public int quantity; }
    [Serializable] private class ShopSellData      { public int user_id; public int inventory_id; public int sell_price; public int quantity; }
    [Serializable] private class StorageDepositData  { public int user_id; public int inventory_id; public int quantity; }
    [Serializable] private class StorageWithdrawData { public int user_id; public int storage_id;   public int quantity; }

    // ─── 외부 사용 필요 클래스 ────────────────────────────────────────────
    [Serializable]
    public class AdminUserResponse
    {
        public int id;
        public string login_id;
        public string username;
        public string role;
        public bool is_banned;
        public string last_login_at;
        public string token;
    }

    [Serializable]
    private class AdminUserListWrapper { public List<AdminUserResponse> users; }
}
