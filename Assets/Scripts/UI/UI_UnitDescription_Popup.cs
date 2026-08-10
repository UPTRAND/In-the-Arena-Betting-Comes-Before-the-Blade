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

    protected override void Awake()
    {
        base.Awake();
        m_BackButton.onClick.AddListener(ClosePopup);
    }

    public void Show(UnitData unitData)
    {
        if (unitData == null)
            return;

        m_CurrentUnit = unitData;

        Refresh();

        UIManager.Instance?.OpenControl(this);
    }

    private void Refresh()
    {
        if (m_CurrentUnit == null)
            return;

        UnitStat stat = m_CurrentUnit.BaseStat;
        SkillData skill = m_CurrentUnit.SkillData;

        // Unit
        m_UnitName.text = m_CurrentUnit.DisplayName;
        m_UnitDescription.text = m_CurrentUnit.Description;
        m_UnitIcon.sprite = m_CurrentUnit.GetPortrait(Team.Blue);

        // Skill
        if (skill != null)
        {
            m_SkillName.text = skill.SkillName;
            m_SkillDescription.text = skill.Description;
            m_SkillIcon.sprite = skill.Icon;
            if (m_SkillIcon != null) m_SkillIcon.enabled = skill.Icon != null;
        }
        else
        {
            m_SkillName.text = string.Empty;
            m_SkillDescription.text = string.Empty;
            m_SkillIcon.sprite = null;
            if (m_SkillIcon != null) m_SkillIcon.enabled = false;
        }

        // Stat
        m_Hp.text = FormatStat(stat.maxHp);
        m_Attack.text = FormatStat(stat.attackPower);
        m_Defense.text = FormatStat(stat.defense);
        m_AttackSpeed.text = FormatStat(stat.attackSpeed);
        m_MoveSpeed.text = FormatStat(stat.moveSpeed);
        m_AttackRange.text = FormatStat(stat.attackRange);
    }

    
    private string FormatStat(float value)
    {
        return Mathf.Approximately(value, Mathf.Round(value))
            ? Mathf.RoundToInt(value).ToString()
            : value.ToString("0.0");
    }
private void ClosePopup()
    {
        SoundManager.Instance?.PlaySfx(SfxIds.ButtonNegative);
        UIManager.Instance?.CloseControl(this);
    }
}