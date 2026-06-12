using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StarterWeaponPanel : MonoBehaviour
{
    [Header("무기 설명 텍스트")]
    [SerializeField] private TMP_Text swordDescription;
    [SerializeField] private TMP_Text staffDescription;
    [SerializeField] private TMP_Text bowDescription;

    [Header("선택 버튼")]
    [SerializeField] private Button swordButton;
    [SerializeField] private Button staffButton;
    [SerializeField] private Button bowButton;

    private const int SwordItemId = 3080;
    private const int StaffItemId = 3084;
    private const int BowItemId   = 3088;

    private void Awake()
    {
        swordButton?.onClick.AddListener(() => StartCoroutine(SelectWeapon(SwordItemId)));
        staffButton?.onClick.AddListener(() => StartCoroutine(SelectWeapon(StaffItemId)));
        bowButton?.onClick.AddListener(()  => StartCoroutine(SelectWeapon(BowItemId)));
    }

    private void OnEnable()
    {
        SetDescription(swordDescription, SwordItemId);
        SetDescription(staffDescription, StaffItemId);
        SetDescription(bowDescription,   BowItemId);
    }

    private static void SetDescription(TMP_Text text, int itemId)
    {
        if (text == null || GameDataSO.Instance == null) return;

        ItemEntry            item        = GameDataSO.Instance.ItemTable   != null ? GameDataSO.Instance.ItemTable.GetById(itemId)   : null;
        EquipmentDetailEntry equipDetail = GameDataSO.Instance.EquipDetail != null ? GameDataSO.Instance.EquipDetail.GetById(itemId) : null;

        text.text = ItemInfoPanel.BuildText(item, 0, equipDetail, null, showPrice: false);
    }

    private IEnumerator SelectWeapon(int itemId)
    {
        if (swordButton) swordButton.interactable = false;
        if (staffButton) staffButton.interactable = false;
        if (bowButton)   bowButton.interactable   = false;

        yield return StartCoroutine(DataAPI.AddItem(UserSession.DbUserId, itemId, 1));

        if (swordButton) swordButton.interactable = true;
        if (staffButton) staffButton.interactable = true;
        if (bowButton)   bowButton.interactable   = true;

        gameObject.SetActive(false);
    }
}
