using InTheArena.MainGame;
using InTheArena.Unit;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public sealed class UI_UnitDescription_Popup : UI_Base
{
    [Header("Unit")]
    [SerializeField] private Image m_UnitIcon;
    [SerializeField] private TMP_Text m_UnitName;
    [SerializeField] private TMP_Text m_UnitDescription;

    [Header("Skill")]
    [SerializeField] private Image m_SkillIcon;
    [SerializeField] private TMP_Text m_SkillName;
    [SerializeField] private TMP_Text m_SkillDescription;

    [Header("Stat")]
    [SerializeField] private TMP_Text m_Hp;
    [SerializeField] private TMP_Text m_Attack;
    [SerializeField] private TMP_Text m_Defense;
    [SerializeField] private TMP_Text m_AttackSpeed;
    [SerializeField] private TMP_Text m_MoveSpeed;
    [SerializeField] private TMP_Text m_AttackRange;

    [SerializeField] private Button m_BackButton;

    private UnitData m_CurrentUnit;
    private int m_SkillIndex;

    /// <summary>닫기와 스킬 순환 입력을 연결합니다.</summary>
    protected override void Awake()
    {
        base.Awake();
        m_BackButton.onClick.AddListener(ClosePopup);
        if (m_SkillName != null)
        {
            m_SkillName.fontSizeMax = m_SkillName.fontSize;
            m_SkillName.fontSizeMin = m_SkillName.fontSize * 0.6f;
            m_SkillName.enableAutoSizing = true;
            m_SkillName.textWrappingMode = TextWrappingModes.NoWrap;
            Button nextFromName = m_SkillName.GetComponent<Button>();
            if (nextFromName == null)
            {
                nextFromName = m_SkillName.gameObject.AddComponent<Button>();
            }
            m_SkillName.raycastTarget = true;
            nextFromName.onClick.AddListener(ShowNextSkill);
        }
        if (m_SkillIcon != null)
        {
            Button next = m_SkillIcon.GetComponent<Button>();
            if (next == null)
            {
                next = m_SkillIcon.gameObject.AddComponent<Button>();
            }
            next.onClick.AddListener(ShowNextSkill);
        }
    }

    /// <summary>유닛 정보를 첫 스킬부터 표시합니다.</summary>
    public void Show(UnitData unitData)
    {
        if (unitData == null)
        {
            return;
        }

        m_CurrentUnit = unitData;
        m_SkillIndex = 0;

        Refresh();

        UIManager.Instance?.OpenControl(this);
    }

    /// <summary>현재 유닛의 스탯과 선택한 스킬 정보를 표시합니다.</summary>
    private void Refresh()
    {
        if (m_CurrentUnit == null)
        {
            return;
        }

        UnitStat stat = m_CurrentUnit.BaseStat;
        SkillData skill = null;
        if (m_CurrentUnit.SkillDatas != null && m_CurrentUnit.SkillDatas.Count > 0)
        {
            m_SkillIndex = Mathf.Clamp(m_SkillIndex, 0, m_CurrentUnit.SkillDatas.Count - 1);
            skill = m_CurrentUnit.SkillDatas[m_SkillIndex];
        }

        // Unit
        m_UnitName.text = m_CurrentUnit.DisplayName;
        m_UnitDescription.text = m_CurrentUnit.Description;
        m_UnitIcon.sprite = m_CurrentUnit.GetPortrait(Team.Blue);

        // Skill
        if (skill != null)
        {
            m_SkillName.text = skill.SkillName;
            if (m_CurrentUnit.SkillDatas.Count > 1)
            {
                m_SkillName.text += $" ({m_SkillIndex + 1}/{m_CurrentUnit.SkillDatas.Count}) >";
            }
            m_SkillDescription.text = skill.Description;
            m_SkillIcon.sprite = skill.Icon;
            if (m_SkillIcon != null)
            {
                m_SkillIcon.enabled = skill.Icon != null;
            }
        }
        else
        {
            m_SkillName.text = string.Empty;
            m_SkillDescription.text = string.Empty;
            m_SkillIcon.sprite = null;
            if (m_SkillIcon != null)
            {
                m_SkillIcon.enabled = false;
            }
        }

        // Stat
        m_Hp.text = FormatStat(stat.maxHp);
        m_Attack.text = FormatStat(stat.attackPower);
        m_Defense.text = FormatStat(stat.defense);
        m_AttackSpeed.text = FormatStat(stat.attackSpeed);
        m_MoveSpeed.text = FormatStat(stat.moveSpeed);
        m_AttackRange.text = FormatStat(stat.attackRange);
    }

    /// <summary>복수 스킬을 기존 설명 영역에서 순환 표시합니다.</summary>
    public void ShowNextSkill()
    {
        if (m_CurrentUnit == null || m_CurrentUnit.SkillDatas == null || m_CurrentUnit.SkillDatas.Count < 2)
        {
            return;
        }
        m_SkillIndex = (m_SkillIndex + 1) % m_CurrentUnit.SkillDatas.Count;
        Refresh();
    }

    /// <summary>정수 스탯과 소수 스탯을 읽기 쉬운 문자열로 변환합니다.</summary>
    private string FormatStat(float value)
    {
        if (Mathf.Approximately(value, Mathf.Round(value)))
        {
            return Mathf.RoundToInt(value).ToString();
        }
        return value.ToString("0.0");
    }

    /// <summary>닫기 효과음을 재생하고 팝업을 닫습니다.</summary>
    private void ClosePopup()
    {
        SoundManager.Instance?.PlaySfx(SfxIds.ButtonNegative);
        UIManager.Instance?.CloseControl(this);
    }
}
