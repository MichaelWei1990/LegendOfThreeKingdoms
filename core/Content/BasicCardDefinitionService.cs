using System.Collections.Generic;
using LegendOfThreeKingdoms.Core.Model;

namespace LegendOfThreeKingdoms.Core.Content;

/// <summary>
/// Basic implementation of ICardDefinitionService for standard edition cards.
/// Provides mappings for all standard edition card definitions.
/// </summary>
public sealed class BasicCardDefinitionService : ICardDefinitionService
{
    private readonly Dictionary<string, CardDefinition> _definitions = new();

    /// <summary>
    /// Creates a new BasicCardDefinitionService and initializes standard edition card definitions.
    /// </summary>
    public BasicCardDefinitionService()
    {
        InitializeStandardEdition();
    }

    /// <summary>
    /// Initializes all standard edition card definitions.
    /// </summary>
    private void InitializeStandardEdition()
    {
        // Basic cards
        Register("Base.Slash", "杀", CardType.Basic, CardSubType.Slash);
        Register("Base.Dodge", "闪", CardType.Basic, CardSubType.Dodge);
        Register("Base.Peach", "桃", CardType.Basic, CardSubType.Peach);

        // Immediate trick cards
        Register("Trick.GuoheChaiqiao", "过河拆桥", CardType.Trick, CardSubType.GuoheChaiqiao);
        Register("Trick.ShunshouQianyang", "顺手牵羊", CardType.Trick, CardSubType.ShunshouQianyang);
        Register("Trick.WuzhongShengyou", "无中生有", CardType.Trick, CardSubType.WuzhongShengyou);
        Register("Trick.Harvest", "五谷丰登", CardType.Trick, CardSubType.Harvest);
        Register("Trick.TaoyuanJieyi", "桃园结义", CardType.Trick, CardSubType.TaoyuanJieyi);
        Register("Trick.WanjianQifa", "万箭齐发", CardType.Trick, CardSubType.WanjianQifa);
        Register("Trick.NanmanRushin", "南蛮入侵", CardType.Trick, CardSubType.NanmanRushin);
        Register("Trick.Duel", "决斗", CardType.Trick, CardSubType.Duel);
        Register("Trick.JieDaoShaRen", "借刀杀人", CardType.Trick, CardSubType.JieDaoShaRen);
        Register("Trick.Wuxiekeji", "无懈可击", CardType.Trick, CardSubType.Wuxiekeji);

        // Delayed trick cards
        Register("Trick.Lebusishu", "乐不思蜀", CardType.Trick, CardSubType.Lebusishu);
        Register("Trick.Shandian", "闪电", CardType.Trick, CardSubType.Shandian);

        // Equipment cards - Weapons
        Register("Equip.Zhugeliannu", "诸葛连弩", CardType.Equip, CardSubType.Weapon);
        Register("Equip.CixiongShuanggujian", "雌雄双股剑", CardType.Equip, CardSubType.Weapon);
        Register("Equip.HanbingJian", "寒冰剑", CardType.Equip, CardSubType.Weapon);
        Register("Equip.QinglongYanyueDao", "青龍偃月刀", CardType.Equip, CardSubType.Weapon);
        Register("Equip.QinggangJian", "青釭劍", CardType.Equip, CardSubType.Weapon);
        Register("Equip.QilinGong", "麒麟弓", CardType.Equip, CardSubType.Weapon);
        Register("Equip.ZhangbaShemao", "丈八蛇矛", CardType.Equip, CardSubType.Weapon);
        Register("Equip.Guanshifu", "贯石斧", CardType.Equip, CardSubType.Weapon);
        Register("Equip.FangtianHuaji", "方天画戟", CardType.Equip, CardSubType.Weapon);

        // Equipment cards - Armor
        Register("Equip.BaguaZhen", "八卦阵", CardType.Equip, CardSubType.Armor);
        Register("Equip.RenwangDun", "仁王盾", CardType.Equip, CardSubType.Armor);

        // Equipment cards - Offensive Horses (-1 distance)
        Register("Equip.Jueying", "绝影", CardType.Equip, CardSubType.OffensiveHorse);
        Register("Equip.Chitu", "赤兔", CardType.Equip, CardSubType.OffensiveHorse);
        Register("Equip.ZhaohuangFeidian", "爪黄飞电", CardType.Equip, CardSubType.OffensiveHorse);
        Register("Equip.Dawan", "大宛", CardType.Equip, CardSubType.OffensiveHorse);

        // Equipment cards - Defensive Horses (+1 distance)
        Register("Equip.Dilu", "的卢", CardType.Equip, CardSubType.DefensiveHorse);
        Register("Equip.Zixing", "紫騂", CardType.Equip, CardSubType.DefensiveHorse);
    }

    /// <summary>
    /// Registers a card definition.
    /// </summary>
    private void Register(string definitionId, string name, CardType cardType, CardSubType cardSubType)
    {
        _definitions[definitionId] = new CardDefinition
        {
            DefinitionId = definitionId,
            Name = name,
            CardType = cardType,
            CardSubType = cardSubType
        };
    }

    /// <inheritdoc />
    public CardDefinition? GetDefinition(string definitionId)
    {
        if (string.IsNullOrWhiteSpace(definitionId))
            return null;

        return _definitions.TryGetValue(definitionId, out var def) ? def : null;
    }

    /// <inheritdoc />
    public bool HasDefinition(string definitionId)
    {
        if (string.IsNullOrWhiteSpace(definitionId))
            return false;

        return _definitions.ContainsKey(definitionId);
    }
}
